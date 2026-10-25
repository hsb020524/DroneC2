using System;
using System.Collections.Generic;
using System.Threading;

namespace DroneC2.DataSource.Mavlink
{
    /// <summary>
    /// UDP 데이터그램 한 개 안의 MAVLink v1·v2 프레임을 추출한다. 수신 전용이며 송신 기능은 없다.
    /// 우리는 송신하지 않아 상대의 프로토콜 버전을 바꿀 수 없으므로 v1·v2를 모두 받는다.
    /// 외부에서 온 바이트는 신뢰하지 않는다. 알려진 메시지만, CRC가 맞는 것만 통과시킨다.
    /// 알려지지 않은 메시지(미지원)는 정상 트래픽이므로 헤더의 길이만큼 통째로 건너뛰고 따로 센다.
    /// 그 외 검사에서 실패한 후보는 손상으로 세고 한 바이트씩 건너뛰며 다음 시작 바이트를 찾는다.
    /// 카운터는 수신 스레드가 쓰고 다른 스레드가 읽어도 된다.
    /// </summary>
    public sealed class MavlinkParser
    {
        public const byte StxV1 = 0xFE;
        public const byte StxV2 = 0xFD;

        public const uint MsgHeartbeat = 0;
        public const uint MsgGlobalPositionInt = 33;

        const byte IncompatSigned = 0x01;
        const int SignatureLength = 13;
        const int HeartbeatLength = 9;
        const int GlobalPositionIntLength = 28;

        enum Outcome
        {
            Frame,
            Unsupported,
            Truncated,
            BadFlags,
            BadLength,
            BadCrc,
        }

        long v1Frames;
        long v2Frames;
        long unsupported;
        long truncated;
        long badFlags;
        long badLength;
        long badCrc;

        /// <summary>통과한 v1 프레임 수.</summary>
        public long V1Frames => Interlocked.Read(ref v1Frames);

        /// <summary>통과한 v2 프레임 수.</summary>
        public long V2Frames => Interlocked.Read(ref v2Frames);

        /// <summary>형식은 맞지만 해석하지 않는 메시지 ID의 프레임 수. 정상 트래픽이며 오류가 아니다.</summary>
        public long Unsupported => Interlocked.Read(ref unsupported);

        /// <summary>데이터그램 끝에서 잘린 후보 수(손상).</summary>
        public long Truncated => Interlocked.Read(ref truncated);

        /// <summary>이해할 수 없는 incompat 플래그를 가진 v2 후보 수(손상).</summary>
        public long BadFlags => Interlocked.Read(ref badFlags);

        /// <summary>알려진 메시지인데 길이가 정의와 맞지 않는 후보 수(손상).</summary>
        public long BadLength => Interlocked.Read(ref badLength);

        /// <summary>알려진 메시지인데 CRC가 맞지 않는 후보 수(손상).</summary>
        public long BadCrc => Interlocked.Read(ref badCrc);

        /// <summary>손상으로 버린 후보 수(잘림·플래그·길이·CRC의 합). 미지원 메시지는 포함하지 않는다.</summary>
        public long Corrupted => Truncated + BadFlags + BadLength + BadCrc;

        /// <summary>통과하지 못한 후보 전체(손상 + 미지원). 이전 호환용 합계다.</summary>
        public long Rejected => Corrupted + Unsupported;

        /// <summary>해석 가능한 메시지의 (CRC_EXTRA, 페이로드 정의 길이). 모르는 메시지는 검증이 불가능하므로 통과시키지 않는다.</summary>
        static bool TryGetDefinition(uint msgId, out byte crcExtra, out int length)
        {
            switch (msgId)
            {
                case MsgHeartbeat:
                    crcExtra = 50;
                    length = HeartbeatLength;
                    return true;
                case MsgGlobalPositionInt:
                    crcExtra = 104;
                    length = GlobalPositionIntLength;
                    return true;
                default:
                    crcExtra = 0;
                    length = 0;
                    return false;
            }
        }

        /// <summary>buffer[offset..offset+count)에서 유효한 프레임을 모두 찾아 output에 추가하고, 추가한 개수를 반환한다.</summary>
        public int Parse(byte[] buffer, int offset, int count, List<MavlinkFrame> output)
        {
            if (buffer == null || output == null || offset < 0 || count <= 0 || offset + count > buffer.Length)
                return 0;

            int found = 0;
            int end = offset + count;
            int i = offset;
            while (i < end)
            {
                byte stx = buffer[i];
                if (stx != StxV2 && stx != StxV1)
                {
                    i++;
                    continue;
                }

                int consumed;
                Outcome outcome = stx == StxV2
                    ? TryParseV2(buffer, i, end, output, out consumed)
                    : TryParseV1(buffer, i, end, output, out consumed);

                switch (outcome)
                {
                    case Outcome.Frame:
                        if (stx == StxV2)
                            Interlocked.Increment(ref v2Frames);
                        else
                            Interlocked.Increment(ref v1Frames);
                        found++;
                        break;
                    case Outcome.Unsupported:
                        Interlocked.Increment(ref unsupported);
                        break;
                    case Outcome.Truncated:
                        Interlocked.Increment(ref truncated);
                        break;
                    case Outcome.BadFlags:
                        Interlocked.Increment(ref badFlags);
                        break;
                    case Outcome.BadLength:
                        Interlocked.Increment(ref badLength);
                        break;
                    case Outcome.BadCrc:
                        Interlocked.Increment(ref badCrc);
                        break;
                }

                // 프레임과 미지원 메시지는 전체를 소비한다. 손상 후보는 한 바이트만 넘긴다.
                i += consumed > 0 ? consumed : 1;
            }
            return found;
        }

        // 헤더 6 + 페이로드 + CRC 2
        static Outcome TryParseV1(byte[] b, int i, int end, List<MavlinkFrame> output, out int consumed)
        {
            consumed = 0;
            if (end - i < 8) return Outcome.Truncated;
            int len = b[i + 1];
            int total = 6 + len + 2;
            if (end - i < total) return Outcome.Truncated;

            uint msgId = b[i + 5];
            if (!TryGetDefinition(msgId, out byte extra, out int defLen))
            {
                // CRC_EXTRA를 몰라 검증할 수 없다. 길이만큼 건너뛴다.
                consumed = total;
                return Outcome.Unsupported;
            }
            if (len != defLen) return Outcome.BadLength;

            ushort crc = MavlinkCrc.Compute(b, i + 1, 5 + len);
            crc = MavlinkCrc.Accumulate(extra, crc);
            if (crc != (ushort)(b[i + 6 + len] | (b[i + 7 + len] << 8))) return Outcome.BadCrc;

            var payload = new byte[defLen];
            Array.Copy(b, i + 6, payload, 0, len);
            output.Add(new MavlinkFrame(b[i + 3], b[i + 4], msgId, payload));
            consumed = total;
            return Outcome.Frame;
        }

        // 헤더 10 + 페이로드 + CRC 2 (+ 서명 13)
        static Outcome TryParseV2(byte[] b, int i, int end, List<MavlinkFrame> output, out int consumed)
        {
            consumed = 0;
            if (end - i < 12) return Outcome.Truncated;
            int len = b[i + 1];
            byte incompat = b[i + 2];
            if ((incompat & ~IncompatSigned) != 0) return Outcome.BadFlags; // 이해할 수 없는 플래그는 길이도 믿을 수 없다
            int total = 10 + len + 2 + ((incompat & IncompatSigned) != 0 ? SignatureLength : 0);
            if (end - i < total) return Outcome.Truncated;

            uint msgId = (uint)(b[i + 7] | (b[i + 8] << 8) | (b[i + 9] << 16));
            if (!TryGetDefinition(msgId, out byte extra, out int defLen))
            {
                // 서명이 있으면 total에 서명 13바이트가 이미 들어 있다.
                consumed = total;
                return Outcome.Unsupported;
            }
            if (len > defLen) return Outcome.BadLength;

            // CRC는 잘려서 온 len 바이트 기준이다(MAVLink v2 규격).
            ushort crc = MavlinkCrc.Compute(b, i + 1, 9 + len);
            crc = MavlinkCrc.Accumulate(extra, crc);
            if (crc != (ushort)(b[i + 10 + len] | (b[i + 11 + len] << 8))) return Outcome.BadCrc;

            // 서명은 검증하지 않는다(시뮬레이터 로컬 입력). 길이만큼만 건너뛴다.
            var payload = new byte[defLen]; // 잘린 끝 0바이트를 0으로 복원
            Array.Copy(b, i + 10, payload, 0, len);
            output.Add(new MavlinkFrame(b[i + 5], b[i + 6], msgId, payload));
            consumed = total;
            return Outcome.Frame;
        }
    }
}
