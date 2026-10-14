using System.Collections.Generic;
using UnityEngine;

namespace DroneC2.Core
{
    /// <summary>
    /// 씬에 하나 두는 유닛 레지스트리. 바인더가 샘플을 보고하고, UI·카메라가 상태와 선택을 읽는다.
    /// 판정 로직은 <see cref="UnitStateStore"/>에 있고 이 클래스는 시계와 유닛의 Transform 조회를 맡는다.
    /// </summary>
    public class UnitRegistry : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float lostTimeoutSec = 2f;

        readonly Dictionary<string, Transform> views = new Dictionary<string, Transform>();
        readonly Dictionary<string, RenderTexture> cameraTextures = new Dictionary<string, RenderTexture>();
        UnitStateStore store;

        public UnitStateStore Store => store ??= new UnitStateStore(lostTimeoutSec);

        void OnValidate()
        {
            if (store != null)
                store.LostTimeoutSec = lostTimeoutSec;
        }

        void Update()
        {
            Store.Tick(Time.timeAsDouble);
        }

        /// <summary>유닛의 표시 Transform을 등록한다. 카메라 추적처럼 Transform이 필요한 곳에서 조회한다.</summary>
        public void RegisterView(string unitId, Transform view)
        {
            if (string.IsNullOrEmpty(unitId) || view == null)
                return;

            views[unitId] = view;
        }

        /// <summary>유닛 카메라 영상(RenderTexture)을 등록한다. 패널은 이 텍스처만 읽는다.</summary>
        public void RegisterCameraTexture(string unitId, RenderTexture texture)
        {
            if (string.IsNullOrEmpty(unitId) || texture == null)
                return;

            cameraTextures[unitId] = texture;
        }

        public bool TryGetCameraTexture(string unitId, out RenderTexture texture)
        {
            if (unitId == null)
            {
                texture = null;
                return false;
            }

            return cameraTextures.TryGetValue(unitId, out texture) && texture != null;
        }

        public bool TryGetView(string unitId, out Transform view)
        {
            if (unitId == null)
            {
                view = null;
                return false;
            }

            return views.TryGetValue(unitId, out view) && view != null;
        }
    }
}
