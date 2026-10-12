using UnityEngine;

namespace DroneC2.Core
{
    /// <summary>
    /// 씬에 하나 두는 유닛 레지스트리. 바인더가 샘플을 보고하고, UI·카메라가 상태와 선택을 읽는다.
    /// 판정 로직은 <see cref="UnitStateStore"/>에 있고 이 클래스는 시계를 연결할 뿐이다.
    /// </summary>
    public class UnitRegistry : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float lostTimeoutSec = 2f;

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
    }
}
