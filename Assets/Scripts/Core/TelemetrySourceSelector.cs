using System.Collections.Generic;
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
        [Tooltip("소스를 전환할 때 이전 소스의 유닛을 비울 레지스트리. 없으면 목록에 이전 유닛이 남는다.")]
        [SerializeField] UnitRegistry registry;

        TelemetrySourceMode appliedMode;
        bool applied;

        public TelemetrySourceMode Mode => mode;

        /// <summary>상태 표시용. 수신 카운터를 읽는 데만 쓴다.</summary>
        public MavlinkReceiver Receiver => receiver;

        void Awake()
        {
            Apply();
        }

        /// <summary>
        /// Play 중에 소스를 전환한다(FR-015). 이전 소스의 유닛·표시·경로선·선택을 모두 비우므로
        /// 가짜와 실제 데이터가 섞여 보이지 않는다. 이미 그 모드면 아무것도 하지 않는다.
        /// </summary>
        public void SetMode(TelemetrySourceMode newMode)
        {
            mode = newMode;
            if (applied && mode != appliedMode)
                Apply(resetUnits: true);
        }

#if UNITY_EDITOR
        // Play 중 인스펙터에서 모드를 바꾸면 전환한다. OnValidate 안에서 객체를 바꾸지 않도록 한 박자 늦춘다.
        // delayCall은 에디터 전용이라 빌드에는 포함하지 않는다.
        void OnValidate()
        {
            if (!Application.isPlaying || !applied || mode == appliedMode)
                return;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && applied && mode != appliedMode)
                    Apply(resetUnits: true);
            };
        }
#endif

        /// <summary>
        /// 현재 모드를 씬의 모든 바인더에 적용한다. 시작 시에는 초기화 없이 호출되고,
        /// 전환 시(<paramref name="resetUnits"/>)에는 이전 소스의 유닛을 비운다.
        /// </summary>
        public void Apply(bool resetUnits = false)
        {
            bool useMavlink = mode == TelemetrySourceMode.Mavlink;
            applied = true;
            appliedMode = mode;
            if (useMavlink && receiver == null)
            {
                // 가짜 데이터를 실제 데이터처럼 보여 주는 것이 가장 위험하므로 가짜 GPS로 대체하지 않는다.
                // MAVLink 모드를 유지하면 샘플이 오지 않아 유닛은 수신 없음(두절)으로 남는다.
                Debug.LogError("TelemetrySourceSelector: MAVLink 모드에는 수신기(MavlinkReceiver)가 필요합니다. "
                    + "가짜 GPS로 대체하지 않고 MAVLink 모드를 유지하므로 데이터가 수신되지 않습니다.", this);
            }

            // 1단계: 이전 소스를 끊고 유닛을 비운다. 새 소스는 아직 켜지 않는다. 가짜 GPS는 켜질 때 첫 샘플을
            // 바로 내보내므로, 초기화가 그 샘플을 지우지 않으려면 초기화가 먼저여야 한다.
            var switched = new List<(UnitTelemetryBinder binder, MonoBehaviour chosen)>();
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
                binder.SetSource(chosen);
                if (resetUnits)
                    binder.ResetUnit();
                switched.Add((binder, chosen));
            }

            if (resetUnits)
            {
                if (registry != null)
                    registry.ResetUnits();
                else
                    Debug.LogWarning("TelemetrySourceSelector: 레지스트리가 없어 이전 소스의 유닛이 목록에 남을 수 있습니다.", this);
            }

            // 2단계: 새 소스를 켠다.
            foreach ((UnitTelemetryBinder binder, MonoBehaviour chosen) in switched)
                chosen.enabled = true;

            if (receiver != null)
                receiver.enabled = useMavlink;
        }
    }
}
