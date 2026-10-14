using UnityEngine;

namespace DroneC2.View
{
    /// <summary>
    /// 유닛에 붙는 가상 카메라. 영상은 RenderTexture로만 내보내며 화면에 직접 그리지 않는다.
    /// 평소에는 렌더하지 않고 <see cref="SetRendering"/>으로 켜진 동안만 그린다.
    /// 유닛의 종류나 상태, 데이터 소스를 알지 못한다.
    /// </summary>
    public class UnitCameraRig : MonoBehaviour
    {
        [SerializeField] Camera unitCamera;
        [SerializeField, Min(64)] int width = 640;
        [SerializeField, Min(64)] int height = 360;
        [SerializeField, Range(0, 24)] int depthBits = 24;

        RenderTexture texture;

        /// <summary>패널에 표시할 영상. 렌더 여부와 관계없이 마지막 프레임을 유지한다.</summary>
        public RenderTexture Texture => texture;

        public bool IsRendering => unitCamera != null && unitCamera.enabled;

        void Awake()
        {
            if (unitCamera == null)
            {
                Debug.LogError("UnitCameraRig: 카메라를 지정해야 합니다.", this);
                enabled = false;
                return;
            }

            texture = new RenderTexture(width, height, depthBits, RenderTextureFormat.ARGB32)
            {
                name = $"UnitCamera_{(transform.parent != null ? transform.parent.name : name)}",
            };
            unitCamera.targetTexture = texture;
            unitCamera.enabled = false;
        }

        void OnDestroy()
        {
            if (unitCamera != null)
                unitCamera.targetTexture = null;

            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
                texture = null;
            }
        }

        public void SetRendering(bool value)
        {
            if (unitCamera != null && texture != null)
                unitCamera.enabled = value;
        }
    }
}
