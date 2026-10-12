namespace DroneC2.Core
{
    /// <summary>텔레메트리 고도 값을 어떤 기준으로 해석할지 정한다.</summary>
    public enum AltitudeMode
    {
        /// <summary>고도 값을 그대로 쓴다. (타원체고 또는 변환기 기준 높이)</summary>
        Absolute,

        /// <summary>고도 값을 지표 위 높이(AGL)로 보고, 지표 높이에 더한다.</summary>
        AboveGround,

        /// <summary>고도 값을 무시하고 지표 높이에 붙인다. (지상 유닛)</summary>
        ClampToGround,
    }
}
