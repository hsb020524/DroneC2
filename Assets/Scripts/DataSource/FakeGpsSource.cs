using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneC2.DataSource
{
    /// <summary>
    /// 웨이포인트를 일정 속도로 순환하는 가짜 GPS 데이터 소스. 위경도 기준으로만 샘플을 만든다.
    /// 위치는 경과 시간으로 정한다: 이동 거리 = 속도 × 경과 시간을 <see cref="PathFollower"/>가 경로 위 위치로 바꾼다.
    /// </summary>
    public class FakeGpsSource : MonoBehaviour, ITelemetrySource
    {
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
        [SerializeField] UnitKind unitKind = UnitKind.Drone;

        [Tooltip("altitudeM의 기준. 샘플에 실려 가며 바인더가 이 기준대로 해석한다. 시나리오를 쓰면 시나리오 값이 덮어쓴다.")]
        [SerializeField] AltitudeMode altitudeReference = AltitudeMode.Absolute;

        // 시연용 결함 주입. 지속 시간이 0이면 해당 결함을 쓰지 않는다. 시각은 소스 시작 후 경과 시간(초).
        [Header("결함 주입 (시연용)")]
        [SerializeField, Min(0f)] float warningStartSec;
        [SerializeField, Min(0f)] float warningDurationSec;
        [SerializeField, Min(0f)] float dropoutStartSec;
        [SerializeField, Min(0f)] float dropoutDurationSec;

        [Tooltip("지정하면 이 파일에서 unitId와 같은 id의 유닛 설정(경로·고도·속도·종류·결함)을 읽어 아래 값을 덮어쓴다.")]
        [SerializeField] TextAsset scenarioJson;

        // 실제 시설과 무관한 공해상 가상 좌표 주변의 사각 경로 (기본값, 약 670m x 670m)
        [SerializeField] List<GeoPoint> waypoints = new List<GeoPoint>
        {
            new GeoPoint { latitude = 30.003, longitude = -40.003 },
            new GeoPoint { latitude = 30.003, longitude = -39.997 },
            new GeoPoint { latitude = 29.997, longitude = -39.997 },
            new GeoPoint { latitude = 29.997, longitude = -40.003 },
        };

        public event Action<TelemetrySample> SampleReceived;

        PathFollower follower;
        double sampleInterval;
        double sinceLastSample;
        double startTime;

        bool started;

        void Start()
        {
            if (!Begin())
            {
                enabled = false;
                return;
            }
            started = true;
        }

        // 소스 전환으로 다시 켜지면 시나리오를 처음부터 다시 시작한다. (꺼져 있던 동안의 진행이 남지 않게)
        // 처음 켜질 때는 Start가 시작하므로 여기서는 하지 않는다.
        void OnEnable()
        {
            if (started && !Begin())
                enabled = false;
        }

        bool Begin()
        {
            if (scenarioJson != null && !ApplyScenario())
                return false;

            if (waypoints.Count < 2)
            {
                Debug.LogError("FakeGpsSource: 웨이포인트가 2개 이상 필요합니다.", this);
                return false;
            }

            var points = new List<(double latitude, double longitude)>(waypoints.Count);
            foreach (GeoPoint point in waypoints)
                points.Add((point.latitude, point.longitude));

            try
            {
                follower = new PathFollower(points);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"FakeGpsSource: {unitId}: {e.Message}", this);
                return false;
            }

            sampleInterval = 1.0 / sampleRateHz;
            sinceLastSample = 0.0;
            startTime = Time.timeAsDouble;
            Emit();
            return true;
        }

        bool ApplyScenario()
        {
            if (!Scenario.TryParse(scenarioJson.text, out Scenario scenario, out string error))
            {
                Debug.LogError($"FakeGpsSource: {scenarioJson.name}: {error}", this);
                return false;
            }

            if (!scenario.TryGetUnit(unitId, out ScenarioUnit unit))
            {
                Debug.LogError($"FakeGpsSource: {scenarioJson.name}에 유닛 '{unitId}'이(가) 없습니다.", this);
                return false;
            }

            unitKind = unit.Kind;
            altitudeM = unit.altitudeM;
            altitudeReference = unit.AltitudeReference;
            speedMps = unit.speedMps;
            warningStartSec = unit.warning.startSec;
            warningDurationSec = unit.warning.durationSec;
            dropoutStartSec = unit.dropout.startSec;
            dropoutDurationSec = unit.dropout.durationSec;

            waypoints = new List<GeoPoint>(unit.waypoints.Count);
            foreach (ScenarioPoint point in unit.waypoints)
                waypoints.Add(new GeoPoint { latitude = point.latitude, longitude = point.longitude });
            return true;
        }

        void Update()
        {
            sinceLastSample += Time.deltaTime;
            if (sinceLastSample >= sampleInterval)
            {
                sinceLastSample = 0.0;
                Emit();
            }
        }

        void Emit()
        {
            double elapsed = Time.timeAsDouble - startTime;

            // 통신 두절 구간에는 샘플을 보내지 않는다. 두절 판정은 수신 측이 한다.
            if (InWindow(elapsed, dropoutStartSec, dropoutDurationSec))
                return;

            ReportedStatus status = InWindow(elapsed, warningStartSec, warningDurationSec)
                ? ReportedStatus.Warning
                : ReportedStatus.Normal;

            follower.Evaluate(speedMps * elapsed, out double latitude, out double longitude, out double heading);
            SampleReceived?.Invoke(new TelemetrySample(
                unitId, latitude, longitude, altitudeM, altitudeReference, heading, speedMps,
                Time.timeAsDouble, unitKind, status));
        }

        static bool InWindow(double elapsed, double start, double duration)
        {
            return duration > 0.0 && elapsed >= start && elapsed < start + duration;
        }
    }
}
