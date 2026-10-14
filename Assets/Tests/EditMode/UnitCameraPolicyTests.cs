using DroneC2.Core;
using DroneC2.DataSource;
using NUnit.Framework;

namespace DroneC2.Tests
{
    public class UnitCameraPolicyTests
    {
        [TestCase(true, UnitStatus.Normal, true)]
        [TestCase(true, UnitStatus.Warning, true)]
        [TestCase(true, UnitStatus.Lost, false)]
        [TestCase(false, UnitStatus.Normal, false)]
        [TestCase(false, UnitStatus.Lost, false)]
        public void ShouldRender_OnlySelectedAndNotLost(bool selected, UnitStatus status, bool expected)
        {
            Assert.AreEqual(expected, UnitCameraPolicy.ShouldRender(selected, status));
        }
    }
}
