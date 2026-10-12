using DroneC2.DataSource;
using UnityEngine;

namespace DroneC2.View
{
    /// <summary>
    /// 유닛 위에 떠 있는 빌보드 마커. 종류는 형태(드론=마름모, 전차=사각형), 상태는 색상으로 구분한다.
    /// 카메라와의 거리에 비례해 크기를 보정해 화면에서 일정한 크기로 보인다.
    /// 위경도나 데이터 소스를 알지 못하며 <see cref="Apply"/>로 받은 값만 표시한다.
    /// </summary>
    public class UnitMarker : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer droneShape;
        [SerializeField] Renderer tankShape;
        [SerializeField, Min(0f)] float heightOffsetM = 30f;
        [Tooltip("카메라 거리 1m당 마커 월드 크기. 화면 높이에 대한 비율에 해당한다.")]
        [SerializeField, Min(0.001f)] float sizePerDistance = 0.025f;
        [SerializeField, Min(1f)] float selectedScale = 1.6f;
        [SerializeField] Color normalColor = new Color(0.25f, 0.9f, 0.35f);
        [SerializeField] Color warningColor = new Color(1f, 0.8f, 0.1f);
        [SerializeField] Color lostColor = new Color(0.9f, 0.2f, 0.2f);

        MaterialPropertyBlock block;
        Camera cam;
        bool selected;
        bool applied;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            SetShapeActive(droneShape, false);
            SetShapeActive(tankShape, false);
            // 첫 샘플로 종류가 정해지기 전에는 보이지 않는다.
        }

        public void Apply(UnitKind kind, UnitStatus status)
        {
            applied = true;
            SetShapeActive(droneShape, kind == UnitKind.Drone);
            SetShapeActive(tankShape, kind == UnitKind.Tank);

            Color color = status switch
            {
                UnitStatus.Warning => warningColor,
                UnitStatus.Lost => lostColor,
                _ => normalColor,
            };
            Paint(droneShape, color);
            Paint(tankShape, color);
        }

        public void SetSelected(bool value)
        {
            selected = value;
        }

        void LateUpdate()
        {
            if (!applied)
                return;

            if (cam == null)
                cam = Camera.main;
            if (cam == null)
                return;

            Transform parent = transform.parent;
            Vector3 anchor = parent != null ? parent.position : Vector3.zero;
            transform.position = anchor + Vector3.up * heightOffsetM;
            transform.rotation = cam.transform.rotation;

            float distance = Vector3.Distance(cam.transform.position, transform.position);
            float size = Mathf.Max(distance, 1f) * sizePerDistance * (selected ? selectedScale : 1f);
            transform.localScale = Vector3.one * (size / ParentScale(parent));
        }

        static float ParentScale(Transform parent)
        {
            return parent != null ? Mathf.Max(parent.lossyScale.y, 0.0001f) : 1f;
        }

        static void SetShapeActive(Renderer shape, bool active)
        {
            if (shape != null)
                shape.gameObject.SetActive(active);
        }

        void Paint(Renderer shape, Color color)
        {
            if (shape == null)
                return;

            shape.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            shape.SetPropertyBlock(block);
        }
    }
}
