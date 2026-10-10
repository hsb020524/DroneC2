namespace DroneC2.DataSource
{
    /// <summary>
    /// 데이터 소스가 내보내는 유닛 상태 한 건. 위경도(WGS84) 기준이며 Unity 좌표를 포함하지 않는다.
    /// </summary>
    public readonly struct TelemetrySample
    {
        public readonly string UnitId;
        public readonly double Latitude;
        public readonly double Longitude;
        public readonly double AltitudeM;
        public readonly double HeadingDeg;
        public readonly double SpeedMps;
        public readonly double Time;

        public TelemetrySample(string unitId, double latitude, double longitude, double altitudeM,
            double headingDeg, double speedMps, double time)
        {
            UnitId = unitId;
            Latitude = latitude;
            Longitude = longitude;
            AltitudeM = altitudeM;
            HeadingDeg = headingDeg;
            SpeedMps = speedMps;
            Time = time;
        }
    }
}
