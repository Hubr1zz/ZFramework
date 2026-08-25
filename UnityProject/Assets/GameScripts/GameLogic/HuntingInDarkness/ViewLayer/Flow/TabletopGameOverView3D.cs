using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ViewLayer.Tabletop;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Flow
{
    /// <summary>跨阶段终局桌面；冻结既有实体交互，并用世界空间卡牌提供重新开始入口。</summary>
    public sealed class TabletopGameOverView3D : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float tabletopHeight = 0.38f;
        [SerializeField, Min(1f)] private float fallbackDistance = 8f;

        private TabletopEventPanel3D panel;
        private readonly List<TabletopBackgroundInputBlocker> inputBlockers = new();
        private readonly HashSet<Collider> blockedColliders = new();

        private bool restartInFlight;

        public System.Func<UniTask<CampaignRestartResult>> RestartCommand;
        public bool IsOpen => panel != null && panel.IsOpen;
        public int BlockedColliderCount => blockedColliders.Count;

        public void Show(string reason)
        {
            Hide();
            CaptureBackgroundColliders();
            panel ??= TabletopEventPanel3D.Create(transform);
            var choices = new[]
            {
                new TabletopEventChoicePresentation("重写年鉴", "舍弃这段战役记录，从第一次苏醒重新开始。", true, "点击开始新战役", Restart)
            };
            string body = string.IsNullOrWhiteSpace(reason) ? "全部猎人已经倒下。" : reason;
            panel.Present(ResolveTabletopAnchor(), "黑暗吞噬一切", body, "这段故事已经结束，但营火仍可再次点亮", TabletopEventPrimaryTone.Failure, choices);
        }

        public void Hide()
        {
            panel?.Close();
            for (int index = inputBlockers.Count - 1; index >= 0; index--)
                inputBlockers[index].Dispose();
            inputBlockers.Clear();
            blockedColliders.Clear();
        }

        public void Restart()
        {
            if (!IsOpen || restartInFlight) return;
            RestartAsync().Forget();
        }

        private async UniTaskVoid RestartAsync()
        {
            restartInFlight = true;
            CampaignRestartResult result;
            try
            {
                result = RestartCommand != null ? await RestartCommand() : CampaignRestartResult.Failed("重新开始命令尚未接入。");
            }
            catch (System.Exception exception)
            {
                result = CampaignRestartResult.Failed($"重新开始失败：{exception.Message}");
            }
            finally
            {
                restartInFlight = false;
            }

            if (this == null) return;
            if (result.Succeeded)
            {
                Hide();
                return;
            }
            Show(result.Reason);
        }

        private void LateUpdate()
        {
            if (IsOpen)
                CaptureBackgroundColliders();
        }

        private void CaptureBackgroundColliders()
        {
            var candidates = new List<Collider>();
            foreach (Collider collider in Object.FindObjectsByType<Collider>())
            {
                if (collider == null || panel != null && collider.transform.IsChildOf(panel.transform))
                    continue;
                if (blockedColliders.Contains(collider))
                {
                    if (collider.enabled)
                        collider.enabled = false;
                    continue;
                }
                if (!collider.enabled)
                    continue;
                blockedColliders.Add(collider);
                candidates.Add(collider);
            }
            if (candidates.Count > 0)
                inputBlockers.Add(TabletopBackgroundInputBlocker.Capture(candidates));
        }

        private Vector3 ResolveTabletopAnchor()
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null)
                return transform.position;

            var tabletopPlane = new Plane(Vector3.up, new Vector3(0f, tabletopHeight, 0f));
            Ray viewRay = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            if (tabletopPlane.Raycast(viewRay, out float distance) && distance > 0f)
                return viewRay.GetPoint(distance);
            return camera.transform.position + camera.transform.forward * fallbackDistance;
        }

        private void OnDisable() => Hide();
        private void OnDestroy() => Hide();
    }
}
