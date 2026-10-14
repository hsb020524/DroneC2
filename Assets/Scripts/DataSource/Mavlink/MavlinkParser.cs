using System;
using System.Collections.Generic;
using System.Threading;

namespace DroneC2.DataSource.Mavlink
{
    /// <summary>
    /// UDP 데이터그램 한 개 안의 MAVLink v1·v2 프레임을 추출한다. 수신 전용이며 송신 기능은 없다.
    /// 우리는 송신하지 않아 상대의 프로토콜 버전을 바꿀 수 없으므로 v1·v2를 모두 받는다.
    /// 외부에서 온 바이트는 신뢰하지 않는다. 알려진 메시지만, CRC가 맞는 것만 통과시키고
    /// 나머지는 한 바이트씩 건너뛰며 다음 시작 바이트를 찾는다.
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

        long v1Frames;
        long v2Frames;
        long rejected;

        /// <summary>통과한 v1 프레임 수.</summary>
        public long V1Frames => Interlocked.Read(ref v1Frames);

        /// <summary>통과한 v2 프레임 수.</summary>
        public long V2Frames => Interlocked.Read(ref v2Frames);

        /// <summary>시작 바이트는 맞았지만 길이·CRC·메시지 종류 검사에서 버린 후보 수(디버깅용).</summary>
        public long Rejected => Interlocked.Read(ref rejected);

        /// <summary>해석 가능한 메시지의 (CRC_EXTRA, 페이로드 정의 길이). 모르는 메시지는 검증이 불가능하므로 버린다.</summary>
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
                int consumed = 0;
                if (stx == StxV2)
                {
                    consumed = TryParseV2(buffer, i, end, output);
                    if (consumed > 0)
                        Interlocked.Increment(ref v2Frames);
                    else
                        Interlocked.Increment(ref rejected);
                }
                else if (stx == StxV1)
                {
                    consumed = TryParseV1(buffer, i, end, output);
                    if (consumed > 0)
                        Interlocked.Increment(ref v1Frames);
                    else
                        Interlocked.Increment(ref rejected);
                }

                if (consumed > 0)
                {
                    found++;
                    i += consumed;
                }
                else
                {
                    i++;
                }
            }
            return found;
        }

        // 헤더 6 + 페이로드 + CRC 2
        static int TryParseV1(byte[] b, int i, int end, List<MavlinkFrame> output)
        {
            if (end - i < 8) return 0;
            int len = b[i + 1];
            int total = 6 + len + 2;
            if (end - i < total) return 0;

            uint msgId = b[i + 5];
            if (!TryGetDefinition(msgId, out byte extra, out int defLen) || len != defLen) return 0;

            ushort crc = MavlinkCrc.Compute(b, i + 1, 5 + len);
            crc = MavlinkCrc.Accumulate(extra, crc);
            if (crc != (ushort)(b[i + 6 + len] | (b[i + 7 + len] << 8))) return 0;

            var payload = new byte[defLen];
            Array.Copy(b, i + 6, payload, 0, len);
            output.Add(new MavlinkFrame(b[i + 3], b[i + 4], msgId, payload));
            return total;
        }

        // 헤더 10 + 페이로드 + CRC 2 (+ 서명 13)
        static int TryParseV2(byte[] b, int i, int end, List<MavlinkFrame> output)
        {
            if (end - i < 12) return 0;
            int len = b[i + 1];
            byte incompat = b[i + 2];
            if ((incompat & ~IncompatSigned) != 0) return 0; // 이해할 수 없는 플래그는 버린다
            int total = 10 + len + 2 + ((incompat & IncompatSigned) != 0 ? SignatureLength : 0);
            if (end - i < total) return 0;

            uint msgId = (uint)(b[i + 7] | (b[i + 8] << 8) | (b[i + 9] << 16));
            if (!TryGetDefinition(msgId, out byte extra, out int defLen) || len > defLen) return 0;

            // CRC는 잘려서 온 len 바이트 기준이다(MAVLink v2 규격).
            ushort crc = MavlinkCrc.Compute(b, i + 1, 9 + len);
            crc = MavlinkCrc.Accumulate(extra, crc);
            if (crc != (ushort)(b[i + 10 + len] | (b[i + 11 + len] << 8))) return 0;

            // 서명은 검증하지 않는다(시뮬레이터 로컬 입력). 길이만큼만 건너뛴다.
            var payload = new byte[defLen]; // 잘린 끝 0바이트를 0으로 복원
            Array.Copy(b, i + 10, payload, 0, len);
            output.Add(new MavlinkFrame(b[i + 5], b[i + 6], msgId, payload));
            return total;
        }
    }
}
