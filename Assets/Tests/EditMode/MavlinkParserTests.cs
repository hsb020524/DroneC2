using System.Collections.Generic;
using DroneC2.DataSource;
using DroneC2.DataSource.Mavlink;
using NUnit.Framework;

namespace DroneC2.Tests
{
    public class MavlinkParserTests
    {
        // 독립 구현(Python)으로 만든 기준 프레임. 파서와 같은 코드를 쓰지 않는다.
        // v2 GLOBAL_POSITION_INT, sys=1 comp=1, 끝의 vz=0·hdg=0 4바이트가 잘려 payload 24바이트.
        // lat 35.563, lon -83.499, alt 1900 m, rel 80 m, vx 3.0, vy -4.0
        static readonly byte[] V2TruncatedPosition =
        {
            0xfd, 0x18, 0x00, 0x00, 0x07, 0x01, 0x01, 0x21, 0x00, 0x00,
            0xe8, 0x03, 0x00, 0x00, 0xb0, 0x7b, 0x32, 0x15, 0x50, 0x10, 0x3b, 0xce,
            0xe0, 0xfd, 0x1c, 0x00, 0x80, 0x38, 0x01, 0x00, 0x2c, 0x01, 0x70, 0xfe,
            0x15, 0x6d,
        };

        // v1 HEARTBEAT, sys=1 comp=1, type=2(QUADROTOR) system_status=4(ACTIVE)
        static readonly byte[] V1Heartbeat =
        {
            0xfe, 0x09, 0x03, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x02, 0x03, 0x51, 0x04, 0x03, 0x43, 0x5e,
        };

        // v2 HEARTBEAT 전체 9바이트, 같은 내용
        static readonly byte[] V2Heartbeat =
        {
            0xfd, 0x09, 0x00, 0x00, 0x04, 0x01, 0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x02, 0x03, 0x51, 0x04, 0x03, 0x85, 0x36,
        };

        static List<MavlinkFrame> ParseAll(MavlinkParser parser, params byte[][] chunks)
        {
            var list = new List<MavlinkFrame>();
            var all = new List<byte>();
            foreach (byte[] c in chunks) all.AddRange(c);
            byte[] data = all.ToArray();
            parser.Parse(data, 0, data.Length, list);
            return list;
        }

        // 테스트용 v2 프레임 조립(이미 잘린 payload를 받는다).
        static byte[] BuildV2(uint msgId, byte crcExtra, byte[] payload, byte incompat = 0, bool addSignature = false)
        {
            var f = new List<byte>
            {
                MavlinkParser.StxV2, (byte)payload.Length, incompat, 0, 1, 1, 1,
                (byte)(msgId & 0xFF), (byte)((msgId >> 8) & 0xFF), (byte)((msgId >> 16) & 0xFF),
            };
            f.AddRange(payload);
            byte[] arr = f.ToArray();
            ushort crc = MavlinkCrc.Compute(arr, 1, arr.Length - 1);
            crc = MavlinkCrc.Accumulate(crcExtra, crc);
            f.Add((byte)(crc & 0xFF));
            f.Add((byte)(crc >> 8));
            if (addSignature) f.AddRange(new byte[13]);
            return f.ToArray();
        }

        [Test]
        public void Crc_KnownAnswer_Mcrf4xx()
        {
            byte[] data = System.Text.Encoding.ASCII.GetBytes("123456789");
            Assert.AreEqual(0x6F91, MavlinkCrc.Compute(data, 0, data.Length));
        }

        [Test]
        public void V2_TruncatedPayload_IsZeroFilledAndAccepted()
        {
            var parser = new MavlinkParser();
            var frames = ParseAll(parser, V2TruncatedPosition);

            Assert.AreEqual(1, frames.Count);
            Assert.AreEqual(28, frames[0].Payload.Length);
            Assert.AreEqual(1, frames[0].SystemId);

            Assert.IsTrue(MavlinkDecoder.TryDecodeGlobalPosition(frames[0], out var pos));
            Assert.AreEqual(35.563, pos.LatitudeDeg, 1e-9);
            Assert.AreEqual(-83.499, pos.LongitudeDeg, 1e-9);
            Assert.AreEqual(1900.0, pos.MslAltitudeM, 1e-9);
            Assert.AreEqual(80.0, pos.RelativeAltitudeM, 1e-9);
            Assert.AreEqual(5.0, pos.GroundSpeedMps, 1e-9);
            // 잘려서 0이 된 hdg는 "알 수 없음"이 아니라 정북이다.
            Assert.IsTrue(pos.HeadingKnown);
            Assert.AreEqual(0.0, pos.HeadingDeg, 1e-9);
        }

        [Test]
        public void V2_HeadingUnknown_65535_IsNotKnown()
        {
            var payload = new byte[28];
            payload[26] = 0xFF;
            payload[27] = 0xFF;
            var parser = new MavlinkParser();
            var frames = ParseAll(parser, BuildV2(33, 104, payload));

            Assert.AreEqual(1, frames.Count);
            Assert.IsTrue(MavlinkDecoder.TryDecodeGlobalPosition(frames[0], out var pos));
            Assert.IsFalse(pos.HeadingKnown);
        }

        [Test]
        public void V2_AllZeroPayloadTruncatedToOneByte_IsAccepted()
        {
            var parser = new MavlinkParser();
            var frames = ParseAll(parser, BuildV2(0, 50, new byte[] { 0 }));

            Assert.AreEqual(1, frames.Count);
            Assert.AreEqual(9, frames[0].Payload.Length);
        }

        [Test]
        public void V1_Heartbeat_Decodes()
        {
            var parser = new MavlinkParser();
            var frames = ParseAll(parser, V1Heartbeat);

            Assert.AreEqual(1, frames.Count);
            Assert.IsTrue(MavlinkDecoder.TryDecodeHeartbeat(frames[0], out var hb));
            Assert.AreEqual(2, hb.MavType);
            Assert.AreEqual(4, hb.SystemStatus);
            Assert.IsFalse(hb.IsWarning);
            Assert.IsTrue(hb.TryGetKind(out UnitKind kind));
            Assert.AreEqual(UnitKind.Drone, kind);
        }

        [Test]
        public void Counters_SeparateV1AndV2()
        {
            var parser = new MavlinkParser();
            var frames = ParseAll(parser, V1Heartbeat, V2Heartbeat, V2TruncatedPosition);

            Assert.AreEqual(3, frames.Count);
            Assert.AreEqual(1, parser.V1Frames);
            Assert.AreEqual(2, parser.V2Frames);
            Assert.AreEqual(0, parser.Rejected);
        }

        [Test]
        public void GarbageBetweenFrames_IsSkipped()
        {
            var parser = new MavlinkParser();
            var frames = ParseAll(parser,
                new byte[] { 0x00, 0x11, 0xFD, 0x22 }, V1Heartbeat, new byte[] { 0xFE, 0xFD }, V2Heartbeat);

            Assert.AreEqual(2, frames.Count);
        }

        [Test]
        public void CorruptedCrc_IsRejected()
        {
            byte[] bad = (byte[])V2Heartbeat.Clone();
            bad[bad.Length - 1] ^= 0xFF;
            var parser = new MavlinkParser();

            Assert.AreEqual(0, ParseAll(parser, bad).Count);
            Assert.AreEqual(0, parser.V2Frames);
            Assert.GreaterOrEqual(parser.Rejected, 1);
        }

        [Test]
        public void CorruptedPayload_IsRejected()
        {
            byte[] bad = (byte[])V2TruncatedPosition.Clone();
            bad[12] ^= 0x01;
            var parser = new MavlinkParser();

            Assert.AreEqual(0, ParseAll(parser, bad).Count);
        }

        [Test]
        public void UnknownMessageId_IsRejected()
        {
            var parser = new MavlinkParser();
            Assert.AreEqual(0, ParseAll(parser, BuildV2(999, 0, new byte[] { 1, 2, 3 })).Count);
        }

        [Test]
        public void PayloadLongerThanDefinition_IsRejected()
        {
            var parser = new MavlinkParser();
            Assert.AreEqual(0, ParseAll(parser, BuildV2(0, 50, new byte[10])).Count);
        }

        [Test]
        public void TruncatedDatagram_IsRejected()
        {
            byte[] cut = new byte[V2TruncatedPosition.Length - 5];
            System.Array.Copy(V2TruncatedPosition, cut, cut.Length);
            var parser = new MavlinkParser();

            Assert.AreEqual(0, ParseAll(parser, cut).Count);
        }

        [Test]
        public void UnknownIncompatFlags_AreRejected()
        {
            var parser = new MavlinkParser();
            Assert.AreEqual(0, ParseAll(parser, BuildV2(0, 50, new byte[9], incompat: 0x02)).Count);
        }

        [Test]
        public void SignedFrame_SignatureIsSkipped()
        {
            byte[] signed = BuildV2(0, 50, new byte[] { 0, 0, 0, 0, 2, 3, 0x51, 4, 3 }, incompat: 0x01, addSignature: true);
            var parser = new MavlinkParser();
            var frames = ParseAll(parser, signed, V1Heartbeat);

            Assert.AreEqual(2, frames.Count);
        }

        [Test]
        public void InvalidArguments_ReturnZero()
        {
            var parser = new MavlinkParser();
            var list = new List<MavlinkFrame>();

            Assert.AreEqual(0, parser.Parse(null, 0, 1, list));
            Assert.AreEqual(0, parser.Parse(new byte[4], 2, 5, list));
            Assert.AreEqual(0, parser.Parse(new byte[4], 0, 0, list));
        }

        [Test]
        public void Decode_LatitudeOutOfRange_IsRejected()
        {
            var payload = new byte[28];
            System.BitConverter.GetBytes(1_000_000_000).CopyTo(payload, 4); // 100도
            var frame = new MavlinkFrame(1, 1, MavlinkParser.MsgGlobalPositionInt, payload);

            Assert.IsFalse(MavlinkDecoder.TryDecodeGlobalPosition(frame, out _));
        }

        [TestCase(5, true)]
        [TestCase(6, true)]
        [TestCase(4, false)]
        [TestCase(3, false)]
        public void Heartbeat_WarningMapping(int systemStatus, bool warning)
        {
            var hb = new MavlinkHeartbeat(2, (byte)systemStatus);
            Assert.AreEqual(warning, hb.IsWarning);
        }

        [TestCase(2, true, UnitKind.Drone)]
        [TestCase(13, true, UnitKind.Drone)]
        [TestCase(10, true, UnitKind.Tank)]
        [TestCase(6, false, UnitKind.Drone)] // GCS
        public void Heartbeat_KindMapping(int mavType, bool mapped, UnitKind expected)
        {
            var hb = new MavlinkHeartbeat((byte)mavType, 4);
            Assert.AreEqual(mapped, hb.TryGetKind(out UnitKind kind));
            if (mapped) Assert.AreEqual(expected, kind);
        }
    }
}
