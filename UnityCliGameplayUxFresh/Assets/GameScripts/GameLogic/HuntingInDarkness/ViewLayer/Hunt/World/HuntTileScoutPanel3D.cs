using System;
using System.Collections.Generic;
using HuntingInDarkness.ViewLayer.Tabletop;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>Interactable 地块的世界空间侦察确认卡；不直接读取或修改 Hunt 权威状态。</summary>
    public sealed class HuntTileScoutPanel3D : MonoBehaviour
    {
        private TabletopEventPanel3D panel;
        private Action<Vector2Int> confirm;
        private Action<Vector2Int> cancel;
        private Vector2Int coordinate;
        private int inputOwnerId;
        private bool holdsInputGuard;

        public bool IsOpen => panel != null && panel.IsOpen;
        public Vector2Int Coordinate => coordinate;
        public TabletopEventPanel3D ActivePanel => panel;

        public static HuntTileScoutPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("HuntTileScoutPanel3D");
            gameObject.transform.SetParent(parent, false);
            HuntTileScoutPanel3D result = gameObject.AddComponent<HuntTileScoutPanel3D>();
            gameObject.SetActive(false);
            return result;
        }

        public void Present(Vector3 worldPosition, Vector2Int targetCoordinate, string tileName, Action<Vector2Int> confirmAction, Action<Vector2Int> cancelAction, string status = null)
        {
            Close();
            coordinate = targetCoordinate;
            confirm = confirmAction;
            cancel = cancelAction;
            AcquireInputGuard();
            gameObject.SetActive(true);
            panel ??= TabletopEventPanel3D.Create(transform);
            bool hasFailure = !string.IsNullOrWhiteSpace(status);
            panel.Present(worldPosition, "侦察地块", tileName ?? "未知地块", hasFailure ? status : "只显示地块名称；确认后翻开", hasFailure ? TabletopEventPrimaryTone.Failure : TabletopEventPrimaryTone.Check, new List<TabletopEventChoicePresentation>
            {
                new("翻开地块", "确认侦察并翻开这张地块卡。", true, string.Empty, Confirm),
                new("取消", "保持地图状态不变。", true, string.Empty, Cancel)
            });
        }

        public void Close()
        {
            panel?.Close();
            ReleaseInputGuard();
            confirm = null;
            cancel = null;
        }

        private void Confirm()
        {
            Action<Vector2Int> callback = confirm;
            Vector2Int target = coordinate;
            Close();
            callback?.Invoke(target);
        }

        private void Cancel()
        {
            Action<Vector2Int> callback = cancel;
            Vector2Int target = coordinate;
            Close();
            callback?.Invoke(target);
        }

        private void AcquireInputGuard()
        {
            if (holdsInputGuard) return;
            if (inputOwnerId == 0)
            {
#if UNITY_6000_5_OR_NEWER
                inputOwnerId = GetEntityId().GetHashCode();
#else
                inputOwnerId = GetInstanceID();
#endif
                if (inputOwnerId == 0) inputOwnerId = int.MaxValue;
            }
            PlayableHuntInputGuard.Acquire(inputOwnerId);
            holdsInputGuard = true;
        }

        private void ReleaseInputGuard()
        {
            if (!holdsInputGuard) return;
            PlayableHuntInputGuard.Release(inputOwnerId);
            holdsInputGuard = false;
        }

        private void OnDisable() => Close();

        private void OnDestroy()
        {
            Close();
            panel = null;
        }
    }
}
