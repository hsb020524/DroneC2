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
        [SerializeField] AltitudeMode altitudeMode = AltitudeMode.Absolute;
        [Tooltip("비워 두면 레지스트리에 보고하지 않는다. (단일 유닛 씬)")]
        [SerializeField] UnitRegistry registry;
        [Tooltip("비워 두면 마커를 갱신하지 않는다. 레지스트리가 있어야 상태가 반영된다.")]
        [SerializeField] UnitMarker marker;
        [Tooltip("비워 두면 카메라 영상을 만들지 않는다. 선택된 유닛만 렌더한다.")]
        [SerializeField] UnitCameraRig cameraRig;

        ITelemetrySource source;
        ICoordinateConverter converter;
        AltitudeResolver altitudeResolver;
        UnitStateStore subscribedStore;
        string unitId;

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

            // 변환기가 지표 높이 조회를 지원하면 고도 모드에 사용한다. (지원하지 않으면 평면으로 간주)
            altitudeResolver = new AltitudeResolver(converterBehaviour as ITerrainHeightSampler);
            source.SampleReceived += OnSample;

            if (registry != null && (marker != null || cameraRig != null))
            {
                subscribedStore = registry.Store;
                subscribedStore.UnitAdded += OnUnitState;
                subscribedStore.StatusChanged += OnUnitState;
                subscribedStore.SelectionChanged += OnSelectionChanged;
                subscribedStore.PinnedChanged += OnPinnedChanged;
            }
        }

        void OnDisable()
        {
            if (source != null)
                source.SampleReceived -= OnSample;

            if (subscribedStore != null)
            {
                subscribedStore.UnitAdded -= OnUnitState;
                subscribedStore.StatusChanged -= OnUnitState;
                subscribedStore.SelectionChanged -= OnSelectionChanged;
                subscribedStore.PinnedChanged -= OnPinnedChanged;
                subscribedStore = null;
            }
        }

        void OnSample(TelemetrySample sample)
        {
            // 지표 높이를 아직 얻지 못했으면 이 샘플은 건너뛴다. (지형 속에 배치하지 않기 위해)
            if (!altitudeResolver.TryResolve(altitudeMode, sample.Latitude, sample.Longitude,
                    sample.AltitudeM, out double altitudeM))
                return;

            Vector3 position = converter.ToWorld(sample.Latitude, sample.Longitude, altitudeM);
            Quaternion rotation = converter.HeadingToRotation(sample.HeadingDeg);
            view.SetTarget(position, rotation);

            if (registry == null)
                return;

            // 이벤트 핸들러가 자기 유닛만 걸러내도록 보고 전에 id를 확정한다.
            if (unitId != sample.UnitId)
            {
                unitId = sample.UnitId;
                registry.RegisterView(unitId, view.transform);
                if (cameraRig != null)
                    registry.RegisterCameraTexture(unitId, cameraRig.Texture);
            }

            registry.Store.Report(sample, position, Time.timeAsDouble, altitudeMode);
        }

        void OnUnitState(UnitState state)
        {
            if (state.Id != unitId)
                return;

            marker?.Apply(state.Kind, state.Status);
            UpdateCamera(state.Status);
        }

        void OnSelectionChanged(string selectedId)
        {
            marker?.SetSelected(unitId != null && selectedId == unitId);
            RefreshCamera();
        }

        void OnPinnedChanged(string pinnedId)
        {
            RefreshCamera();
        }

        void RefreshCamera()
        {
            if (cameraRig == null)
                return;

            if (unitId != null && subscribedStore.TryGet(unitId, out UnitState state))
                UpdateCamera(state.Status);
            else
                cameraRig.SetRendering(false);
        }

        void UpdateCamera(UnitStatus status)
        {
            if (cameraRig == null)
                return;

            bool shown = unitId != null
                && (subscribedStore.SelectedId == unitId || subscribedStore.PinnedId == unitId);
            cameraRig.SetRendering(UnitCameraPolicy.ShouldRender(shown, status));
        }
    }
}
