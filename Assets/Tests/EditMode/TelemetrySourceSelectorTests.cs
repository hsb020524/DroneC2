using System.Collections.Generic;
using DroneC2.Core;
using DroneC2.DataSource;
using DroneC2.DataSource.Mavlink;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DroneC2.Tests
{
    public class TelemetrySourceSelectorTests
    {
        readonly List<Object> created = new List<Object>();

        GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null)
                    Object.DestroyImmediate(o);
            }
            created.Clear();
        }

        // 바인더 + 두 소스가 한 GameObject에 있는 유닛을 만든다.
        (UnitTelemetryBinder binder, FakeGpsSource fake, MavlinkUnitSource mavlink) NewUnit(string name, bool withMavlink = true)
        {
            GameObject go = NewObject(name);
            var fake = go.AddComponent<FakeGpsSource>();
            MavlinkUnitSource mavlink = withMavlink ? go.AddComponent<MavlinkUnitSource>() : null;
            var binder = go.AddComponent<UnitTelemetryBinder>();

            // 프리팹과 같은 초기 상태: 가짜 GPS 연결, MAVLink 소스는 꺼 둔다.
            var so = new SerializedObject(binder);
            so.FindProperty("sourceBehaviour").objectReferenceValue = fake;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (mavlink != null)
                mavlink.enabled = false;
            return (binder, fake, mavlink);
        }

        TelemetrySourceSelector NewSelector(TelemetrySourceMode mode, MavlinkReceiver receiver)
        {
            var selector = NewObject("Selector").AddComponent<TelemetrySourceSelector>();
            var so = new SerializedObject(selector);
            so.FindProperty("mode").enumValueIndex = (int)mode;
            so.FindProperty("receiver").objectReferenceValue = receiver;
            so.ApplyModifiedPropertiesWithoutUndo();
            return selector;
        }

        MavlinkReceiver NewReceiver(bool enabled)
        {
            var receiver = NewObject("Receiver").AddComponent<MavlinkReceiver>();
            receiver.enabled = enabled;
            return receiver;
        }

        [Test]
        public void FakeGpsMode_KeepsFakeSource_AndLeavesReceiverOff()
        {
            var unit = NewUnit("UAV-01");
            MavlinkReceiver receiver = NewReceiver(false);
            TelemetrySourceSelector selector = NewSelector(TelemetrySourceMode.FakeGps, receiver);

            selector.Apply();

            Assert.IsTrue(unit.fake.enabled);
            Assert.IsFalse(unit.mavlink.enabled);
            Assert.AreSame(unit.fake, unit.binder.CurrentSource);
            Assert.IsFalse(receiver.enabled);
        }

        [Test]
        public void MavlinkMode_SwitchesEveryUnit_AndEnablesReceiver()
        {
            var a = NewUnit("UAV-01");
            var b = NewUnit("TNK-01");
            MavlinkReceiver receiver = NewReceiver(false);
            TelemetrySourceSelector selector = NewSelector(TelemetrySourceMode.Mavlink, receiver);

            selector.Apply();

            Assert.IsFalse(a.fake.enabled);
            Assert.IsTrue(a.mavlink.enabled);
            Assert.AreSame(a.mavlink, a.binder.CurrentSource);
            Assert.IsFalse(b.fake.enabled);
            Assert.IsTrue(b.mavlink.enabled);
            Assert.AreSame(b.mavlink, b.binder.CurrentSource);
            Assert.IsTrue(receiver.enabled);
        }

        [Test]
        public void MavlinkMode_WithoutReceiver_FallsBackToFakeGps()
        {
            var unit = NewUnit("UAV-01");
            TelemetrySourceSelector selector = NewSelector(TelemetrySourceMode.Mavlink, null);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("수신기"));
            selector.Apply();

            Assert.IsTrue(unit.fake.enabled);
            Assert.IsFalse(unit.mavlink.enabled);
            Assert.AreSame(unit.fake, unit.binder.CurrentSource);
        }

        [Test]
        public void MavlinkMode_UnitWithoutMavlinkSource_IsLeftOnFakeAndReported()
        {
            var unit = NewUnit("UAV-01", withMavlink: false);
            MavlinkReceiver receiver = NewReceiver(false);
            TelemetrySourceSelector selector = NewSelector(TelemetrySourceMode.Mavlink, receiver);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("MavlinkUnitSource"));
            selector.Apply();

            Assert.IsTrue(unit.fake.enabled);
            Assert.AreSame(unit.fake, unit.binder.CurrentSource);
        }

        [Test]
        public void SwitchingBack_RestoresFakeSource()
        {
            var unit = NewUnit("UAV-01");
            MavlinkReceiver receiver = NewReceiver(false);
            TelemetrySourceSelector selector = NewSelector(TelemetrySourceMode.Mavlink, receiver);
            selector.Apply();

            var so = new SerializedObject(selector);
            so.FindProperty("mode").enumValueIndex = (int)TelemetrySourceMode.FakeGps;
            so.ApplyModifiedPropertiesWithoutUndo();
            selector.Apply();

            Assert.IsTrue(unit.fake.enabled);
            Assert.IsFalse(unit.mavlink.enabled);
            Assert.AreSame(unit.fake, unit.binder.CurrentSource);
            Assert.IsFalse(receiver.enabled);
        }

        [Test]
        public void Binder_SetSource_RejectsNonSourceComponent()
        {
            var unit = NewUnit("UAV-01");
            var notASource = NewObject("Other").AddComponent<UnitRegistry>();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("ITelemetrySource"));
            unit.binder.SetSource(notASource);

            Assert.AreSame(unit.fake, unit.binder.CurrentSource);
        }
    }
}
