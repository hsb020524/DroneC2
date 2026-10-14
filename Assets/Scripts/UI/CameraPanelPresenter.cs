using DroneC2.Core;
using DroneC2.DataSource;
using UnityEngine;
using UnityEngine.UIElements;

namespace DroneC2.UI
{
    /// <summary>
    /// 선택한 유닛의 카메라 영상을 패널에 표시한다. 두절되면 "신호 없음"을 덮어 표시한다.
    /// 레지스트리만 읽고 카메라 렌더링이나 데이터 소스는 알지 못한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CameraPanelPresenter : MonoBehaviour
    {
        const string HiddenClass = "hidden";

        [SerializeField] UnitRegistry registry;

        UnitStateStore store;
        VisualElement panel;
        Label title;
        Image image;
        Label lostOverlay;
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

            lostOverlay.EnableInClassList(HiddenClass, state.Status != UnitStatus.Lost);
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
            panel = root?.Q<VisualElement>("camera-panel");
            if (panel == null)
            {
                Debug.LogError("CameraPanelPresenter: UXML에서 camera-panel을 찾을 수 없습니다.", this);
                return;
            }

            title = root.Q<Label>("camera-title");
            image = root.Q<Image>("camera-image");
            lostOverlay = root.Q<Label>("camera-lost");

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
            if (selectedId != null && registry.TryGetCameraTexture(selectedId, out RenderTexture texture))
            {
                title.text = $"카메라 · {selectedId}";
                image.image = texture;
                panel.RemoveFromClassList(HiddenClass);
            }
            else
            {
                image.image = null;
                panel.AddToClassList(HiddenClass);
            }
        }
    }
}
