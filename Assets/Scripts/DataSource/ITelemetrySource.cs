using System;

namespace DroneC2.DataSource
{
    /// <summary>
    /// 텔레메트리 데이터 소스의 공통 인터페이스. 가짜 GPS, MAVLink 등 구현체를 교체할 수 있다.
    /// </summary>
    public interface ITelemetrySource
    {
        event Action<TelemetrySample> SampleReceived;
    }
}
