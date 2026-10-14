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

        /// <summary><see cref="AltitudeReference"/>에 따라 해석하는 고도(m).</summary>
        public readonly double AltitudeM;

        /// <summary>
        /// 고도의 기준. 소스가 반드시 선언한다. (기본값이 없어, 빠뜨리면 컴파일 오류가 난다.
        /// 상대고도를 해발로 잘못 해석해도 조용히 지나가는 것을 막기 위함이다.)
        /// </summary>
        public readonly AltitudeMode AltitudeReference;

        /// <summary><see cref="AltitudeMode.HomeRelative"/>일 때의 홈 위경도. 그 외에는 NaN.</summary>
        public readonly double HomeLatitude;
        public readonly double HomeLongitude;

        public readonly double HeadingDeg;
        public readonly double SpeedMps;
        public readonly double Time;
        public readonly UnitKind Kind;
        public readonly ReportedStatus Status;

        public TelemetrySample(string unitId, double latitude, double longitude, double altitudeM,
            AltitudeMode altitudeReference, double headingDeg, double speedMps, double time,
            UnitKind kind = UnitKind.Drone, ReportedStatus status = ReportedStatus.Normal,
            double homeLatitude = double.NaN, double homeLongitude = double.NaN)
        {
            UnitId = unitId;
            Latitude = latitude;
            Longitude = longitude;
            AltitudeM = altitudeM;
            AltitudeReference = altitudeReference;
            HomeLatitude = homeLatitude;
            HomeLongitude = homeLongitude;
            HeadingDeg = headingDeg;
            SpeedMps = speedMps;
            Time = time;
            Kind = kind;
            Status = status;
        }

        /// <summary>홈 위경도가 둘 다 유효한 값인지.</summary>
        public bool HasHome => !double.IsNaN(HomeLatitude) && !double.IsNaN(HomeLongitude);
    }
}
