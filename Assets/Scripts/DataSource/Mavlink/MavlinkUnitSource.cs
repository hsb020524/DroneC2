using System;
using UnityEngine;

namespace DroneC2.DataSource.Mavlink
{
    /// <summary>
    /// MAVLink sysid 한 개에 해당하는 데이터 소스. <see cref="MavlinkReceiver"/>가 메인 스레드에서
    /// 샘플을 넘겨 주며, 바인더는 FakeGpsSource와 똑같이 <see cref="ITelemetrySource"/>로 다룬다.
    /// </summary>
    public class MavlinkUnitSource : MonoBehaviour, ITelemetrySource
    {
        [SerializeField] MavlinkReceiver receiver;
        [SerializeField] byte systemId = 1;

        public event Action<TelemetrySample> SampleReceived;

        void OnEnable()
        {
            if (receiver == null)
            {
                Debug.LogError("MavlinkUnitSource: 수신기(MavlinkReceiver)를 지정해야 합니다.", this);
                enabled = false;
                return;
            }
            receiver.Register(systemId, this);
        }

        void OnDisable()
        {
            if (receiver != null)
                receiver.Unregister(systemId, this);
        }

        internal void Dispatch(TelemetrySample sample)
        {
            SampleReceived?.Invoke(sample);
        }
    }
}
