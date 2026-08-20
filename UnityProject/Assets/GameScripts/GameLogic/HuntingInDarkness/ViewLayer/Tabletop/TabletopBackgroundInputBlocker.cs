using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>在桌面模态交互期间暂时冻结已有实体 Collider；租约结束后只恢复原本启用的对象。</summary>
    public sealed class TabletopBackgroundInputBlocker : IDisposable
    {
        private readonly List<Collider> colliders;
        private bool disposed;

        private TabletopBackgroundInputBlocker(List<Collider> colliders) => this.colliders = colliders;

        public int BlockedCount => colliders.Count;

        public static TabletopBackgroundInputBlocker Capture() => Capture(UnityEngine.Object.FindObjectsByType<Collider>());

        public static TabletopBackgroundInputBlocker Capture(IEnumerable<Collider> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var captured = new List<Collider>();
            foreach (Collider candidate in candidates)
            {
                if (candidate == null || !candidate.enabled) continue;
                candidate.enabled = false;
                captured.Add(candidate);
            }
            return new TabletopBackgroundInputBlocker(captured);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Collider collider in colliders)
                if (collider != null) collider.enabled = true;
            colliders.Clear();
        }
    }
}
