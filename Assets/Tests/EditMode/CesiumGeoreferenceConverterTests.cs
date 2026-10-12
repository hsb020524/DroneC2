using CesiumForUnity;
using DroneC2.Coordinate;
using NUnit.Framework;
using UnityEngine;

namespace DroneC2.Tests
{
    public class CesiumGeoreferenceConverterTests
    {
        // 시연 지역(그레이트 스모키 산맥) 기준점. 높이는 타원체고(m).
        const double OriginLat = 35.563;
        const double OriginLon = -83.499;
        const double OriginHeight = 2000.0;

        // WGS84. 기대값을 Cesium과 독립적으로 계산하기 위한 상수.
        const double SemiMajorM = 6378137.0;
        const double EccSq = 0.00669437999014;
        const double DegToRad = System.Math.PI / 180.0;

        // NFR-005: 변환 오차 허용치. 수백 m 범위에서 0.5 m 이내를 기준으로 한다.
        const float ToleranceM = 0.5f;

        GameObject geoGo;
        GameObject converterGo;
        CesiumGeoreferenceConverter converter;

        [SetUp]
        public void SetUp()
        {
            geoGo = new GameObject("Georeference");
            var geo = geoGo.AddComponent<CesiumGeoreference>();
            geo.originAuthority = CesiumGeoreferenceOriginAuthority.LongitudeLatitudeHeight;
            geo.latitude = OriginLat;
            geo.longitude = OriginLon;
            geo.height = OriginHeight;

            converterGo = new GameObject("Converter");
            converter = converterGo.AddComponent<CesiumGeoreferenceConverter>();
            converter.SetGeoreference(geo);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(converterGo);
            Object.DestroyImmediate(geoGo);
        }

        // 위도 1도당 북쪽 거리(m), 경도 1도당 동쪽 거리(m). 타원체 곡률 반경 기준.
        static double MetersPerDegreeNorth(double latDeg)
        {
            double s = System.Math.Sin(latDeg * DegToRad);
            double w = 1.0 - EccSq * s * s;
            double meridianRadius = SemiMajorM * (1.0 - EccSq) / (w * System.Math.Sqrt(w));
            return meridianRadius * DegToRad;
        }

        static double MetersPerDegreeEast(double latDeg)
        {
            double s = System.Math.Sin(latDeg * DegToRad);
            double primeVerticalRadius = SemiMajorM / System.Math.Sqrt(1.0 - EccSq * s * s);
            return primeVerticalRadius * System.Math.Cos(latDeg * DegToRad) * DegToRad;
        }

        [Test]
        public void Origin_MapsToZero()
        {
            Vector3 p = converter.ToWorld(OriginLat, OriginLon, OriginHeight);
            Assert.AreEqual(0f, p.magnitude, 0.05f);
        }

        [Test]
        public void LatitudeOffset_MapsToNorth()
        {
            const double dLat = 0.001;
            Vector3 p = converter.ToWorld(OriginLat + dLat, OriginLon, OriginHeight);
            Assert.AreEqual((float)(MetersPerDegreeNorth(OriginLat) * dLat), p.z, ToleranceM);
            Assert.AreEqual(0f, p.x, ToleranceM);
            Assert.AreEqual(0f, p.y, ToleranceM);
        }

        [Test]
        public void LongitudeOffset_MapsToEast()
        {
            const double dLon = 0.001;
            Vector3 p = converter.ToWorld(OriginLat, OriginLon + dLon, OriginHeight);
            Assert.AreEqual((float)(MetersPerDegreeEast(OriginLat) * dLon), p.x, ToleranceM);
            Assert.AreEqual(0f, p.z, ToleranceM);
            Assert.AreEqual(0f, p.y, ToleranceM);
        }

        [Test]
        public void Height_MapsToY()
        {
            Vector3 p = converter.ToWorld(OriginLat, OriginLon, OriginHeight + 120.0);
            Assert.AreEqual(120f, p.y, 0.05f);
            Assert.AreEqual(0f, p.x, 0.05f);
            Assert.AreEqual(0f, p.z, 0.05f);
        }

        [TestCase(0.0, 0f, 0f, 1f)]
        [TestCase(90.0, 1f, 0f, 0f)]
        [TestCase(180.0, 0f, 0f, -1f)]
        public void Heading_RotatesForwardClockwiseFromNorth(double heading, float x, float y, float z)
        {
            Vector3 forward = converter.HeadingToRotation(heading) * Vector3.forward;
            Assert.AreEqual(x, forward.x, 1e-4f);
            Assert.AreEqual(y, forward.y, 1e-4f);
            Assert.AreEqual(z, forward.z, 1e-4f);
        }
    }
}
