using DroneC2.Coordinate;
using DroneC2.Core;
using NUnit.Framework;

namespace DroneC2.Tests
{
    public class AltitudeResolverTests
    {
        class FakeSampler : ITerrainHeightSampler
        {
            public bool Available;
            public double Height;

            public bool TryGetGroundHeight(double latitude, double longitude, out double ellipsoidHeightM)
            {
                ellipsoidHeightM = Height;
                return Available;
            }
        }

        [Test]
        public void Absolute_ReturnsReportedAltitude_WithoutSampler()
        {
            var resolver = new AltitudeResolver(null);
            Assert.IsTrue(resolver.TryResolve(AltitudeMode.Absolute, 0, 0, 123.0, out double alt));
            Assert.AreEqual(123.0, alt, 1e-9);
        }

        [Test]
        public void Absolute_IgnoresTerrain()
        {
            var resolver = new AltitudeResolver(new FakeSampler { Available = true, Height = 1800.0 });
            Assert.IsTrue(resolver.TryResolve(AltitudeMode.Absolute, 0, 0, 50.0, out double alt));
            Assert.AreEqual(50.0, alt, 1e-9);
        }

        [Test]
        public void AboveGround_AddsReportedAltitudeToGround()
        {
            var resolver = new AltitudeResolver(new FakeSampler { Available = true, Height = 1800.0 });
            Assert.IsTrue(resolver.TryResolve(AltitudeMode.AboveGround, 0, 0, 80.0, out double alt));
            Assert.AreEqual(1880.0, alt, 1e-9);
        }

        [Test]
        public void ClampToGround_UsesGroundHeightOnly()
        {
            var resolver = new AltitudeResolver(new FakeSampler { Available = true, Height = 1800.0 });
            Assert.IsTrue(resolver.TryResolve(AltitudeMode.ClampToGround, 0, 0, 80.0, out double alt));
            Assert.AreEqual(1800.0, alt, 1e-9);
        }

        [Test]
        public void GroundUnavailable_NeverResolved_IsNotReady()
        {
            var resolver = new AltitudeResolver(new FakeSampler { Available = false });
            Assert.IsFalse(resolver.TryResolve(AltitudeMode.AboveGround, 0, 0, 80.0, out _));
            Assert.IsFalse(resolver.TryResolve(AltitudeMode.ClampToGround, 0, 0, 0.0, out _));
        }

        [Test]
        public void GroundUnavailable_AfterSuccess_KeepsLastGround()
        {
            var sampler = new FakeSampler { Available = true, Height = 1800.0 };
            var resolver = new AltitudeResolver(sampler);
            resolver.TryResolve(AltitudeMode.AboveGround, 0, 0, 80.0, out _);

            sampler.Available = false;
            sampler.Height = 0.0;
            Assert.IsTrue(resolver.TryResolve(AltitudeMode.AboveGround, 0, 0, 80.0, out double alt));
            Assert.AreEqual(1880.0, alt, 1e-9);
        }

        [Test]
        public void GroundUpdates_WhenSamplerReturnsNewValue()
        {
            var sampler = new FakeSampler { Available = true, Height = 1800.0 };
            var resolver = new AltitudeResolver(sampler);
            resolver.TryResolve(AltitudeMode.AboveGround, 0, 0, 80.0, out _);

            sampler.Height = 1700.0;
            Assert.IsTrue(resolver.TryResolve(AltitudeMode.AboveGround, 0, 0, 80.0, out double alt));
            Assert.AreEqual(1780.0, alt, 1e-9);
        }

        [Test]
        public void NoSampler_TreatsGroundAsFlatPlaneAtZero()
        {
            var resolver = new AltitudeResolver(null);
            Assert.IsTrue(resolver.TryResolve(AltitudeMode.AboveGround, 0, 0, 20.0, out double above));
            Assert.AreEqual(20.0, above, 1e-9);
            Assert.IsTrue(resolver.TryResolve(AltitudeMode.ClampToGround, 0, 0, 20.0, out double clamped));
            Assert.AreEqual(0.0, clamped, 1e-9);
        }
    }
}
