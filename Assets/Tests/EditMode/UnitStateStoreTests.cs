using System.Collections.Generic;
using DroneC2.Core;
using DroneC2.DataSource;
using NUnit.Framework;
using UnityEngine;

namespace DroneC2.Tests
{
    public class UnitStateStoreTests
    {
        static TelemetrySample Sample(string id, UnitKind kind = UnitKind.Drone,
            ReportedStatus status = ReportedStatus.Normal, double lat = 35.0, double speed = 10.0,
            AltitudeMode altitude = AltitudeMode.Absolute)
        {
            return new TelemetrySample(id, lat, -83.0, 80.0, altitude, 90.0, speed, 0.0, kind, status);
        }

        [Test]
        public void Report_NewUnit_AddsAndRaisesUnitAdded()
        {
            var store = new UnitStateStore();
            var added = new List<string>();
            store.UnitAdded += s => added.Add(s.Id);

            store.Report(Sample("UAV-01"), Vector3.zero, 0.0);
            store.Report(Sample("UAV-01"), Vector3.zero, 0.1);

            Assert.AreEqual(1, store.Units.Count);
            CollectionAssert.AreEqual(new[] { "UAV-01" }, added);
        }

        [Test]
        public void Report_UpdatesValuesAndKind()
        {
            var store = new UnitStateStore();
            store.Report(Sample("TNK-01", UnitKind.Tank, speed: 8.0, lat: 35.5), new Vector3(1, 2, 3), 5.0);

            Assert.IsTrue(store.TryGet("TNK-01", out UnitState state));
            Assert.AreEqual(UnitKind.Tank, state.Kind);
            Assert.AreEqual(8.0, state.SpeedMps, 1e-9);
            Assert.AreEqual(35.5, state.Latitude, 1e-9);
            Assert.AreEqual(new Vector3(1, 2, 3), state.WorldPosition);
            Assert.AreEqual(5.0, state.LastReceivedTime, 1e-9);
        }

        [Test]
        public void Report_StoresAltitudeModeFromSample()
        {
            var store = new UnitStateStore();
            store.Report(Sample("UAV-01", altitude: AltitudeMode.HomeRelative), Vector3.zero, 0.0);
            store.Report(Sample("TNK-01", UnitKind.Tank, altitude: AltitudeMode.ClampToGround), Vector3.zero, 0.0);

            store.TryGet("UAV-01", out UnitState drone);
            store.TryGet("TNK-01", out UnitState tank);
            Assert.AreEqual(AltitudeMode.HomeRelative, drone.AltitudeMode);
            Assert.AreEqual(AltitudeMode.ClampToGround, tank.AltitudeMode);
        }

        [Test]
        public void Clear_RemovesUnitsAndRaisesCleared()
        {
            var store = new UnitStateStore();
            store.Report(Sample("UAV-01"), Vector3.zero, 0.0);
            store.Report(Sample("TNK-01", UnitKind.Tank), Vector3.zero, 0.0);
            int cleared = 0;
            store.Cleared += () => cleared++;

            store.Clear();

            Assert.AreEqual(0, store.Units.Count);
            Assert.IsFalse(store.TryGet("UAV-01", out _));
            Assert.AreEqual(1, cleared);
        }

        [Test]
        public void Clear_ReleasesSelectionAndPin()
        {
            var store = new UnitStateStore();
            store.Report(Sample("UAV-01"), Vector3.zero, 0.0);
            store.Report(Sample("UAV-02"), Vector3.zero, 0.0);
            store.Select("UAV-01");
            store.Pin("UAV-02");
            var selections = new List<string>();
            var pins = new List<string>();
            store.SelectionChanged += id => selections.Add(id ?? "<null>");
            store.PinnedChanged += id => pins.Add(id ?? "<null>");

            store.Clear();

            Assert.IsNull(store.SelectedId);
            Assert.IsNull(store.PinnedId);
            CollectionAssert.AreEqual(new[] { "<null>" }, selections);
            CollectionAssert.AreEqual(new[] { "<null>" }, pins);
        }

        [Test]
        public void Clear_WithoutSelection_DoesNotRaiseSelectionEvents()
        {
            var store = new UnitStateStore();
            store.Report(Sample("UAV-01"), Vector3.zero, 0.0);
            int raised = 0;
            store.SelectionChanged += _ => raised++;
            store.PinnedChanged += _ => raised++;

            store.Clear();

            Assert.AreEqual(0, raised);
        }

        [Test]
        public void Report_AfterClear_AddsUnitAgain()
        {
            var store = new UnitStateStore();
            store.Report(Sample("UAV-01"), Vector3.zero, 0.0);
            store.Clear();
            var added = new List<string>();
            store.UnitAdded += s => added.Add(s.Id);

            store.Report(Sample("UAV-01"), Vector3.zero, 1.0);

            CollectionAssert.AreEqual(new[] { "UAV-01" }, added);
            Assert.AreEqual(1, store.Units.Count);
            Assert.IsTrue(store.Select("UAV-01"));
        }

        [Test]
        public void Report_EmptyId_IsIgnored()
        {
            var store = new UnitStateStore();
            store.Report(Sample(""), Vector3.zero, 0.0);
            Assert.AreEqual(0, store.Units.Count);
        }

        [Test]
        public void Tick_WithinTimeout_StaysNormal()
        {
            var store = new UnitStateStore(2.0);
            store.Report(Sample("UAV-01"), Vector3.zero, 10.0);

            store.Tick(11.9);

            store.TryGet("UAV-01", out UnitState state);
            Assert.AreEqual(UnitStatus.Normal, state.Status);
        }

        [Test]
        public void Tick_AfterTimeout_MarksLostOnce()
        {
            var store = new UnitStateStore(2.0);
            int changes = 0;
            store.StatusChanged += _ => changes++;
            store.Report(Sample("UAV-01"), Vector3.zero, 10.0);

            store.Tick(12.5);
            store.Tick(13.0);

            store.TryGet("UAV-01", out UnitState state);
            Assert.AreEqual(UnitStatus.Lost, state.Status);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Report_AfterLost_Recovers()
        {
            var store = new UnitStateStore(2.0);
            store.Report(Sample("UAV-01"), Vector3.zero, 10.0);
            store.Tick(13.0);

            var changes = new List<UnitStatus>();
            store.StatusChanged += s => changes.Add(s.Status);
            store.Report(Sample("UAV-01"), Vector3.zero, 14.0);

            store.TryGet("UAV-01", out UnitState state);
            Assert.AreEqual(UnitStatus.Normal, state.Status);
            CollectionAssert.AreEqual(new[] { UnitStatus.Normal }, changes);
        }

        [Test]
        public void Report_WarningStatus_RaisesStatusChanged()
        {
            var store = new UnitStateStore();
            var changes = new List<UnitStatus>();
            store.StatusChanged += s => changes.Add(s.Status);

            store.Report(Sample("UAV-01"), Vector3.zero, 0.0);
            store.Report(Sample("UAV-01", status: ReportedStatus.Warning), Vector3.zero, 0.1);
            store.Report(Sample("UAV-01", status: ReportedStatus.Warning), Vector3.zero, 0.2);

            CollectionAssert.AreEqual(new[] { UnitStatus.Warning }, changes);
        }

        [Test]
        public void Tick_LostUnit_ReportedWarning_ComesBackAsWarning()
        {
            var store = new UnitStateStore(1.0);
            store.Report(Sample("UAV-01", status: ReportedStatus.Warning), Vector3.zero, 0.0);
            store.Tick(5.0);

            store.Report(Sample("UAV-01", status: ReportedStatus.Warning), Vector3.zero, 6.0);

            store.TryGet("UAV-01", out UnitState state);
            Assert.AreEqual(UnitStatus.Warning, state.Status);
        }

        [Test]
        public void Select_RegisteredUnit_SetsSelectionAndRaisesEvent()
        {
            var store = new UnitStateStore();
            store.Report(Sample("UAV-01"), Vector3.zero, 0.0);
            var events = new List<string>();
            store.SelectionChanged += id => events.Add(id);

            Assert.IsTrue(store.Select("UAV-01"));
            Assert.IsTrue(store.Select("UAV-01"));

            Assert.AreEqual("UAV-01", store.SelectedId);
            Assert.AreEqual("UAV-01", store.Selected.Id);
            CollectionAssert.AreEqual(new[] { "UAV-01" }, events);
        }

        [Test]
        public void Select_UnknownId_ReturnsFalseAndKeepsSelection()
        {
            var store = new UnitStateStore();
            store.Report(Sample("UAV-01"), Vector3.zero, 0.0);
            store.Select("UAV-01");

            Assert.IsFalse(store.Select("NOPE"));
            Assert.IsFalse(store.Select(null));

            Assert.AreEqual("UAV-01", store.SelectedId);
        }

        [Test]
        public void ClearSelection_RaisesEventWithNull()
        {
            var store = new UnitStateStore();
            store.Report(Sample("UAV-01"), Vector3.zero, 0.0);
            store.Select("UAV-01");
            var events = new List<string>();
            store.SelectionChanged += id => events.Add(id);

            store.ClearSelection();
            store.ClearSelection();

            Assert.IsNull(store.SelectedId);
            Assert.IsNull(store.Selected);
            Assert.AreEqual(1, events.Count);
            Assert.IsNull(events[0]);
        }

        [Test]
        public void TryGet_NullOrUnknown_ReturnsFalse()
        {
            var store = new UnitStateStore();
            Assert.IsFalse(store.TryGet(null, out _));
            Assert.IsFalse(store.TryGet("NOPE", out _));
        }
    }
}
