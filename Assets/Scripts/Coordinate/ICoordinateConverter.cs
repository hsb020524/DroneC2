using UnityEngine;

namespace DroneC2.Coordinate
{
    /// <summary>
    /// WGS84 위경도·고도를 Unity 월드 좌표로 변환한다. 평면 로컬 변환, Cesium 지오레퍼런스 등으로 교체할 수 있다.
    /// </summary>
    public interface ICoordinateConverter
    {
        Vector3 ToWorld(double latitude, double longitude, double altitudeM);

        /// <summary>북 기준 시계방향 방위각(도)을 월드 회전으로 변환한다.</summary>
        Quaternion HeadingToRotation(double headingDeg);
    }
}
