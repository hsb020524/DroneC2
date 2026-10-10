using DroneC2.Coordinate;
using NUnit.Framework;
using UnityEngine;

namespace DroneC2.Tests
{
    public class LocalTangentPlaneConverterTests
    {
        const double OriginLat = 30.0;
        const double OriginLon = -40.0;

        GameObject go;
        LocalTangentPlaneConverter converter;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Converter");
            converter = go.AddComponent<LocalTangentPlaneConverter>();
            converter.SetOrigin(OriginLat, OriginLon, 0.0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Origin_MapsToZero()
        {
            Vector3 p = converter.ToWorld(OriginLat, OriginLon, 0.0);
            Assert.AreEqual(0f, p.magnitude, 1e-3f);
        }

        [Test]
        public void LatitudeOffset_MapsToNorth()
        {
            Vector3 p = converter.ToWorld(OriginLat + 0.001, OriginLon, 0.0);
            Assert.AreEqual(111.32f, p.z, 0.5f);
            Assert.AreEqual(0f, p.x, 1e-3f);
        }

        [Test]
        public void LongitudeOffset_MapsToEast_ScaledByCosLatitude()
        {
            Vector3 p = converter.ToWorld(OriginLat, OriginLon + 0.001, 0.0);
            Assert.AreEqual(111.32f * Mathf.Cos(30f * Mathf.Deg2Rad), p.x, 0.5f);
            Assert.AreEqual(0f, p.z, 1e-3f);
        }

        [Test]
        public void Altitude_MapsToY()
        {
            Vector3 p = converter.ToWorld(OriginLat, OriginLon, 120.0);
            Assert.AreEqual(120f, p.y, 1e-3f);
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
