using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>屏幕模态层的可嵌套输入租约；关闭当帧直到鼠标释放前仍保持阻断。</summary>
    public static class ScreenModalInputGate
    {
        private static readonly HashSet<int> leases = new();
        private static int nextLeaseId = 1;
        private static int releaseFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            leases.Clear();
            nextLeaseId = 1;
            releaseFrame = -1;
        }

        public static bool IsBlocked
        {
            get
            {
                if (leases.Count > 0) return true;
                if (releaseFrame < 0) return false;
                if (Time.frameCount <= releaseFrame || Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2) || Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2)) return true;
                releaseFrame = -1;
                return false;
            }
        }

        public static IDisposable Acquire(UnityEngine.Object owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
#if UNITY_6000_0_OR_NEWER
            return Acquire(owner.GetEntityId().GetHashCode());
#else
            return Acquire(owner.GetInstanceID());
#endif
        }

        public static IDisposable Acquire(int ownerId)
        {
            int leaseId = nextLeaseId++;
            if (leaseId == 0) leaseId = nextLeaseId++;
            leases.Add(leaseId);
            releaseFrame = -1;
            return new Lease(leaseId);
        }

        private static void Release(int leaseId)
        {
            if (!leases.Remove(leaseId)) return;
            if (leases.Count == 0) releaseFrame = Time.frameCount;
        }

        private sealed class Lease : IDisposable
        {
            private readonly int ownerId;
            private bool disposed;

            public Lease(int ownerId) => this.ownerId = ownerId;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                Release(ownerId);
            }
        }
    }
}
