using DroneC2.DataSource;
using DroneC2.UI;
using NUnit.Framework;

namespace DroneC2.Tests
{
    public class UnitFormatTests
    {
        [Test]
        public void Latitude_UsesHemisphereLetter()
        {
            Assert.AreEqual("35.56300° N", UnitFormat.Latitude(35.563));
            Assert.AreEqual("12.50000° S", UnitFormat.Latitude(-12.5));
        }

        [Test]
        public void Longitude_UsesHemisphereLetter()
        {
            Assert.AreEqual("83.49900° W", UnitFormat.Longitude(-83.499));
            Assert.AreEqual("10.00000° E", UnitFormat.Longitude(10.0));
        }

        [Test]
        public void Speed_ShowsMetersPerSecondAndKilometersPerHour()
        {
            Assert.AreEqual("25.0 m/s (90 km/h)", UnitFormat.Speed(25.0));
        }

        [TestCase(0.0, "000° N")]
        [TestCase(90.0, "090° E")]
        [TestCase(180.0, "180° S")]
        [TestCase(270.0, "270° W")]
        [TestCase(45.0, "045° NE")]
        [TestCase(-90.0, "270° W")]
        [TestCase(360.0, "000° N")]
        [TestCase(359.6, "000° N")]
        [TestCase(725.0, "005° N")]
        public void Heading_NormalizesAndNamesDirection(double input, string expected)
        {
            Assert.AreEqual(expected, UnitFormat.Heading(input));
        }

        [Test]
        public void Labels_AreKorean()
        {
            Assert.AreEqual("드론", UnitFormat.KindLabel(UnitKind.Drone));
            Assert.AreEqual("전차", UnitFormat.KindLabel(UnitKind.Tank));
            Assert.AreEqual("정상", UnitFormat.StatusLabel(UnitStatus.Normal));
            Assert.AreEqual("경고", UnitFormat.StatusLabel(UnitStatus.Warning));
            Assert.AreEqual("두절", UnitFormat.StatusLabel(UnitStatus.Lost));
        }

        [Test]
        public void Link_Lost_ShowsLostWithElapsedTime()
        {
            Assert.AreEqual("두절 · 마지막 수신 5.2초 전", UnitFormat.Link(UnitStatus.Lost, 5.2));
        }

        [Test]
        public void Link_Warning_StillReceiving()
        {
            Assert.AreEqual("수신 중 · 마지막 수신 0.1초 전", UnitFormat.Link(UnitStatus.Warning, 0.1));
        }

        [Test]
        public void Link_NegativeElapsed_ClampedToZero()
        {
            Assert.AreEqual("수신 중 · 마지막 수신 0.0초 전", UnitFormat.Link(UnitStatus.Normal, -0.5));
        }
    }
}
