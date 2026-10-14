using System.Collections.Generic;
using DroneC2.Core;
using DroneC2.DataSource;
using NUnit.Framework;
using UnityEngine;

namespace DroneC2.Tests
{
    public class UnitStatePinTests
    {
        static UnitStateStore StoreWith(params string[] ids)
        {
            var store = new UnitStateStore();
            foreach (string id in ids)
            {
                store.Report(new TelemetrySample(id, 35.0, -83.0, 80.0, 90.0, 10.0, 0.0,
                    UnitKind.Drone, UnitStatus.Normal), Vector3.zero, 0.0);
            }
            return store;
        }

        [Test]
        public void Pin_RegisteredUnit_SetsPinnedAndRaisesOnce()
        {
            UnitStateStore store = StoreWith("UAV-01");
            var events = new List<string>();
            store.PinnedChanged += events.Add;

            Assert.IsTrue(store.Pin("UAV-01"));
            Assert.IsTrue(store.Pin("UAV-01"));

            Assert.AreEqual("UAV-01", store.PinnedId);
            CollectionAssert.AreEqual(new[] { "UAV-01" }, events);
        }

        [Test]
        public void Pin_UnknownOrNull_ReturnsFalseAndKeepsPin()
        {
            UnitStateStore store = StoreWith("UAV-01");
            store.Pin("UAV-01");

            Assert.IsFalse(store.Pin("NOPE"));
            Assert.IsFalse(store.Pin(null));
            Assert.AreEqual("UAV-01", store.PinnedId);
        }

        [Test]
        public void Pin_Another_ReplacesPin()
        {
            UnitStateStore store = StoreWith("UAV-01", "TNK-01");
            store.Pin("UAV-01");
            store.Pin("TNK-01");

            Assert.AreEqual("TNK-01", store.PinnedId);
        }

        [Test]
        public void Unpin_ClearsAndRaisesNullOnce()
        {
            UnitStateStore store = StoreWith("UAV-01");
            store.Pin("UAV-01");
            var events = new List<string>();
            store.PinnedChanged += events.Add;

            store.Unpin();
            store.Unpin();

            Assert.IsNull(store.PinnedId);
            Assert.AreEqual(1, events.Count);
            Assert.IsNull(events[0]);
        }

        [Test]
        public void PinAndSelection_AreIndependent()
        {
            UnitStateStore store = StoreWith("UAV-01", "TNK-01");
            store.Select("UAV-01");
            store.Pin("TNK-01");
            store.ClearSelection();

            Assert.AreEqual("TNK-01", store.PinnedId);
        }
    }
}
