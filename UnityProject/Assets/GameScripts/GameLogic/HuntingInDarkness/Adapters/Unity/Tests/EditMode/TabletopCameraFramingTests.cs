using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class TabletopCameraFramingTests
    {
        [TestCase(16f / 9f, 20f, 0.4f, 2f, 0.01f)]
        [TestCase(4f / 3f, 20f, 0.4f, 2f, 0.01f)]
        [TestCase(16f / 9f, 8f, 8f, 2f, 0.01f)]
        [TestCase(16f / 9f, 2f, 2f, 2f, 0.001f)]
        public void CalculatePerspectivePose_FitsAllBoundsCornersAndAvoidsSphereOverFraming(float aspect, float width, float height, float depth, float nearClipPlane)
        {
            GameObject cameraObject = new GameObject("TabletopCameraFramingTestCamera");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.aspect = aspect;
                camera.fieldOfView = 42f;
                camera.nearClipPlane = nearClipPlane;
                Bounds bounds = new Bounds(Vector3.zero, new Vector3(width, height, depth));
                const float padding = 1.15f;
                const float minimumDistance = 4f;

                TabletopCameraFraming.CalculatePerspectivePose(camera, bounds, 55f, 0f, padding, minimumDistance, out Vector3 position, out Quaternion rotation);
                float distance = Vector3.Distance(position, bounds.center);
                float oldSphereDistance = CalculateOldSphereDistance(camera, bounds, padding, minimumDistance);
                camera.transform.SetPositionAndRotation(position, rotation);

                Assert.That(distance, Is.GreaterThanOrEqualTo(minimumDistance));
                Assert.That(distance, Is.LessThan(oldSphereDistance));
                if (width >= 8f || height >= 8f)
                    Assert.That(distance, Is.LessThan(oldSphereDistance * 0.9f));

                foreach (Vector3 corner in GetBoundsCorners(bounds))
                {
                    Vector3 viewportPoint = camera.WorldToViewportPoint(corner);
                    Assert.That(viewportPoint.z, Is.GreaterThan(camera.nearClipPlane));
                    Assert.That(viewportPoint.x, Is.InRange(0f, 1f));
                    Assert.That(viewportPoint.y, Is.InRange(0f, 1f));
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void Request_ReusesUnchangedActiveRequestWithoutBroadcasting()
        {
            GameObject owner = new GameObject("TabletopCameraFramingRequestOwner");
            int changedCount = 0;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
            try
            {
                TabletopCameraFraming.Changed += OnChanged;
                TabletopCameraFraming.Request(owner, bounds, 0);
                TabletopCameraFraming.Request(owner, bounds, 0);
                Assert.That(changedCount, Is.EqualTo(1));

                TabletopCameraFraming.Request(owner, new Bounds(Vector3.right, Vector3.one), 0);
                Assert.That(changedCount, Is.EqualTo(2));
            }
            finally
            {
                TabletopCameraFraming.Changed -= OnChanged;
                TabletopCameraFraming.Release(owner);
                Object.DestroyImmediate(owner);
            }

            void OnChanged() => changedCount++;
        }

        private static float CalculateOldSphereDistance(Camera camera, Bounds bounds, float padding, float minimumDistance)
        {
            float verticalHalfAngle = Mathf.Clamp(camera.fieldOfView * 0.5f, 5f, 80f) * Mathf.Deg2Rad;
            float horizontalHalfAngle = Mathf.Atan(Mathf.Tan(verticalHalfAngle) * Mathf.Max(0.1f, camera.aspect));
            float limitingHalfAngle = Mathf.Max(5f * Mathf.Deg2Rad, Mathf.Min(verticalHalfAngle, horizontalHalfAngle));
            float radius = Mathf.Max(0.5f, bounds.extents.magnitude) * Mathf.Max(1f, padding);
            return Mathf.Max(minimumDistance, radius / Mathf.Sin(limitingHalfAngle));
        }

        private static Vector3[] GetBoundsCorners(Bounds bounds)
        {
            Vector3 extents = bounds.extents;
            Vector3[] corners = new Vector3[8];
            int index = 0;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                corners[index++] = bounds.center + Vector3.Scale(extents, new Vector3(x, y, z));
            return corners;
        }
    }
}
