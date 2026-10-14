using System;
using System.Globalization;
using DroneC2.Core;
using DroneC2.DataSource;

namespace DroneC2.UI
{
    /// <summary>관제 UI에 표시할 값을 문자열로 바꾼다. Unity에 의존하지 않는 순수 함수 모음.</summary>
    public static class UnitFormat
    {
        static readonly string[] Cardinals = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        public static string Latitude(double deg)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F5}° {1}",
                Math.Abs(deg), deg >= 0.0 ? "N" : "S");
        }

        public static string Longitude(double deg)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F5}° {1}",
                Math.Abs(deg), deg >= 0.0 ? "E" : "W");
        }

        public static string Altitude(double meters)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F0} m", meters);
        }

        public static string Speed(double metersPerSecond)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F1} m/s ({1:F0} km/h)",
                metersPerSecond, metersPerSecond * 3.6);
        }

        /// <summary>방위각을 0~360° 범위로 맞추고 8방위 문자를 붙인다. 예: "090° E"</summary>
        public static string Heading(double deg)
        {
            double normalized = ((deg % 360.0) + 360.0) % 360.0;
            // 359.6°처럼 반올림하면 360이 되는 값은 000으로 되돌린다.
            int rounded = (int)Math.Round(normalized) % 360;
            int index = (int)Math.Round(rounded / 45.0) % 8;
            return string.Format(CultureInfo.InvariantCulture, "{0:000}° {1}", rounded, Cardinals[index]);
        }

        /// <summary>
        /// 고도 모드에 맞는 고도 항목 이름. Absolute 값은 WGS84 타원체고다.
        /// 지오이드 기준 해발 고도와 다르므로 "해발"이라고 쓰지 않는다.
        /// </summary>
        public static string AltitudeLabel(AltitudeMode mode)
        {
            switch (mode)
            {
                case AltitudeMode.AboveGround: return "지표 위 고도";
                case AltitudeMode.ClampToGround: return "지면";
                case AltitudeMode.HomeRelative: return "홈 기준 고도";
                default: return "타원체 고도(WGS84)";
            }
        }

        public static string KindLabel(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Tank: return "전차";
                default: return "드론";
            }
        }

        public static string StatusLabel(UnitStatus status)
        {
            switch (status)
            {
                case UnitStatus.Warning: return "경고";
                case UnitStatus.Lost: return "두절";
                default: return "정상";
            }
        }

        /// <summary>통신 상태 문장. 마지막 수신 이후 경과 시간을 함께 보여 준다.</summary>
        public static string Link(UnitStatus status, double secondsSinceLastReceive)
        {
            double seconds = Math.Max(0.0, secondsSinceLastReceive);
            string text = status == UnitStatus.Lost ? "두절" : "수신 중";
            return string.Format(CultureInfo.InvariantCulture, "{0} · 마지막 수신 {1:F1}초 전", text, seconds);
        }
    }
}
