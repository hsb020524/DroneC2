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
