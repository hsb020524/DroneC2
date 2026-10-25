using DroneC2.Core;
using DroneC2.UI;
using NUnit.Framework;

namespace DroneC2.Tests
{
    public class DataSourceStatusFormatTests
    {
        [Test]
        public void FakeGps_IsLabeledAsSimulation()
        {
            DataSourceStatus s = DataSourceStatusFormat.Describe(TelemetrySourceMode.FakeGps, false, false, 0, 0, 0, 0, 0);

            Assert.AreEqual("데이터 소스: 가상 GPS(시뮬레이션)", s.Text);
            Assert.AreEqual(DataSourceTone.Simulated, s.Tone);
        }

        [Test]
        public void FakeGps_IgnoresReceiverCounters()
        {
            DataSourceStatus s = DataSourceStatusFormat.Describe(TelemetrySourceMode.FakeGps, true, true, 14560, 99, 1, 2, 3);

            Assert.AreEqual("데이터 소스: 가상 GPS(시뮬레이션)", s.Text);
        }

        [Test]
        public void Mavlink_Listening_ShowsEndpointAndCounters()
        {
            DataSourceStatus s = DataSourceStatusFormat.Describe(TelemetrySourceMode.Mavlink, true, true, 14560, 1200, 3, 40, 0);

            Assert.AreEqual("데이터 소스: MAVLink · 127.0.0.1:14560 · 수신 1200 / 손상 3 (미지원 40)", s.Text);
            Assert.AreEqual(DataSourceTone.Mavlink, s.Tone);
        }

        [Test]
        public void Mavlink_UnsupportedOnly_ShowsZeroCorrupted()
        {
            DataSourceStatus s = DataSourceStatusFormat.Describe(TelemetrySourceMode.Mavlink, true, true, 14560, 50, 0, 1846, 0);

            StringAssert.Contains("손상 0 (미지원 1846)", s.Text);
            StringAssert.DoesNotContain("거부", s.Text);
        }

        [Test]
        public void Mavlink_UnknownSysid_IsAppended()
        {
            DataSourceStatus s = DataSourceStatusFormat.Describe(TelemetrySourceMode.Mavlink, true, true, 14560, 10, 0, 0, 4);

            StringAssert.EndsWith("미등록 sysid 4", s.Text);
        }

        [Test]
        public void Mavlink_WithoutReceiver_IsError()
        {
            DataSourceStatus s = DataSourceStatusFormat.Describe(TelemetrySourceMode.Mavlink, false, false, 0, 0, 0, 0, 0);

            Assert.AreEqual("데이터 소스: MAVLink · 수신기 없음", s.Text);
            Assert.AreEqual(DataSourceTone.Error, s.Tone);
        }

        [Test]
        public void Mavlink_ReceiverNotListening_IsError()
        {
            DataSourceStatus s = DataSourceStatusFormat.Describe(TelemetrySourceMode.Mavlink, true, false, 14560, 0, 0, 0, 0);

            StringAssert.Contains("수신기 꺼짐", s.Text);
            Assert.AreEqual(DataSourceTone.Error, s.Tone);
        }
    }
}
