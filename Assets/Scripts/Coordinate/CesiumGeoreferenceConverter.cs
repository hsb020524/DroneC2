using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;

namespace DroneC2.Coordinate
{
    /// <summary>
    /// Cesium 지오레퍼런스 기준으로 WGS84 위경도·타원체고를 Unity 월드 좌표로 변환한다.
    /// 월드 축은 지오레퍼런스 원점에서 x=동, y=위, z=북이다. 고도는 타원체고(ellipsoid height)로 해석한다.
    /// </summary>
    public class CesiumGeoreferenceConverter : MonoBehaviour, ICoordinateConverter
    {
        [SerializeField] CesiumGeoreference georeference;

        public void SetGeoreference(CesiumGeoreference value)
        {
            georeference = value;
        }

        public Vector3 ToWorld(double latitude, double longitude, double altitudeM)
        {
            CesiumGeoreference geo = ResolveGeoreference();
            double3 ecef = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(
                new double3(longitude, latitude, altitudeM));
            double3 unity = geo.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
            return new Vector3((float)unity.x, (float)unity.y, (float)unity.z);
        }

        /// <summary>
        /// 위치를 받지 않는 인터페이스라 원점 기준 ENU 회전을 쓴다.
        /// 원점에서 수 km 이내라면 지구 곡률에 의한 기울기(5km에서 약 0.05°)는 무시할 수 있다.
        /// </summary>
        public Quaternion HeadingToRotation(double headingDeg)
        {
            return Quaternion.Euler(0f, (float)headingDeg, 0f);
        }

        CesiumGeoreference ResolveGeoreference()
        {
            if (georeference == null)
                georeference = GetComponentInParent<CesiumGeoreference>();
            if (georeference == null)
                georeference = FindAnyObjectByType<CesiumGeoreference>();
            if (georeference == null)
                throw new System.InvalidOperationException(
                    "CesiumGeoreferenceConverter: 씬에서 CesiumGeoreference를 찾을 수 없습니다.");
            return georeference;
        }
    }
}
