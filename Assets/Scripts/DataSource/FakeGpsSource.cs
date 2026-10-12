using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneC2.DataSource
{
    /// <summary>
    /// 웨이포인트를 일정 속도로 순환하는 가짜 GPS 데이터 소스. 위경도 기준으로만 샘플을 만든다.
    /// </summary>
    public class FakeGpsSource : MonoBehaviour, ITelemetrySource
    {
        const double EarthRadiusM = 6378137.0;
        const double DegToRad = Math.PI / 180.0;
        const double RadToDeg = 180.0 / Math.PI;

        [Serializable]
        public struct GeoPoint
        {
            public double latitude;
            public double longitude;
        }

        [SerializeField] string unitId = "UAV-01";
        [SerializeField] double altitudeM = 50.0;
        [SerializeField, Min(0.1f)] float speedMps = 25f;
        [SerializeField, Min(1f)] float sampleRateHz = 10f;
        [SerializeField, Min(0.1f)] float arrivalRadiusM = 5f;
        [SerializeField] UnitKind unitKind = UnitKind.Drone;

        // 시연용 결함 주입. 지속 시간이 0이면 해당 결함을 쓰지 않는다. 시각은 소스 시작 후 경과 시간(초).
        [Header("결함 주입 (시연용)")]
        [SerializeField, Min(0f)] float warningStartSec;
        [SerializeField, Min(0f)] float warningDurationSec;
        [SerializeField, Min(0f)] float dropoutStartSec;
        [SerializeField, Min(0f)] float dropoutDurationSec;

        // 실제 시설과 무관한 공해상 가상 좌표 주변의 사각 경로 (기본값, 약 670m x 670m)
        [SerializeField] List<GeoPoint> waypoints = new List<GeoPoint>
        {
            new GeoPoint { latitude = 30.003, longitude = -40.003 },
            new GeoPoint { latitude = 30.003, longitude = -39.997 },
            new GeoPoint { latitude = 29.997, longitude = -39.997 },
            new GeoPoint { latitude = 29.997, longitude = -40.003 },
        };

        public event Action<TelemetrySample> SampleReceived;

        double latitude;
        double longitude;
        double headingDeg;
        int targetIndex;
        double sampleInterval;
        double sinceLastSample;
        double startTime;

        void Start()
        {
            if (waypoints.Count < 2)
            {
                Debug.LogError("FakeGpsSource: 웨이포인트가 2개 이상 필요합니다.", this);
                enabled = false;
                return;
            }

            latitude = waypoints[0].latitude;
            longitude = waypoints[0].longitude;
            targetIndex = 1;
            headingDeg = BearingTo(waypoints[targetIndex]);
            sampleInterval = 1.0 / sampleRateHz;
            startTime = Time.timeAsDouble;
            Emit();
        }

        void Update()
        {
            Advance(Time.deltaTime);

            sinceLastSample += Time.deltaTime;
            if (sinceLastSample >= sampleInterval)
            {
                sinceLastSample = 0.0;
                Emit();
            }
        }

        void Advance(double dt)
        {
            double remaining = speedMps * dt;
            // 한 프레임에 웨이포인트를 지나칠 수 있어 이동 거리가 소진될 때까지 반복한다.
            // 반복 횟수는 웨이포인트 수로 제한해 도착 반경이 겹칠 때의 무한 루프를 막는다.
            int guard = waypoints.Count;
            while (remaining > 0.0 && guard-- > 0)
            {
                ToLocalMeters(waypoints[targetIndex], out double east, out double north);
                double distance = Math.Sqrt(east * east + north * north);

                if (distance <= arrivalRadiusM)
                {
                    targetIndex = (targetIndex + 1) % waypoints.Count;
                    headingDeg = BearingTo(waypoints[targetIndex]);
                    continue;
                }

                headingDeg = Math.Atan2(east, north) * RadToDeg;
                double step = Math.Min(remaining, distance);
                double cosLat = Math.Cos(latitude * DegToRad);
                latitude += step * (north / distance) / (EarthRadiusM * DegToRad);
                longitude += step * (east / distance) / (EarthRadiusM * DegToRad * cosLat);
                remaining -= step;
            }
        }

        void ToLocalMeters(GeoPoint target, out double east, out double north)
        {
            double cosLat = Math.Cos(latitude * DegToRad);
            east = (target.longitude - longitude) * DegToRad * EarthRadiusM * cosLat;
            north = (target.latitude - latitude) * DegToRad * EarthRadiusM;
        }

        double BearingTo(GeoPoint target)
        {
            ToLocalMeters(target, out double east, out double north);
            double bearing = Math.Atan2(east, north) * RadToDeg;
            return (bearing + 360.0) % 360.0;
        }

        void Emit()
        {
            double elapsed = Time.timeAsDouble - startTime;

            // 통신 두절 구간에는 샘플을 보내지 않는다. 두절 판정은 수신 측이 한다.
            if (InWindow(elapsed, dropoutStartSec, dropoutDurationSec))
                return;

            UnitStatus status = InWindow(elapsed, warningStartSec, warningDurationSec)
                ? UnitStatus.Warning
                : UnitStatus.Normal;

            double heading = (headingDeg + 360.0) % 360.0;
            SampleReceived?.Invoke(new TelemetrySample(
                unitId, latitude, longitude, altitudeM, heading, speedMps, Time.timeAsDouble,
                unitKind, status));
        }

        static bool InWindow(double elapsed, double start, double duration)
        {
            return duration > 0.0 && elapsed >= start && elapsed < start + duration;
        }
    }
}
