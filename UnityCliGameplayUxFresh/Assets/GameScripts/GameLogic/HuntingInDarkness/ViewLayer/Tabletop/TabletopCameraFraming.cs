using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>集中维护基础桌面与临时事件面板的取景范围；高优先级请求会暂时覆盖基础桌面。</summary>
    public static class TabletopCameraFraming
    {
        private sealed class FrameRequest
        {
            public UnityEngine.Object Owner;
            public Bounds Bounds;
            public int Priority;
            public long Order;
        }

        private static readonly List<FrameRequest> requests = new();
        private static long nextOrder;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            requests.Clear();
            nextOrder = 0;
            Changed = null;
        }

        public static void Request(UnityEngine.Object owner, Bounds bounds, int priority = 0)
        {
            if (owner == null || !IsFinite(bounds)) return;
            FrameRequest request = requests.Find(candidate => candidate.Owner == owner);
            if (request == null)
            {
                request = new FrameRequest { Owner = owner };
                requests.Add(request);
            }
            request.Bounds = bounds;
            request.Priority = priority;
            request.Order = ++nextOrder;
            Changed?.Invoke();
        }

        public static void Release(UnityEngine.Object owner)
        {
            if (owner == null) return;
            bool removed = requests.RemoveAll(request => request.Owner == null || request.Owner == owner) > 0;
            if (removed)
                Changed?.Invoke();
        }

        public static bool TryGetActiveBounds(out Bounds bounds)
        {
            return TryGetActiveFrame(out bounds, out _);
        }

        public static bool TryGetActiveFrame(out Bounds bounds, out int priority)
        {
            requests.RemoveAll(request => request.Owner == null);
            FrameRequest selected = null;
            foreach (FrameRequest request in requests)
                if (selected == null || request.Priority > selected.Priority || request.Priority == selected.Priority && request.Order > selected.Order)
                    selected = request;
            bounds = selected?.Bounds ?? default;
            priority = selected?.Priority ?? 0;
            return selected != null;
        }

        public static bool TryCalculateWorldBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null) return false;
            bool hasBounds = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer == null || !renderer.enabled) continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }
                bounds.Encapsulate(renderer.bounds);
            }
            return hasBounds && IsFinite(bounds);
        }

        public static void CalculatePerspectivePose(UnityEngine.Camera camera, Bounds bounds, float pitch, float yaw, float padding, float minimumDistance, out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.Euler(Mathf.Clamp(pitch, 20f, 80f), yaw, 0f);
            float verticalHalfAngle = Mathf.Clamp(camera.fieldOfView * 0.5f, 5f, 80f) * Mathf.Deg2Rad;
            float horizontalHalfAngle = Mathf.Atan(Mathf.Tan(verticalHalfAngle) * Mathf.Max(0.1f, camera.aspect));
            float limitingHalfAngle = Mathf.Max(5f * Mathf.Deg2Rad, Mathf.Min(verticalHalfAngle, horizontalHalfAngle));
            float radius = Mathf.Max(0.5f, bounds.extents.magnitude) * Mathf.Max(1f, padding);
            float distance = Mathf.Max(minimumDistance, radius / Mathf.Sin(limitingHalfAngle));
            position = bounds.center - rotation * Vector3.forward * distance;
        }

        private static bool IsFinite(Bounds bounds) => IsFinite(bounds.center) && IsFinite(bounds.size) && bounds.size.sqrMagnitude > 0.0001f;
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
