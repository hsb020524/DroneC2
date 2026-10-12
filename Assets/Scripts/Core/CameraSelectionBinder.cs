using DroneC2.View;
using UnityEngine;

namespace DroneC2.Core
{
    /// <summary>
    /// 레지스트리의 선택 상태를 카메라 추적 대상에 연결한다. 선택이 해제되면 전체 보기로 돌아간다.
    /// </summary>
    public class CameraSelectionBinder : MonoBehaviour
    {
        [SerializeField] UnitRegistry registry;
        [SerializeField] FollowCamera followCamera;

        UnitStateStore store;

        void OnEnable()
        {
            if (registry == null || followCamera == null)
            {
                Debug.LogError("CameraSelectionBinder: 레지스트리와 카메라를 지정해야 합니다.", this);
                enabled = false;
                return;
            }

            store = registry.Store;
            store.SelectionChanged += OnSelectionChanged;
            OnSelectionChanged(store.SelectedId);
        }

        void OnDisable()
        {
            if (store != null)
            {
                store.SelectionChanged -= OnSelectionChanged;
                store = null;
            }
        }

        void OnSelectionChanged(string unitId)
        {
            if (unitId != null && registry.TryGetView(unitId, out Transform view))
                followCamera.SetTarget(view);
            else
                followCamera.SetTarget(null);
        }
    }
}
