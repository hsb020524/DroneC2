namespace DroneC2.DataSource
{
    /// <summary>
    /// 유닛 상태. 데이터 소스는 Normal·Warning만 보고한다.
    /// Lost(통신 두절)는 샘플이 끊긴 것을 수신 측(UnitRegistry)이 판정해 붙인다.
    /// </summary>
    public enum UnitStatus
    {
        Normal,
        Warning,
        Lost,
    }
}
