namespace DroneC2.DataSource
{
    /// <summary>
    /// 데이터 소스가 보고할 수 있는 상태. 통신 두절(Lost)은 소스가 보고할 수 없고
    /// 샘플이 끊긴 것을 수신 측(UnitStateStore)이 판정해 <see cref="UnitStatus"/>로 붙인다.
    /// </summary>
    public enum ReportedStatus
    {
        Normal,
        Warning,
    }
}
