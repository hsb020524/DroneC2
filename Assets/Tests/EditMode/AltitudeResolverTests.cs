using DroneC2.Coordinate;
using DroneC2.Core;
using DroneC2.DataSource;
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

        // 위도에 비례하는 지형 높이. 현재 위치와 홈 위치의 조회를 구분하기 위한 샘플러.
        class LatitudeSampler : ITerrainHeightSampler
        {
            public bool Available = true;
            public int Calls;

            public bool TryGetGroundHeight(double latitude, double longitude, out double ellipsoidHeightM)
            {
                Calls++;
                ellipsoidHeightM = latitude * 100.0;
                return Available;
            }
        }

        [Test]
        public void HomeRelative_AddsReportedAltitudeToHomeGround_NotCurrentGround()
        {
            var sampler = new LatitudeSampler();
            var resolver = new AltitudeResolver(sampler);

            // 홈(위도 10) 지형 1000m + 상대고도 80m. 현재 위치(위도 20)의 지형은 쓰지 않는다.
            Assert.IsTrue(resolver.TryResolve(AltitudeMode.HomeRelative, 20.0, 0, 80.0, 10.0, 0.0, out double alt));
            Assert.AreEqual(1080.0, alt, 1e-9);
        }

        [Test]
        public void HomeRelative_LooksUpHomeOnlyWhenHomeChanges()
        {
            var sampler = new LatitudeSampler();
            var resolver = new AltitudeResolver(sampler);

            resolver.TryResolve(AltitudeMode.HomeRelative, 20.0, 0, 80.0, 10.0, 0.0, out _);
            resolver.TryResolve(AltitudeMode.HomeRelative, 21.0, 0, 70.0, 10.0, 0.0, out _);
            resolver.TryResolve(AltitudeMode.HomeRelative, 22.0, 0, 60.0, 10.0, 0.0, out _);
            Assert.AreEqual(1, sampler.Calls);

            Assert.IsTrue(resolver.TryResolve(AltitudeMode.HomeRelative, 22.0, 0, 60.0, 30.0, 0.0, out double alt));
            Assert.AreEqual(2, sampler.Calls);
            Assert.AreEqual(3060.0, alt, 1e-9);
        }

        [Test]
        public void HomeRelative_HoldsUntilHomeGroundAvailable()
        {
            var sampler = new LatitudeSampler { Available = false };
            var resolver = new AltitudeResolver(sampler);

            Assert.IsFalse(resolver.TryResolve(AltitudeMode.HomeRelative, 20.0, 0, 80.0, 10.0, 0.0, out _));

            sampler.Available = true;
            Assert.IsTrue(resolver.TryResolve(AltitudeMode.HomeRelative, 20.0, 0, 80.0, 10.0, 0.0, out double alt));
            Assert.AreEqual(1080.0, alt, 1e-9);
        }

        [Test]
        public void HomeRelative_ChangedHome_DoesNotReuseOldHomeGround()
        {
            var sampler = new LatitudeSampler();
            var resolver = new AltitudeResolver(sampler);
            resolver.TryResolve(AltitudeMode.HomeRelative, 20.0, 0, 80.0, 10.0, 0.0, out _);

            sampler.Available = false;
            Assert.IsFalse(resolver.TryResolve(AltitudeMode.HomeRelative, 20.0, 0, 80.0, 30.0, 0.0, out _));
        }

        [Test]
        public void HomeRelative_NaNHome_IsRejected()
        {
            var resolver = new AltitudeResolver(new LatitudeSampler());

            Assert.IsFalse(resolver.TryResolve(AltitudeMode.HomeRelative, 20.0, 0, 80.0, double.NaN, 0.0, out _));
            Assert.IsFalse(resolver.TryResolve(AltitudeMode.HomeRelative, 20.0, 0, 80.0, 10.0, double.NaN, out _));
            // 홈 인자가 없는 오버로드로도 HomeRelative는 통과하지 못한다.
            Assert.IsFalse(resolver.TryResolve(AltitudeMode.HomeRelative, 20.0, 0, 80.0, out _));
        }

        [Test]
        public void HomeRelative_NoSampler_TreatsHomeGroundAsZero()
        {
            var resolver = new AltitudeResolver(null);

            Assert.IsTrue(resolver.TryResolve(AltitudeMode.HomeRelative, 20.0, 0, 80.0, 10.0, 0.0, out double alt));
            Assert.AreEqual(80.0, alt, 1e-9);
        }

        [Test]
        public void AltitudeMode_NumericValues_AreStable()
        {
            // 직렬화된 에셋(프리팹·씬)과 호환되도록 기존 값을 바꾸지 않는다.
            Assert.AreEqual(0, (int)AltitudeMode.Absolute);
            Assert.AreEqual(1, (int)AltitudeMode.AboveGround);
            Assert.AreEqual(2, (int)AltitudeMode.ClampToGround);
            Assert.AreEqual(3, (int)AltitudeMode.HomeRelative);
        }
    }
}
