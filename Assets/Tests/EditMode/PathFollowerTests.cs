using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DroneC2.DataSource;
using NUnit.Framework;
using UnityEngine;

namespace DroneC2.Tests
{
    public class PathFollowerTests
    {
        static readonly (double latitude, double longitude)[] Square =
        {
            (35.0, -83.0), (35.001, -83.0), (35.001, -83.001), (35.0, -83.001),
        };

        const double EarthRadiusM = 6378137.0;

        [Test]
        public void Evaluate_AtZero_IsFirstPointAndHeadsAlongFirstSegment()
        {
            var follower = new PathFollower(Square);
            follower.Evaluate(0.0, out double lat, out double lon, out double heading);
            Assert.AreEqual(35.0, lat, 1e-12);
            Assert.AreEqual(-83.0, lon, 1e-12);
            Assert.AreEqual(0.0, heading, 1e-6); // 정북
        }

        [Test]
        public void Evaluate_AtCorner_DoesNotCutTheCorner()
        {
            var follower = new PathFollower(Square);
            double northM = 0.001 * Math.PI / 180.0 * EarthRadiusM;
            follower.Evaluate(northM, out double lat, out double lon, out _);
            Assert.AreEqual(35.001, lat, 1e-9);
            Assert.AreEqual(-83.0, lon, 1e-9);
        }

        [Test]
        public void Evaluate_AfterManyLaps_RepeatsPosition()
        {
            var follower = new PathFollower(Square);
            follower.Evaluate(10.0, out double lat1, out double lon1, out double h1);
            follower.Evaluate(10.0 + follower.TotalLengthM * 3, out double lat2, out double lon2, out double h2);
            Assert.AreEqual(lat1, lat2, 1e-7);
            Assert.AreEqual(lon1, lon2, 1e-7);
            Assert.AreEqual(h1, h2, 1e-6);
        }

        [Test]
        public void Evaluate_NegativeDistance_WrapsFromTheEnd()
        {
            var follower = new PathFollower(Square);
            follower.Evaluate(follower.TotalLengthM - 5.0, out double lat1, out double lon1, out _);
            follower.Evaluate(-5.0, out double lat2, out double lon2, out _);
            Assert.AreEqual(lat1, lat2, 1e-9);
            Assert.AreEqual(lon1, lon2, 1e-9);
        }

        [Test]
        public void Evaluate_ZeroLengthSegments_AreSkipped()
        {
            var points = new[] { (35.0, -83.0), (35.0, -83.0), (35.001, -83.0), (35.001, -83.001) };
            new PathFollower(points).Evaluate(1.0, out double lat, out _, out _);
            Assert.That(lat, Is.GreaterThan(35.0).And.LessThan(35.001));
        }

        [Test]
        public void Constructor_RejectsDegeneratePaths()
        {
            Assert.Throws<ArgumentException>(() => new PathFollower(new[] { (35.0, -83.0) }));
            Assert.Throws<ArgumentException>(() => new PathFollower(new[] { (35.0, -83.0), (35.0, -83.0) }));
        }

        [Serializable]
        class GoldenFile { public GoldenCase[] cases; }

        [Serializable]
        class GoldenCase
        {
            public string name;
            public string totalLengthM;
            public GoldenPoint[] points;
            public GoldenSample[] samples;
        }

        [Serializable]
        class GoldenPoint { public string latitude; public string longitude; }

        [Serializable]
        class GoldenSample
        {
            public string distanceM;
            public string latitude;
            public string longitude;
            public string headingDeg;
        }

        static double D(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        // tools/path_follower.py가 기록한 값과 같은 결과를 내는지 대조한다. (경로·수식을 바꾸면 tools/make_path_golden.py로 다시 만든다)
        [Test]
        public void Evaluate_MatchesPythonGoldenValues()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "tools", "testdata", "path_follower_golden.json"));
            var golden = JsonUtility.FromJson<GoldenFile>(File.ReadAllText(path));
            Assert.GreaterOrEqual(golden.cases.Length, 4);

            foreach (GoldenCase c in golden.cases)
            {
                var points = new List<(double, double)>();
                foreach (GoldenPoint p in c.points)
                    points.Add((D(p.latitude), D(p.longitude)));

                var follower = new PathFollower(points);
                Assert.AreEqual(D(c.totalLengthM), follower.TotalLengthM, 1e-9, c.name);
                foreach (GoldenSample s in c.samples)
                {
                    follower.Evaluate(D(s.distanceM), out double lat, out double lon, out double heading);
                    Assert.AreEqual(D(s.latitude), lat, 1e-11, $"{c.name} d={s.distanceM} 위도");
                    Assert.AreEqual(D(s.longitude), lon, 1e-11, $"{c.name} d={s.distanceM} 경도");
                    Assert.AreEqual(D(s.headingDeg), heading, 1e-8, $"{c.name} d={s.distanceM} 방위");
                }
            }
        }

        // 저장소의 실제 시나리오 파일이 파싱되고 모든 유닛 경로가 이동 가능한지 확인한다.
        [TestCase("stage3-demo.json", 3)]
        [TestCase("osm-roads-tnk.json", 2)]
        public void CommittedScenario_ParsesAndEveryUnitIsFollowable(string file, int expectedUnits)
        {
            string path = Path.Combine(Application.dataPath, "Scenarios", file);
            Assert.IsTrue(Scenario.TryParse(File.ReadAllText(path), out Scenario scenario, out string error), error);
            Assert.AreEqual(expectedUnits, scenario.units.Count);

            foreach (ScenarioUnit unit in scenario.units)
            {
                var points = new List<(double, double)>();
                foreach (ScenarioPoint p in unit.waypoints)
                    points.Add((p.latitude, p.longitude));
                Assert.DoesNotThrow(() => new PathFollower(points), unit.id);
            }
        }
    }
}
