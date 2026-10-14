using System;

namespace DroneC2.DataSource.Mavlink
{
    /// <summary>HEARTBEAT에서 쓰는 값만.</summary>
    public readonly struct MavlinkHeartbeat
    {
        public readonly byte MavType;
        public readonly byte SystemStatus;

        public MavlinkHeartbeat(byte mavType, byte systemStatus)
        {
            MavType = mavType;
            SystemStatus = systemStatus;
        }

        // MAV_STATE: 5 = CRITICAL, 6 = EMERGENCY
        public bool IsWarning => SystemStatus == 5 || SystemStatus == 6;

        /// <summary>MAV_TYPE → 유닛 종류. 매핑하지 않는 종류는 false.</summary>
        public bool TryGetKind(out UnitKind kind)
        {
            switch (MavType)
            {
                case 1:  // FIXED_WING
                case 2:  // QUADROTOR
                case 3:  // COAXIAL
                case 4:  // HELICOPTER
                case 13: // HEXAROTOR
                case 14: // OCTOROTOR
                case 15: // TRICOPTER
                case 19: // VTOL_DUOROTOR
                case 20: // VTOL_QUADROTOR
                case 21: // VTOL_TILTROTOR
                    kind = UnitKind.Drone;
                    return true;
                case 10: // GROUND_ROVER
                    kind = UnitKind.Tank;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }
    }

    /// <summary>GLOBAL_POSITION_INT를 도·미터·m/s 단위로 변환한 값.</summary>
    public readonly struct MavlinkGlobalPosition
    {
        public readonly double LatitudeDeg;
        public readonly double LongitudeDeg;

        /// <summary>해수면(MSL) 고도(m). 지형 기준 타원체고와 다르므로 표시에 직접 쓰지 않는다.</summary>
        public readonly double MslAltitudeM;

        /// <summary>홈 기준 상대고도(m).</summary>
        public readonly double RelativeAltitudeM;

        public readonly double NorthSpeedMps;
        public readonly double EastSpeedMps;

        /// <summary>hdg가 65535(알 수 없음)가 아니면 true. 0은 정북이다.</summary>
        public readonly bool HeadingKnown;

        public readonly double HeadingDeg;

        public MavlinkGlobalPosition(double lat, double lon, double mslAlt, double relAlt,
            double vNorth, double vEast, bool headingKnown, double headingDeg)
        {
            LatitudeDeg = lat;
            LongitudeDeg = lon;
            MslAltitudeM = mslAlt;
            RelativeAltitudeM = relAlt;
            NorthSpeedMps = vNorth;
            EastSpeedMps = vEast;
            HeadingKnown = headingKnown;
            HeadingDeg = headingDeg;
        }

        public double GroundSpeedMps => Math.Sqrt(NorthSpeedMps * NorthSpeedMps + EastSpeedMps * EastSpeedMps);
    }

    public static class MavlinkDecoder
    {
        public static bool TryDecodeHeartbeat(in MavlinkFrame frame, out MavlinkHeartbeat result)
        {
            result = default;
            if (frame.MessageId != MavlinkParser.MsgHeartbeat || frame.Payload == null || frame.Payload.Length < 9)
                return false;
            // custom_mode(u32), type, autopilot, base_mode, system_status, mavlink_version
            result = new MavlinkHeartbeat(frame.Payload[4], frame.Payload[7]);
            return true;
        }

        public static bool TryDecodeGlobalPosition(in MavlinkFrame frame, out MavlinkGlobalPosition result)
        {
            result = default;
            if (frame.MessageId != MavlinkParser.MsgGlobalPositionInt || frame.Payload == null || frame.Payload.Length < 28)
                return false;

            byte[] p = frame.Payload;
            // time_boot_ms(u32) lat lon alt relative_alt(i32) vx vy vz(i16) hdg(u16)
            double lat = BitConverter.ToInt32(p, 4) / 1e7;
            double lon = BitConverter.ToInt32(p, 8) / 1e7;
            if (lat < -90.0 || lat > 90.0 || lon < -180.0 || lon > 180.0)
                return false;

            double alt = BitConverter.ToInt32(p, 12) / 1000.0;
            double rel = BitConverter.ToInt32(p, 16) / 1000.0;
            double vx = BitConverter.ToInt16(p, 20) / 100.0;
            double vy = BitConverter.ToInt16(p, 22) / 100.0;
            ushort hdg = BitConverter.ToUInt16(p, 26);
            bool known = hdg != ushort.MaxValue;
            result = new MavlinkGlobalPosition(lat, lon, alt, rel, vx, vy, known, known ? hdg / 100.0 : 0.0);
            return true;
        }
    }
}
