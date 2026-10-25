using DroneC2.Core;
using DroneC2.DataSource;
using UnityEngine;
using UnityEngine.UIElements;

namespace DroneC2.UI
{
    /// <summary>
    /// 선택한 유닛의 상태(위치, 고도, 속도, 방위, 통신 상태)를 상세 패널에 표시한다.
    /// 선택이 없으면 패널을 숨긴다. 레지스트리만 읽고 데이터 소스는 알지 못한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class UnitDetailPresenter : MonoBehaviour
    {
        const string HiddenClass = "hidden";

        [SerializeField] UnitRegistry registry;

        UIDocument document;
        UnitStateStore store;
        VisualElement panel;
        Label title;
        Label kind;
        Label status;
        Label link;
        Label latitude;
        Label longitude;
        Label altitudeKey;
        Label altitude;
        Label speed;
        Label heading;
        bool started;

        void Start()
        {
            started = true;
            Bind();
        }

        void OnEnable()
        {
            if (started)
                Bind();
        }

        void OnDisable()
        {
            Unbind();
        }

        void Update()
        {
            if (store == null)
                return;

            UnitState state = store.Selected;
            if (state == null)
                return;

            Refresh(state);
        }

        void Bind()
        {
            if (store != null)
                return;

            if (registry == null)
            {
                Debug.LogError("UnitDetailPresenter: 레지스트리를 지정해야 합니다.", this);
                return;
            }

            document = GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;
            panel = root?.Q<VisualElement>("detail-panel");
            if (panel == null)
            {
                Debug.LogError("UnitDetailPresenter: UXML에서 detail-panel을 찾을 수 없습니다.", this);
                return;
            }

            title = root.Q<Label>("detail-title");
            kind = root.Q<Label>("detail-kind");
            status = root.Q<Label>("detail-status");
            link = root.Q<Label>("detail-link");
            latitude = root.Q<Label>("detail-lat");
            longitude = root.Q<Label>("detail-lon");
            altitudeKey = root.Q<Label>("detail-alt-key");
            altitude = root.Q<Label>("detail-alt");
            speed = root.Q<Label>("detail-speed");
            heading = root.Q<Label>("detail-heading");

            store = registry.Store;
            store.SelectionChanged += OnSelectionChanged;
            OnSelectionChanged(store.SelectedId);
        }

        void Unbind()
        {
            if (store == null)
                return;

            store.SelectionChanged -= OnSelectionChanged;
            store = null;
            panel?.AddToClassList(HiddenClass);
        }

        void OnSelectionChanged(string selectedId)
        {
            UnitState state = store.Selected;
            panel.EnableInClassList(HiddenClass, state == null);
            if (state == null)
                return;

            title.text = state.Id;
            kind.text = UnitFormat.KindLabel(state.Kind);
            Refresh(state);
        }

        void Refresh(UnitState state)
        {
            ApplyStatus(state.Status);
            link.text = UnitFormat.Link(state.Status, Time.timeAsDouble - state.LastReceivedTime);
            latitude.text = UnitFormat.Latitude(state.Latitude);
            longitude.text = UnitFormat.Longitude(state.Longitude);
            altitudeKey.text = UnitFormat.AltitudeLabel(state.AltitudeMode);
            altitude.text = UnitFormat.Altitude(state.AltitudeM);
            speed.text = UnitFormat.Speed(state.SpeedMps);
            heading.text = UnitFormat.Heading(state.HeadingDeg);
        }

        void ApplyStatus(UnitStatus value)
        {
            ApplyStatusTo(status, value);
        }

        /// <summary>
        /// 라벨이 실제로 가진 텍스트를 기준으로 갱신한다. 이전에 적용한 값을 따로 기억하면
        /// UXML 재적용 등으로 라벨이 외부에서 초기화됐을 때 복구되지 않기 때문이다. 쓴 경우 true.
        /// </summary>
        public static bool ApplyStatusTo(Label label, UnitStatus value)
        {
            string text = UnitFormat.StatusLabel(value);
            if (label.text == text)
                return false;

            label.text = text;
            label.EnableInClassList("status-normal", value == UnitStatus.Normal);
            label.EnableInClassList("status-warning", value == UnitStatus.Warning);
            label.EnableInClassList("status-lost", value == UnitStatus.Lost);
            return true;
        }
    }
}
