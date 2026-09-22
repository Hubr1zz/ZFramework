using HuntingInDarkness.ViewLayer.Tabletop;
using HuntingInDarkness.Hunt;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class TabletopEventLayoutTests
    {
        [Test]
        public void GetChoiceLocalPosition_CentersFullRow()
        {
            var first = TabletopEventLayout.GetChoiceLocalPosition(0, 4);
            var last = TabletopEventLayout.GetChoiceLocalPosition(3, 4);

            Assert.That(first.x, Is.EqualTo(-last.x).Within(0.001f));
            Assert.That(first.z, Is.EqualTo(last.z).Within(0.001f));
        }

        [Test]
        public void GetChoiceLocalPosition_CentersPartialRow()
        {
            var fifth = TabletopEventLayout.GetChoiceLocalPosition(4, 6);
            var sixth = TabletopEventLayout.GetChoiceLocalPosition(5, 6);

            Assert.That(fifth.x, Is.EqualTo(-sixth.x).Within(0.001f));
            Assert.That(fifth.z, Is.EqualTo(sixth.z).Within(0.001f));
        }

        [Test]
        public void GetChoiceLocalPosition_PlacesLaterRowsFartherFromPrimaryCard()
        {
            var first = TabletopEventLayout.GetChoiceLocalPosition(0, 5);
            var fifth = TabletopEventLayout.GetChoiceLocalPosition(4, 5);

            Assert.That(fifth.z, Is.LessThan(first.z));
        }

        [TestCase(-1, 1)]
        [TestCase(1, 1)]
        [TestCase(0, 0)]
        public void GetChoiceLocalPosition_RejectsInvalidRange(int index, int count)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => TabletopEventLayout.GetChoiceLocalPosition(index, count));
        }

        [Test]
        public void CameraFraming_HighPriorityPanelOverridesAndReleasesToBaseTable()
        {
            var baseOwner = new GameObject("BaseTableOwner");
            var panelOwner = new GameObject("PanelOwner");
            var baseBounds = new Bounds(new Vector3(1f, 0f, 2f), new Vector3(8f, 1f, 6f));
            var panelBounds = new Bounds(new Vector3(-3f, 0f, -4f), new Vector3(3f, 1f, 5f));

            TabletopCameraFraming.Request(baseOwner, baseBounds);
            TabletopCameraFraming.Request(panelOwner, panelBounds, 100);
            Assert.That(TabletopCameraFraming.TryGetActiveFrame(out Bounds active, out int priority), Is.True);
            Assert.That(active.center, Is.EqualTo(panelBounds.center));
            Assert.That(priority, Is.EqualTo(100));

            TabletopCameraFraming.Release(panelOwner);
            Assert.That(TabletopCameraFraming.TryGetActiveBounds(out active), Is.True);
            Assert.That(active.center, Is.EqualTo(baseBounds.center));

            TabletopCameraFraming.Release(baseOwner);
            Object.DestroyImmediate(panelOwner);
            Object.DestroyImmediate(baseOwner);
        }

        [Test]
        public void HuntCamera_ModalFrameStaysCenteredAndLocksMapNavigation()
        {
            var panelOwner = new GameObject("DistantEventPanel");
            var cameraObject = new GameObject("HuntCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var panelBounds = new Bounds(new Vector3(20f, 0f, 12f), new Vector3(5f, 1f, 4f));

            try
            {
                TabletopCameraFraming.Request(panelOwner, panelBounds, 100);
                HuntCameraController controller = cameraObject.AddComponent<HuntCameraController>();
                controller.RefreshFraming();
                Vector3 framedPosition = camera.transform.position;

                Assert.That(camera.orthographic, Is.False);
                Assert.That(camera.transform.position, Is.EqualTo(framedPosition));
                Assert.That(controller.NavigationLocked, Is.True);
                var centerRay = new Ray(camera.transform.position, camera.transform.forward);
                var ground = new Plane(Vector3.up, panelBounds.center);
                Assert.That(ground.Raycast(centerRay, out float distance), Is.True);
                Assert.That(centerRay.GetPoint(distance).x, Is.EqualTo(panelBounds.center.x).Within(0.01f));
                Assert.That(centerRay.GetPoint(distance).z, Is.EqualTo(panelBounds.center.z).Within(0.01f));
            }
            finally
            {
                TabletopCameraFraming.Release(panelOwner);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(panelOwner);
            }
        }
    }
}
