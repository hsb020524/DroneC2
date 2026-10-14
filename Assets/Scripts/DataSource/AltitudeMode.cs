namespace DroneC2.DataSource
{
    /// <summary>
    /// 텔레메트리 고도 값을 어떤 기준으로 해석할지 정한다. 샘플이 직접 싣고 오며,
    /// 데이터 소스가 자기 고도의 의미를 선언한다. 숫자 값은 저장된 에셋과 호환되므로 바꾸지 않는다.
    /// </summary>
    public enum AltitudeMode
    {
        /// <summary>고도 값을 그대로 쓴다. (타원체고 또는 변환기 기준 높이)</summary>
        Absolute = 0,

        /// <summary>고도 값을 지표 위 높이(AGL)로 보고, 지표 높이에 더한다.</summary>
        AboveGround = 1,

        /// <summary>고도 값을 무시하고 지표 높이에 붙인다. (지상 유닛)</summary>
        ClampToGround = 2,

        /// <summary>
        /// 고도 값을 홈 기준 상대고도로 보고, 샘플이 싣고 온 홈 위경도의 지표 높이에 더한다.
        /// (MAVLink GLOBAL_POSITION_INT.relative_alt)
        /// </summary>
        HomeRelative = 3,
    }
}
