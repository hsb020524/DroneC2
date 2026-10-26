using DroneC2.View;
using NUnit.Framework;
using UnityEngine;

namespace DroneC2.Tests
{
    public class DroneViewTests
    {
        GameObject go;
        DroneView view;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Drone");
            view = go.AddComponent<DroneView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        // 자식 표시(모델·경로선·마커)를 만들어 DroneView 아래에 둔다. 컴포넌트 추가 시점에 Awake가 돈다.
        DroneView CreateWithChildren(out MeshRenderer model, out LineRenderer line, out MeshRenderer alreadyOff)
        {
            var root = new GameObject("Unit");
            var m = new GameObject("Body");
            m.transform.SetParent(root.transform);
            model = m.AddComponent<MeshRenderer>();
            var l = new GameObject("PathTrail");
            l.transform.SetParent(root.transform);
            line = l.AddComponent<LineRenderer>();
            var o = new GameObject("Off");
            o.transform.SetParent(root.transform);
            alreadyOff = o.AddComponent<MeshRenderer>();
            alreadyOff.enabled = false;
            DroneView v = root.AddComponent<DroneView>();
            // EditMode에서는 AddComponent가 Awake를 부르지 않으므로 Play 모드와 같게 직접 호출한다.
            typeof(DroneView).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(v, null);
            extra = root;
            return v;
        }

        GameObject extra;

        [Test]
        public void ChildRenderers_AreHidden_BeforeFirstTarget_AndShownAfter()
        {
            DroneView v = CreateWithChildren(out MeshRenderer model, out LineRenderer line, out MeshRenderer off);
            try
            {
                Assert.IsFalse(model.enabled);
                Assert.IsFalse(line.enabled);

                v.SetTarget(Vector3.one, Quaternion.identity);

                Assert.IsTrue(model.enabled);
                Assert.IsTrue(line.enabled);
                Assert.IsFalse(off.enabled, "원래 꺼져 있던 렌더러는 켜지지 않는다.");
            }
            finally
            {
                Object.DestroyImmediate(extra);
            }
        }

        [Test]
        public void SecondTarget_DoesNotReenableRenderersDisabledLater()
        {
            DroneView v = CreateWithChildren(out MeshRenderer model, out _, out _);
            try
            {
                v.SetTarget(Vector3.one, Quaternion.identity);
                model.enabled = false; // 다른 코드가 끈 경우
                v.SetTarget(Vector3.zero, Quaternion.identity);

                Assert.IsFalse(model.enabled);
            }
            finally
            {
                Object.DestroyImmediate(extra);
            }
        }

        [Test]
        public void ResetPlacement_HidesRenderersAgain_AndClearsHasTarget()
        {
            DroneView v = CreateWithChildren(out MeshRenderer model, out LineRenderer line, out MeshRenderer off);
            try
            {
                v.SetTarget(Vector3.one, Quaternion.identity);
                Assert.IsTrue(model.enabled);

                v.ResetPlacement();

                Assert.IsFalse(v.HasTarget);
                Assert.IsFalse(model.enabled);
                Assert.IsFalse(line.enabled);
                Assert.IsFalse(off.enabled);

                v.SetTarget(Vector3.zero, Quaternion.identity);

                Assert.IsTrue(model.enabled);
                Assert.IsTrue(line.enabled);
                Assert.IsFalse(off.enabled, "원래 꺼져 있던 렌더러는 초기화 뒤에도 켜지지 않는다.");
            }
            finally
            {
                Object.DestroyImmediate(extra);
            }
        }

        [Test]
        public void ResetPlacement_NextTarget_PlacesImmediately()
        {
            view.SetTarget(new Vector3(100f, 0f, 0f), Quaternion.identity);
            view.ResetPlacement();

            var position = new Vector3(-5f, 7f, 9f);
            view.SetTarget(position, Quaternion.identity);

            Assert.IsTrue(view.HasTarget);
            Assert.AreEqual(0f, (go.transform.position - position).magnitude, 1e-4f);
        }

        [Test]
        public void ResetPlacement_RaisesPlacementReset()
        {
            int raised = 0;
            view.PlacementReset += () => raised++;

            view.ResetPlacement();

            Assert.AreEqual(1, raised);
        }

        [Test]
        public void ResetPlacement_ClearsPathTrail()
        {
            var root = new GameObject("Unit");
            try
            {
                DroneView v = root.AddComponent<DroneView>();
                var trailObject = new GameObject("PathTrail");
                trailObject.transform.SetParent(root.transform);
                LineRenderer line = trailObject.AddComponent<LineRenderer>();
                PathTrail trail = trailObject.AddComponent<PathTrail>();

                var so = new UnityEditor.SerializedObject(trail);
                so.FindProperty("target").objectReferenceValue = root.transform;
                so.ApplyModifiedPropertiesWithoutUndo();

                // EditMode에서는 Awake·OnEnable이 자동으로 돌지 않으므로 직접 호출한다.
                const System.Reflection.BindingFlags flags =
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(DroneView).GetMethod("Awake", flags).Invoke(v, null);
                typeof(PathTrail).GetMethod("Awake", flags).Invoke(trail, null);
                typeof(PathTrail).GetMethod("OnEnable", flags).Invoke(trail, null);

                v.SetTarget(Vector3.zero, Quaternion.identity);
                typeof(PathTrail).GetMethod("LateUpdate", flags).Invoke(trail, null);
                Assert.AreEqual(1, line.positionCount);

                v.ResetPlacement();

                Assert.AreEqual(0, line.positionCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HasTarget_IsFalse_BeforeFirstTarget()
        {
            Assert.IsFalse(view.HasTarget);
        }

        [Test]
        public void FirstTarget_PlacesImmediately_AndSetsHasTarget()
        {
            var position = new Vector3(10f, 20f, 30f);
            view.SetTarget(position, Quaternion.Euler(0f, 90f, 0f));

            Assert.IsTrue(view.HasTarget);
            Assert.AreEqual(0f, (go.transform.position - position).magnitude, 1e-4f);
        }
    }
}
