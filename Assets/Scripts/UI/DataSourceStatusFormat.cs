using System.Globalization;
using DroneC2.Core;

namespace DroneC2.UI
{
    /// <summary>상단 표시줄의 색 구분.</summary>
    public enum DataSourceTone
    {
        /// <summary>가상 GPS(시뮬레이션). 실제 수신 데이터가 아니다.</summary>
        Simulated,

        /// <summary>MAVLink 수신 중(수신기가 소켓을 열고 있음).</summary>
        Mavlink,

        /// <summary>MAVLink 모드인데 수신기가 없거나 소켓이 열려 있지 않음.</summary>
        Error,
    }

    public readonly struct DataSourceStatus
    {
        public readonly string Text;
        public readonly DataSourceTone Tone;

        public DataSourceStatus(string text, DataSourceTone tone)
        {
            Text = text;
            Tone = tone;
        }
    }

    /// <summary>
    /// 관제 화면 상단에 항상 표시할 데이터 소스와 수신 상태 문구. Unity에 의존하지 않는 순수 함수.
    /// 운용자가 가짜 데이터와 수신 데이터를 혼동하지 않게 하는 것이 목적이다.
    /// </summary>
    public static class DataSourceStatusFormat
    {
        public static DataSourceStatus Describe(TelemetrySourceMode mode, bool hasReceiver, bool listening,
            int port, long receivedFrames, long rejectedFrames, long unknownSysid)
        {
            if (mode == TelemetrySourceMode.FakeGps)
                return new DataSourceStatus("데이터 소스: 가상 GPS(시뮬레이션)", DataSourceTone.Simulated);

            if (!hasReceiver)
                return new DataSourceStatus("데이터 소스: MAVLink · 수신기 없음", DataSourceTone.Error);

            if (!listening)
                return new DataSourceStatus("데이터 소스: MAVLink · 수신기 꺼짐(소켓 없음)", DataSourceTone.Error);

            string text = string.Format(CultureInfo.InvariantCulture,
                "데이터 소스: MAVLink · 127.0.0.1:{0} · 수신 {1} / 거부 {2}", port, receivedFrames, rejectedFrames);
            if (unknownSysid > 0)
                text += string.Format(CultureInfo.InvariantCulture, " · 미등록 sysid {0}", unknownSysid);
            return new DataSourceStatus(text, DataSourceTone.Mavlink);
        }
    }
}
