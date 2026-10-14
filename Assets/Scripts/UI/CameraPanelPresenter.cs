using DroneC2.Core;
using DroneC2.DataSource;
using UnityEngine;
using UnityEngine.UIElements;

namespace DroneC2.UI
{
    /// <summary>
    /// 선택한 유닛과 고정한 유닛의 카메라 영상을 패널 2개에 표시한다. 두절되면 "신호 없음"을 덮어 표시한다.
    /// 레지스트리만 읽고 카메라 렌더링이나 데이터 소스는 알지 못한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CameraPanelPresenter : MonoBehaviour
    {
        const string HiddenClass = "hidden";

        /// <summary>패널 하나(선택용 또는 고정용)의 요소 묶음.</summary>
        sealed class Slot
        {
            public VisualElement Panel;
            public Label Title;
            public Image Image;
            public Label LostOverlay;
            public string UnitId;
        }

        [SerializeField] UnitRegistry registry;

        UnitStateStore store;
        readonly Slot selectedSlot = new Slot();
        readonly Slot pinnedSlot = new Slot();
        Button pinButton;
        Button unpinButton;
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

            UpdateOverlay(selectedSlot);
            UpdateOverlay(pinnedSlot);
        }

        void UpdateOverlay(Slot slot)
        {
            if (slot.UnitId == null || !store.TryGet(slot.UnitId, out UnitState state))
                return;

            slot.LostOverlay.EnableInClassList(HiddenClass, state.Status != UnitStatus.Lost);
        }

        void Bind()
        {
            if (store != null)
                return;

            if (registry == null)
            {
                Debug.LogError("CameraPanelPresenter: 레지스트리를 지정해야 합니다.", this);
                return;
            }

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            if (!Find(root, selectedSlot, "camera-panel", "camera-title", "camera-image", "camera-lost")
                || !Find(root, pinnedSlot, "pinned-panel", "pinned-title", "pinned-image", "pinned-lost"))
                return;

            pinButton = root.Q<Button>("pin-button");
            unpinButton = root.Q<Button>("unpin-button");
            pinButton.clicked += OnPinClicked;
            unpinButton.clicked += OnUnpinClicked;

            store = registry.Store;
            store.SelectionChanged += OnSelectionChanged;
            store.PinnedChanged += OnPinnedChanged;
            OnSelectionChanged(store.SelectedId);
            OnPinnedChanged(store.PinnedId);
        }

        bool Find(VisualElement root, Slot slot, string panel, string title, string image, string lost)
        {
            slot.Panel = root?.Q<VisualElement>(panel);
            if (slot.Panel == null)
            {
                Debug.LogError($"CameraPanelPresenter: UXML에서 {panel}을 찾을 수 없습니다.", this);
                return false;
            }

            slot.Title = root.Q<Label>(title);
            slot.Image = root.Q<Image>(image);
            slot.LostOverlay = root.Q<Label>(lost);
            return true;
        }

        void Unbind()
        {
            if (store == null)
                return;

            store.SelectionChanged -= OnSelectionChanged;
            store.PinnedChanged -= OnPinnedChanged;
            store = null;
            pinButton.clicked -= OnPinClicked;
            unpinButton.clicked -= OnUnpinClicked;
            selectedSlot.Panel?.AddToClassList(HiddenClass);
            pinnedSlot.Panel?.AddToClassList(HiddenClass);
        }

        void OnPinClicked()
        {
            if (store.SelectedId != null)
                store.Pin(store.SelectedId);
        }

        void OnUnpinClicked()
        {
            store.Unpin();
        }

        void OnSelectionChanged(string selectedId)
        {
            Show(selectedSlot, selectedId, "카메라");
            UpdatePinButton();
        }

        void OnPinnedChanged(string pinnedId)
        {
            Show(pinnedSlot, pinnedId, "고정");
            UpdatePinButton();
        }

        void Show(Slot slot, string unitId, string titlePrefix)
        {
            if (unitId != null && registry.TryGetCameraTexture(unitId, out RenderTexture texture))
            {
                slot.UnitId = unitId;
                slot.Title.text = $"{titlePrefix} · {unitId}";
                slot.Image.image = texture;
                slot.Panel.RemoveFromClassList(HiddenClass);
            }
            else
            {
                slot.UnitId = null;
                slot.Image.image = null;
                slot.Panel.AddToClassList(HiddenClass);
            }
        }

        /// <summary>이미 고정한 유닛을 또 고정할 수 없다.</summary>
        void UpdatePinButton()
        {
            pinButton.SetEnabled(store.SelectedId != null && store.SelectedId != store.PinnedId);
        }
    }
}
