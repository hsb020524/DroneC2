namespace DroneC2.Coordinate
{
    /// <summary>
    /// 위경도 지점의 지표 높이(타원체고, m)를 조회한다. 지형 데이터는 비동기로 도착할 수 있어
    /// 값이 아직 없으면 false를 반환하며, 호출자는 마지막 유효 값을 유지해야 한다.
    /// </summary>
    public interface ITerrainHeightSampler
    {
        bool TryGetGroundHeight(double latitude, double longitude, out double ellipsoidHeightM);
    }
}
