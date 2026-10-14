using CesiumForUnity;
using UnityEngine;

namespace DroneC2.View
{
    /// <summary>
    /// 렌더 중인 유닛 카메라를 Cesium 타일 선택 카메라로 등록한다.
    /// 등록하지 않으면 유닛 카메라는 메인 카메라가 로드한 타일만 보므로, 메인 카메라가 먼 곳을 보면 영상이 비어 보인다.
    /// 등록은 렌더 중일 때만 하므로 추가 타일 부담은 선택·고정된 유닛 수(최대 2)로 제한된다.
    /// </summary>
    public class CesiumUnitCameraLink : MonoBehaviour
    {
        [SerializeField] Cesium3DTileset tileset;

        CesiumCameraManager manager;
        UnitCameraRig[] rigs;

        void Start()
        {
            if (tileset == null)
            {
                Debug.LogError("CesiumUnitCameraLink: 타일셋을 지정해야 합니다.", this);
                enabled = false;
                return;
            }

            manager = CesiumCameraManager.GetOrCreate(tileset.gameObject);
            rigs = FindObjectsByType<UnitCameraRig>(FindObjectsSortMode.None);
            foreach (UnitCameraRig rig in rigs)
            {
                rig.RenderingChanged += OnRenderingChanged;
                if (rig.IsRendering)
                    OnRenderingChanged(rig.Camera, true);
            }
        }

        void OnDestroy()
        {
            if (rigs == null)
                return;

            foreach (UnitCameraRig rig in rigs)
            {
                if (rig != null)
                    rig.RenderingChanged -= OnRenderingChanged;
            }
        }

        void OnRenderingChanged(Camera camera, bool rendering)
        {
            if (manager == null || camera == null)
                return;

            if (rendering)
            {
                if (!manager.additionalCameras.Contains(camera))
                    manager.additionalCameras.Add(camera);
            }
            else
            {
                manager.additionalCameras.Remove(camera);
            }
        }
    }
}
