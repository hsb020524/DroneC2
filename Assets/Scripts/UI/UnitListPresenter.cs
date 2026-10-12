using System.Collections.Generic;
using DroneC2.Core;
using DroneC2.DataSource;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DroneC2.UI
{
    /// <summary>
    /// 유닛 목록을 그리고 선택을 레지스트리에 전달한다. 데이터 소스는 알지 못하고 레지스트리만 본다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class UnitListPresenter : MonoBehaviour
    {
        const string RowClass = "unit-row";
        const string SelectedClass = "selected";

        [SerializeField] UnitRegistry registry;

        sealed class Row
        {
            public VisualElement Root;
            public VisualElement Dot;
            public Label Kind;
            public Label Id;
            public Label Status;
            public UnitStatus AppliedStatus;
        }

        readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
        UIDocument document;
        UnitStateStore store;
        ScrollView list;
        Label title;
        Button overviewButton;
        bool started;

        void Start()
        {
            // UIDocument가 UXML을 복제하는 시점이 OnEnable 순서에 따라 달라질 수 있어 Start에서 연결한다.
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
            // Esc: 선택 해제(전체 보기)
            if (store != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                store.ClearSelection();
        }

        void Bind()
        {
            if (store != null)
                return;

            if (registry == null)
            {
                Debug.LogError("UnitListPresenter: 레지스트리를 지정해야 합니다.", this);
                return;
            }

            document = GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;
            list = root?.Q<ScrollView>("unit-list");
            title = root?.Q<Label>("list-title");
            overviewButton = root?.Q<Button>("overview-button");
            if (list == null)
            {
                Debug.LogError("UnitListPresenter: UXML에서 unit-list를 찾을 수 없습니다.", this);
                return;
            }

            store = registry.Store;
            store.UnitAdded += OnUnitAdded;
            store.StatusChanged += OnStatusChanged;
            store.SelectionChanged += OnSelectionChanged;

            if (overviewButton != null)
                overviewButton.clicked += OnOverviewClicked;

            Rebuild();
        }

        void Unbind()
        {
            if (store == null)
                return;

            store.UnitAdded -= OnUnitAdded;
            store.StatusChanged -= OnStatusChanged;
            store.SelectionChanged -= OnSelectionChanged;
            store = null;

            if (overviewButton != null)
                overviewButton.clicked -= OnOverviewClicked;

            rows.Clear();
            list?.Clear();
        }

        void OnOverviewClicked()
        {
            store?.ClearSelection();
        }

        void OnUnitAdded(UnitState state)
        {
            Rebuild();
        }

        void OnStatusChanged(UnitState state)
        {
            if (rows.TryGetValue(state.Id, out Row row))
                ApplyStatus(row, state.Status);
        }

        void OnSelectionChanged(string selectedId)
        {
            foreach (KeyValuePair<string, Row> pair in rows)
                pair.Value.Root.EnableInClassList(SelectedClass, pair.Key == selectedId);
        }

        /// <summary>유닛 수가 바뀔 때마다 id 순으로 다시 만든다. (유닛 수가 적어 비용은 무시할 수 있다)</summary>
        void Rebuild()
        {
            list.Clear();
            rows.Clear();

            var ordered = new List<UnitState>(store.Units);
            ordered.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            foreach (UnitState state in ordered)
            {
                Row row = CreateRow(state);
                rows.Add(state.Id, row);
                list.Add(row.Root);
            }

            if (title != null)
                title.text = $"유닛 목록 ({ordered.Count})";

            OnSelectionChanged(store.SelectedId);
        }

        Row CreateRow(UnitState state)
        {
            var row = new Row
            {
                Root = new VisualElement(),
                Dot = new VisualElement(),
                Kind = new Label(UnitFormat.KindLabel(state.Kind)),
                Id = new Label(state.Id),
                Status = new Label(),
            };

            row.Root.AddToClassList(RowClass);
            row.Dot.AddToClassList("status-dot");
            row.Kind.AddToClassList("kind-badge");
            row.Id.AddToClassList("unit-id");
            row.Status.AddToClassList("unit-status");

            row.Root.Add(row.Dot);
            row.Root.Add(row.Kind);
            row.Root.Add(row.Id);
            row.Root.Add(row.Status);

            string id = state.Id;
            row.Root.RegisterCallback<ClickEvent>(_ => store?.Select(id));

            row.AppliedStatus = (UnitStatus)(-1);
            ApplyStatus(row, state.Status);
            return row;
        }

        static void ApplyStatus(Row row, UnitStatus status)
        {
            if (row.AppliedStatus == status)
                return;

            row.AppliedStatus = status;
            foreach (VisualElement element in new[] { row.Dot, row.Status })
            {
                element.EnableInClassList("status-normal", status == UnitStatus.Normal);
                element.EnableInClassList("status-warning", status == UnitStatus.Warning);
                element.EnableInClassList("status-lost", status == UnitStatus.Lost);
            }

            row.Status.text = UnitFormat.StatusLabel(status);
        }
    }
}
