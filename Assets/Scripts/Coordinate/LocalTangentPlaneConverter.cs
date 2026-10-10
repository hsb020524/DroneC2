using UnityEngine;

namespace DroneC2.Coordinate
{
    /// <summary>
    /// 기준점 주변을 평면(ENU)으로 근사하는 변환기. x=동, y=고도, z=북.
    /// 수 km 범위의 1단계 시연용이며 큰 범위에서는 오차가 커진다.
    /// </summary>
    public class LocalTangentPlaneConverter : MonoBehaviour, ICoordinateConverter
    {
        const double EarthRadiusM = 6378137.0;
        const double DegToRad = System.Math.PI / 180.0;

        // 실제 시설과 무관한 공해상(북대서양) 가상 기준점
        [SerializeField] double originLatitude = 30.0;
        [SerializeField] double originLongitude = -40.0;
        [SerializeField] double originAltitudeM = 0.0;

        public void SetOrigin(double latitude, double longitude, double altitudeM = 0.0)
        {
            originLatitude = latitude;
            originLongitude = longitude;
            originAltitudeM = altitudeM;
        }

        public Vector3 ToWorld(double latitude, double longitude, double altitudeM)
        {
            double east = (longitude - originLongitude) * DegToRad * EarthRadiusM
                          * System.Math.Cos(originLatitude * DegToRad);
            double north = (latitude - originLatitude) * DegToRad * EarthRadiusM;
            double up = altitudeM - originAltitudeM;
            return new Vector3((float)east, (float)up, (float)north);
        }

        public Quaternion HeadingToRotation(double headingDeg)
        {
            return Quaternion.Euler(0f, (float)headingDeg, 0f);
        }
    }
}
