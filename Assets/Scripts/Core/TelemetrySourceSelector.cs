using DroneC2.DataSource;
using DroneC2.DataSource.Mavlink;
using UnityEngine;

namespace DroneC2.Core
{
    public enum TelemetrySourceMode
    {
        FakeGps,
        Mavlink,
    }

    /// <summary>
    /// 씬 전체의 데이터 소스를 가짜 GPS ↔ MAVLink로 전환한다(FR-015). 설정으로만 바꾸며 표시 계층은 모른다.
    /// 각 바인더와 같은 GameObject에 있는 <see cref="FakeGpsSource"/>·<see cref="MavlinkUnitSource"/> 중
    /// 선택한 쪽만 켜고 바인더에 연결한다. 고도 기준은 샘플이 싣고 오므로 모드를 따로 맞출 필요가 없다.
    /// MAVLink 모드에서만 <see cref="MavlinkReceiver"/>(소켓)를 켠다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class TelemetrySourceSelector : MonoBehaviour
    {
        [SerializeField] TelemetrySourceMode mode = TelemetrySourceMode.FakeGps;
        [Tooltip("MAVLink 모드에서만 켜진다. 씬에서는 비활성으로 두고 이 필드로 연결한다.")]
        [SerializeField] MavlinkReceiver receiver;

        public TelemetrySourceMode Mode => mode;

        /// <summary>상태 표시용. 수신 카운터를 읽는 데만 쓴다.</summary>
        public MavlinkReceiver Receiver => receiver;

        void Awake()
        {
            Apply();
        }

        /// <summary>현재 모드를 씬의 모든 바인더에 적용한다. 시작 시 한 번 호출된다.</summary>
        public void Apply()
        {
            bool useMavlink = mode == TelemetrySourceMode.Mavlink;
            if (useMavlink && receiver == null)
            {
                // 가짜 데이터를 실제 데이터처럼 보여 주는 것이 가장 위험하므로 가짜 GPS로 대체하지 않는다.
                // MAVLink 모드를 유지하면 샘플이 오지 않아 유닛은 수신 없음(두절)으로 남는다.
                Debug.LogError("TelemetrySourceSelector: MAVLink 모드에는 수신기(MavlinkReceiver)가 필요합니다. "
                    + "가짜 GPS로 대체하지 않고 MAVLink 모드를 유지하므로 데이터가 수신되지 않습니다.", this);
            }

            if (receiver != null)
                receiver.enabled = useMavlink;

            UnitTelemetryBinder[] binders = FindObjectsByType<UnitTelemetryBinder>(FindObjectsInactive.Include);
            foreach (UnitTelemetryBinder binder in binders)
            {
                var fake = binder.GetComponent<FakeGpsSource>();
                var mavlink = binder.GetComponent<MavlinkUnitSource>();
                MonoBehaviour chosen = useMavlink ? (MonoBehaviour)mavlink : fake;
                MonoBehaviour other = useMavlink ? (MonoBehaviour)fake : mavlink;

                if (chosen == null)
                {
                    Debug.LogError($"TelemetrySourceSelector: {binder.name}에 "
                        + $"{(useMavlink ? nameof(MavlinkUnitSource) : nameof(FakeGpsSource))}가 없어 전환하지 못했습니다.", binder);
                    continue;
                }

                if (other != null)
                    other.enabled = false;
                chosen.enabled = true;
                binder.SetSource(chosen);
            }
        }
    }
}
