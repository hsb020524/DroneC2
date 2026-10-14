using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using DroneC2.DataSource;
using DroneC2.DataSource.Mavlink;
using NUnit.Framework;
using UnityEngine;

namespace DroneC2.Tests
{
    public class MavlinkRoutingTests
    {
        sealed class FakeLookup : IMavlinkUnitLookup
        {
            readonly Dictionary<byte, MavlinkUnitEntry> map = new Dictionary<byte, MavlinkUnitEntry>();

            public FakeLookup Add(byte sysid, string unitId, UnitKind kind, double homeLat = 35.563, double homeLon = -83.499)
            {
                map[sysid] = new MavlinkUnitEntry
                {
                    systemId = sysid, unitId = unitId, kind = kind, homeLatitude = homeLat, homeLongitude = homeLon,
                };
                return this;
            }

            public bool TryGet(byte systemId, out MavlinkUnitEntry entry) => map.TryGetValue(systemId, out entry);
        }

        static MavlinkFrame Heartbeat(byte sysid, byte mavType, byte systemStatus)
        {
            var p = new byte[9];
            p[4] = mavType;
            p[7] = systemStatus;
            return new MavlinkFrame(sysid, 1, MavlinkParser.MsgHeartbeat, p);
        }

        static MavlinkFrame Position(byte sysid, double lat = 35.563, double lon = -83.499,
            double relAltM = 80.0, double vNorth = 0.0, double vEast = 0.0, ushort hdgCdeg = 65535)
        {
            var p = new byte[28];
            BitConverter.GetBytes((int)Math.Round(lat * 1e7)).CopyTo(p, 4);
            BitConverter.GetBytes((int)Math.Round(lon * 1e7)).CopyTo(p, 8);
            BitConverter.GetBytes((int)Math.Round((relAltM + 1500.0) * 1000.0)).CopyTo(p, 12);
            BitConverter.GetBytes((int)Math.Round(relAltM * 1000.0)).CopyTo(p, 16);
            BitConverter.GetBytes((short)Math.Round(vNorth * 100.0)).CopyTo(p, 20);
            BitConverter.GetBytes((short)Math.Round(vEast * 100.0)).CopyTo(p, 22);
            BitConverter.GetBytes(hdgCdeg).CopyTo(p, 26);
            return new MavlinkFrame(sysid, 1, MavlinkParser.MsgGlobalPositionInt, p);
        }

        static List<TelemetrySample> Collect(MavlinkTelemetryRouter router)
        {
            var list = new List<TelemetrySample>();
            router.SampleProduced += (_, s) => list.Add(s);
            return list;
        }

        // ---- 라우터 ----

        [Test]
        public void UnknownSysid_IsIgnoredAndCounted()
        {
            var router = new MavlinkTelemetryRouter(new FakeLookup().Add(1, "UAV-01", UnitKind.Drone), null);
            var samples = Collect(router);

            router.Handle(Position(9), 0.0);
            router.Handle(Heartbeat(9, 2, 4), 0.0);

            Assert.AreEqual(0, samples.Count);
            Assert.AreEqual(2, router.UnknownSysid);
        }

        [Test]
        public void Drone_DeclaresHomeRelative_WithRelativeAltAndTableHome()
        {
            var lookup = new FakeLookup().Add(1, "UAV-01", UnitKind.Drone, 35.5, -83.4);
            var router = new MavlinkTelemetryRouter(lookup, null);
            var samples = Collect(router);

            // 첫 샘플 위치(35.6, -83.5)는 홈이 아니다(비행 중 접속). 홈은 테이블 값이어야 한다.
            router.Handle(Position(1, lat: 35.6, lon: -83.5, relAltM: 80.0), 5.0);

            Assert.AreEqual(1, samples.Count);
            Assert.AreEqual("UAV-01", samples[0].UnitId);
            Assert.AreEqual(AltitudeMode.HomeRelative, samples[0].AltitudeReference);
            Assert.AreEqual(80.0, samples[0].AltitudeM, 1e-6); // MSL(1580)이 아니라 상대고도
            Assert.IsTrue(samples[0].HasHome);
            Assert.AreEqual(35.5, samples[0].HomeLatitude, 1e-9);
            Assert.AreEqual(-83.4, samples[0].HomeLongitude, 1e-9);
            Assert.AreEqual(35.6, samples[0].Latitude, 1e-6);
            Assert.AreEqual(UnitKind.Drone, samples[0].Kind);
            Assert.AreEqual(5.0, samples[0].Time, 1e-9);
        }

        [Test]
        public void Tank_DeclaresClampToGround_WithoutHome()
        {
            var router = new MavlinkTelemetryRouter(new FakeLookup().Add(7, "TNK-01", UnitKind.Tank), null);
            var samples = Collect(router);

            router.Handle(Position(7), 0.0);

            Assert.AreEqual(1, samples.Count);
            Assert.AreEqual(UnitKind.Tank, samples[0].Kind);
            Assert.AreEqual(AltitudeMode.ClampToGround, samples[0].AltitudeReference);
            Assert.IsFalse(samples[0].HasHome);
        }

        [Test]
        public void Kind_FollowsTable_NotHeartbeat_ForAltitudeDeclaration()
        {
            // 테이블은 전차인데 기체는 쿼드콥터(MAV_TYPE 2)라고 해도 테이블을 따른다.
            var router = new MavlinkTelemetryRouter(new FakeLookup().Add(7, "TNK-01", UnitKind.Tank), null);
            var samples = Collect(router);

            router.Handle(Heartbeat(7, 2, 4), 0.0);
            router.Handle(Position(7), 0.1);

            Assert.AreEqual(UnitKind.Tank, samples[0].Kind);
            Assert.AreEqual(AltitudeMode.ClampToGround, samples[0].AltitudeReference);
        }

        [Test]
        public void KindMismatch_FollowsTableAndWarnsOncePerSysid()
        {
            var warnings = new List<string>();
            var lookup = new FakeLookup().Add(1, "UAV-01", UnitKind.Drone).Add(2, "UAV-02", UnitKind.Drone);
            var router = new MavlinkTelemetryRouter(lookup, warnings.Add);
            var samples = Collect(router);

            router.Handle(Heartbeat(1, 10, 4), 0.0); // 테이블은 드론, 기체는 ROVER
            router.Handle(Heartbeat(1, 10, 4), 0.1);
            router.Handle(Heartbeat(1, 10, 4), 0.2);
            router.Handle(Position(1), 0.3);

            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("sysid 1", warnings[0]);
            Assert.AreEqual(UnitKind.Drone, samples[0].Kind);

            // 다른 sysid는 따로 한 번 더 경고한다.
            router.Handle(Heartbeat(2, 10, 4), 0.4);
            Assert.AreEqual(2, warnings.Count);
        }

        [Test]
        public void MatchingKind_DoesNotWarn()
        {
            var warnings = new List<string>();
            var router = new MavlinkTelemetryRouter(new FakeLookup().Add(1, "UAV-01", UnitKind.Drone), warnings.Add);

            router.Handle(Heartbeat(1, 2, 4), 0.0);

            Assert.AreEqual(0, warnings.Count);
        }

        [Test]
        public void HeartbeatStatus_IsCarriedIntoSamples()
        {
            var router = new MavlinkTelemetryRouter(new FakeLookup().Add(1, "UAV-01", UnitKind.Drone), null);
            var samples = Collect(router);

            router.Handle(Position(1), 0.0);
            router.Handle(Heartbeat(1, 2, 5), 0.1); // CRITICAL
            router.Handle(Position(1), 0.2);
            router.Handle(Heartbeat(1, 2, 4), 0.3); // ACTIVE
            router.Handle(Position(1), 0.4);

            Assert.AreEqual(ReportedStatus.Normal, samples[0].Status);
            Assert.AreEqual(ReportedStatus.Warning, samples[1].Status);
            Assert.AreEqual(ReportedStatus.Normal, samples[2].Status);
        }

        [Test]
        public void Heading_UsesHdgWhenKnown_ElseVelocity_ElseLast()
        {
            var router = new MavlinkTelemetryRouter(new FakeLookup().Add(1, "UAV-01", UnitKind.Drone), null);
            var samples = Collect(router);

            router.Handle(Position(1, hdgCdeg: 9000), 0.0);                       // 90도
            router.Handle(Position(1, vNorth: 0.0, vEast: -5.0), 0.1);            // hdg 모름, 서쪽 이동 → 270도
            router.Handle(Position(1, vNorth: 0.0, vEast: 0.0), 0.2);             // 정지 → 마지막 값 유지

            Assert.AreEqual(90.0, samples[0].HeadingDeg, 1e-6);
            Assert.AreEqual(270.0, samples[1].HeadingDeg, 1e-6);
            Assert.AreEqual(5.0, samples[1].SpeedMps, 1e-6);
            Assert.AreEqual(270.0, samples[2].HeadingDeg, 1e-6);
        }

        [Test]
        public void ParserToRouter_TruncatedFrameProducesSample()
        {
            // MavlinkParserTests의 기준 프레임과 같은 바이트열(끝의 vz·hdg 0이 잘린 v2).
            byte[] frameBytes =
            {
                0xfd, 0x18, 0x00, 0x00, 0x07, 0x01, 0x01, 0x21, 0x00, 0x00,
                0xe8, 0x03, 0x00, 0x00, 0xb0, 0x7b, 0x32, 0x15, 0x50, 0x10, 0x3b, 0xce,
                0xe0, 0xfd, 0x1c, 0x00, 0x80, 0x38, 0x01, 0x00, 0x2c, 0x01, 0x70, 0xfe,
                0x15, 0x6d,
            };
            var frames = new List<MavlinkFrame>();
            new MavlinkParser().Parse(frameBytes, 0, frameBytes.Length, frames);
            var router = new MavlinkTelemetryRouter(new FakeLookup().Add(1, "UAV-01", UnitKind.Drone), null);
            var samples = Collect(router);

            router.Handle(frames[0], 1.0);

            Assert.AreEqual(1, samples.Count);
            Assert.AreEqual(35.563, samples[0].Latitude, 1e-9);
            Assert.AreEqual(80.0, samples[0].AltitudeM, 1e-6);
            Assert.AreEqual(AltitudeMode.HomeRelative, samples[0].AltitudeReference);
            Assert.AreEqual(5.0, samples[0].SpeedMps, 1e-6);
            Assert.AreEqual(0.0, samples[0].HeadingDeg, 1e-6);
        }

        // ---- 테이블 ----

        [Test]
        public void Table_LookupBySystemId()
        {
            var table = ScriptableObject.CreateInstance<MavlinkUnitTable>();
            try
            {
                table.SetEntries(new[]
                {
                    new MavlinkUnitEntry { systemId = 1, unitId = "UAV-01", kind = UnitKind.Drone, homeLatitude = 35.563, homeLongitude = -83.499 },
                    new MavlinkUnitEntry { systemId = 4, unitId = "TNK-01", kind = UnitKind.Tank, homeLatitude = 35.56, homeLongitude = -83.5 },
                });

                Assert.IsTrue(table.TryGet(4, out MavlinkUnitEntry e));
                Assert.AreEqual("TNK-01", e.unitId);
                Assert.IsFalse(table.TryGet(2, out _));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        // ---- UDP 수신기 (루프백) ----

        [Test]
        public void UdpListener_ReceivesFramesOnLoopback()
        {
            byte[] hb =
            {
                0xfd, 0x09, 0x00, 0x00, 0x04, 0x01, 0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x02, 0x03, 0x51, 0x04, 0x03, 0x85, 0x36,
            };

            using (var listener = new MavlinkUdpListener(0))
            using (var sender = new UdpClient())
            {
                listener.Start();
                Assert.Greater(listener.LocalPort, 0);

                sender.Send(hb, hb.Length, new IPEndPoint(IPAddress.Loopback, listener.LocalPort));
                sender.Send(new byte[] { 1, 2, 3 }, 3, new IPEndPoint(IPAddress.Loopback, listener.LocalPort)); // 쓰레기

                MavlinkFrame frame = default;
                bool got = false;
                for (int i = 0; i < 100 && !got; i++)
                {
                    got = listener.TryDequeue(out frame);
                    if (!got) Thread.Sleep(10);
                }

                Assert.IsTrue(got);
                Assert.AreEqual(1, frame.SystemId);
                Assert.AreEqual(MavlinkParser.MsgHeartbeat, frame.MessageId);
                Assert.AreEqual(1, listener.Parser.V2Frames);
                Assert.IsNull(listener.FaultMessage);
            }
        }

        [Test]
        public void UdpListener_FullQueueDropsOldest()
        {
            byte[] hb =
            {
                0xfd, 0x09, 0x00, 0x00, 0x04, 0x01, 0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x02, 0x03, 0x51, 0x04, 0x03, 0x85, 0x36,
            };

            using (var listener = new MavlinkUdpListener(0, maxQueued: 3))
            using (var sender = new UdpClient())
            {
                listener.Start();
                for (int i = 0; i < 10; i++)
                    sender.Send(hb, hb.Length, new IPEndPoint(IPAddress.Loopback, listener.LocalPort));

                for (int i = 0; i < 200 && listener.Parser.V2Frames < 10; i++)
                    Thread.Sleep(10);

                int count = 0;
                while (listener.TryDequeue(out _)) count++;

                Assert.AreEqual(10, listener.Parser.V2Frames);
                Assert.LessOrEqual(count, 3);
                Assert.AreEqual(10 - count, listener.Dropped);
            }
        }

        [Test]
        public void UdpListener_DisposeStopsThread_AndPortCanBeReused()
        {
            var first = new MavlinkUdpListener(0);
            first.Start();
            int port = first.LocalPort;
            first.Dispose();

            using (var second = new MavlinkUdpListener(port))
            {
                Assert.DoesNotThrow(() => second.Start());
            }
        }

        [Test]
        public void UdpListener_BindFailureThrows_WhenPortInUse()
        {
            using (var first = new MavlinkUdpListener(0))
            {
                first.Start();
                using (var second = new MavlinkUdpListener(first.LocalPort))
                {
                    Assert.Throws<SocketException>(() => second.Start());
                }
            }
        }
    }
}
