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
