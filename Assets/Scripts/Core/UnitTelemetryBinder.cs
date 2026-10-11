using DroneC2.Coordinate;
using DroneC2.DataSource;
using DroneC2.View;
using UnityEngine;

namespace DroneC2.Core
{
    /// <summary>
    /// 데이터 소스 → 좌표 변환 → 표시를 연결하는 유일한 지점.
    /// 인터페이스 필드는 직렬화되지 않으므로 MonoBehaviour로 받고 구현 여부를 검사한다.
    /// </summary>
    public class UnitTelemetryBinder : MonoBehaviour
    {
        [SerializeField] MonoBehaviour sourceBehaviour;
        [SerializeField] MonoBehaviour converterBehaviour;
        [SerializeField] DroneView view;

        ITelemetrySource source;
        ICoordinateConverter converter;

        void OnValidate()
        {
            if (sourceBehaviour != null && !(sourceBehaviour is ITelemetrySource))
            {
                Debug.LogError($"{sourceBehaviour.name}은(는) ITelemetrySource를 구현하지 않습니다.", this);
                sourceBehaviour = null;
            }

            if (converterBehaviour != null && !(converterBehaviour is ICoordinateConverter))
            {
                Debug.LogError($"{converterBehaviour.name}은(는) ICoordinateConverter를 구현하지 않습니다.", this);
                converterBehaviour = null;
            }
        }

        void OnEnable()
        {
            source = sourceBehaviour as ITelemetrySource;
            converter = converterBehaviour as ICoordinateConverter;

            if (source == null || converter == null || view == null)
            {
                Debug.LogError("UnitTelemetryBinder: 소스, 변환기, 뷰를 모두 지정해야 합니다.", this);
                enabled = false;
                return;
            }

            source.SampleReceived += OnSample;
        }

        void OnDisable()
        {
            if (source != null)
                source.SampleReceived -= OnSample;
        }

        void OnSample(TelemetrySample sample)
        {
            Vector3 position = converter.ToWorld(sample.Latitude, sample.Longitude, sample.AltitudeM);
            Quaternion rotation = converter.HeadingToRotation(sample.HeadingDeg);
            view.SetTarget(position, rotation);
        }
    }
}
