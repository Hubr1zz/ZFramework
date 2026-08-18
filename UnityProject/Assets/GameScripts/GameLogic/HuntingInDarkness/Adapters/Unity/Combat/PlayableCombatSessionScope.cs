using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    /// <summary>每场决战独占的 Unity 对象与订阅清理边界。</summary>
    public sealed class PlayableCombatSessionScope : IDisposable
    {
        private readonly List<Action> cleanupActions = new();
        private bool disposed;

        public GameObject Root { get; }

        public PlayableCombatSessionScope(Transform parent)
        {
            Root = new GameObject("CombatSession");
            Root.transform.SetParent(parent, false);
        }

        public void RegisterCleanup(Action cleanup)
        {
            if (cleanup == null) return;
            if (disposed)
                throw new ObjectDisposedException(nameof(PlayableCombatSessionScope));
            cleanupActions.Add(cleanup);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            for (int index = cleanupActions.Count - 1; index >= 0; index--)
            {
                try
                {
                    cleanupActions[index]();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
            cleanupActions.Clear();
            if (Root == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(Root);
            else
                UnityEngine.Object.DestroyImmediate(Root);
        }
    }
}
