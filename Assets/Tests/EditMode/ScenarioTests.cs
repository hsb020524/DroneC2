using DroneC2.DataSource;
using NUnit.Framework;

namespace DroneC2.Tests
{
    public class ScenarioTests
    {
        const string Valid = @"{
            ""version"": 1,
            ""name"": ""test"",
            ""units"": [
                {
                    ""id"": ""UAV-01"", ""kind"": ""Drone"", ""altitudeM"": 80, ""speedMps"": 25,
                    ""waypoints"": [
                        { ""latitude"": 35.56, ""longitude"": -83.50 },
                        { ""latitude"": 35.57, ""longitude"": -83.49 }
                    ],
                    ""warning"": { ""startSec"": 10, ""durationSec"": 5 },
                    ""dropout"": { ""startSec"": 0, ""durationSec"": 0 }
                },
                {
                    ""id"": ""TNK-01"", ""kind"": ""Tank"", ""altitudeM"": 0, ""speedMps"": 12,
                    ""waypoints"": [
                        { ""latitude"": 35.55, ""longitude"": -83.51 },
                        { ""latitude"": 35.551, ""longitude"": -83.511 }
                    ]
                }
            ]
        }";

        static string WithUnit(string unitJson)
        {
            return "{\"units\": [" + unitJson + "]}";
        }

        [Test]
        public void TryParse_Valid_ReadsUnits()
        {
            bool ok = Scenario.TryParse(Valid, out Scenario scenario, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(2, scenario.units.Count);
            Assert.IsTrue(scenario.TryGetUnit("UAV-01", out ScenarioUnit uav));
            Assert.AreEqual(UnitKind.Drone, uav.Kind);
            Assert.AreEqual(80.0, uav.altitudeM, 1e-9);
            Assert.AreEqual(25f, uav.speedMps, 1e-6);
            Assert.AreEqual(2, uav.waypoints.Count);
            Assert.AreEqual(35.57, uav.waypoints[1].latitude, 1e-9);
            Assert.AreEqual(10f, uav.warning.startSec, 1e-6);
            Assert.AreEqual(5f, uav.warning.durationSec, 1e-6);
        }

        [Test]
        public void TryParse_MissingFaults_DefaultToNone()
        {
            Scenario.TryParse(Valid, out Scenario scenario, out _);

            scenario.TryGetUnit("TNK-01", out ScenarioUnit tank);

            Assert.AreEqual(UnitKind.Tank, tank.Kind);
            Assert.AreEqual(0f, tank.warning.durationSec);
            Assert.AreEqual(0f, tank.dropout.durationSec);
        }

        [Test]
        public void TryGetUnit_Unknown_ReturnsFalse()
        {
            Scenario.TryParse(Valid, out Scenario scenario, out _);
            Assert.IsFalse(scenario.TryGetUnit("NOPE", out _));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not json")]
        [TestCase("{}")]
        [TestCase("{\"units\": []}")]
        public void TryParse_EmptyOrBroken_Fails(string json)
        {
            Assert.IsFalse(Scenario.TryParse(json, out Scenario scenario, out string error));
            Assert.IsNull(scenario);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void TryParse_DuplicateId_Fails()
        {
            string unit = "{\"id\":\"A\",\"speedMps\":5,\"waypoints\":[{\"latitude\":1,\"longitude\":1},{\"latitude\":1,\"longitude\":2}]}";
            Assert.IsFalse(Scenario.TryParse(WithUnit(unit + "," + unit), out _, out string error));
            StringAssert.Contains("중복", error);
        }

        [Test]
        public void TryParse_UnknownKind_Fails()
        {
            string unit = "{\"id\":\"A\",\"kind\":\"Submarine\",\"speedMps\":5,\"waypoints\":[{\"latitude\":1,\"longitude\":1},{\"latitude\":1,\"longitude\":2}]}";
            Assert.IsFalse(Scenario.TryParse(WithUnit(unit), out _, out string error));
            StringAssert.Contains("종류", error);
        }

        [Test]
        public void TryParse_TooFewWaypoints_Fails()
        {
            string unit = "{\"id\":\"A\",\"speedMps\":5,\"waypoints\":[{\"latitude\":1,\"longitude\":1}]}";
            Assert.IsFalse(Scenario.TryParse(WithUnit(unit), out _, out _));
        }

        [Test]
        public void TryParse_NonPositiveSpeed_Fails()
        {
            string unit = "{\"id\":\"A\",\"speedMps\":0,\"waypoints\":[{\"latitude\":1,\"longitude\":1},{\"latitude\":1,\"longitude\":2}]}";
            Assert.IsFalse(Scenario.TryParse(WithUnit(unit), out _, out _));
        }

        [Test]
        public void TryParse_LatitudeOutOfRange_Fails()
        {
            string unit = "{\"id\":\"A\",\"speedMps\":5,\"waypoints\":[{\"latitude\":91,\"longitude\":1},{\"latitude\":1,\"longitude\":2}]}";
            Assert.IsFalse(Scenario.TryParse(WithUnit(unit), out _, out _));
        }
    }
}
