using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.ViewLayer.Tabletop;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Hunt
{
    /// <summary>地图边缘的实体回营卡与确认桌面；只提交请求，不写入狩猎或营地状态。</summary>
    public sealed class HuntRetreatPanel3D : MonoBehaviour
    {
        private const float MapEdgeOffset = 1.8f;
        private const float CardHeight = 0.38f;
        private static int nextInputOwnerId;
        private IPlayableHuntRetreatInput input;
        private HuntManager manager;
        private bool confirmationOpen;
        private bool requestInFlight;
        private int inputOwnerId;
        private bool holdsInputGuard;

        public bool IsConfirmationOpen => confirmationOpen;
        public bool IsRequestInFlight => requestInFlight;
        public int ActiveCardCount => transform.Cast<Transform>().Count(child => child.gameObject.activeSelf);

        public static HuntRetreatPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("HuntRetreatPanel3D");
            gameObject.transform.SetParent(parent, false);
            return gameObject.AddComponent<HuntRetreatPanel3D>();
        }

        public void Initialize(IPlayableHuntRetreatInput retreatInput, HuntManager huntManager)
        {
            input = retreatInput;
            manager = huntManager;
            requestInFlight = false;
            PresentLauncher();
        }

        public void RequestOpen()
        {
            if (confirmationOpen || requestInFlight || input == null || manager == null || PlayableHuntInputGuard.IsBlocked)
                return;

            confirmationOpen = true;
            AcquireInputGuard();
            PresentConfirmation(string.Empty);
        }

        public void RequestClose()
        {
            if (requestInFlight || input?.IsReturnCheckpointLocked == true)
                return;
            PresentLauncher();
        }

        private void PresentLauncher()
        {
            confirmationOpen = false;
            ReleaseInputGuard();
            ClearCards();
            transform.localPosition = GetLauncherPosition();
            TabletopEventChoiceCard3D launcher = TabletopEventChoiceCard3D.Create(transform, Vector3.zero);
            launcher.Present("收队回营", "结束本次探索并结算带回的素材。", true, "点击查看结算", RequestOpen);
        }

        private void PresentConfirmation(string status)
        {
            ClearCards();
            transform.localPosition = GetLauncherPosition();
            int hunterCount = manager.ActiveHunters?.Count ?? 0;
            int lostCount = manager.ActiveHunters?.Count(hunter => hunter == null || !hunter.IsAlive) ?? 0;
            HuntCollectiblePresentation collectibles = HuntCollectiblePresentation.Create(manager.ActiveHunters?.Where(hunter => hunter != null).SelectMany(hunter => hunter.Collectibles ?? Enumerable.Empty<ItemInstance>()));
            TabletopEventPrimaryCard3D primary = TabletopEventPrimaryCard3D.Create(transform);
            primary.MoveTo(new Vector3(0f, 0f, 1.75f));
            primary.Present("返回营地？", $"出发猎人 · {hunterCount}\n失去猎人 · {lostCount}\n携带素材 · {collectibles.TotalCount}\n{collectibles.Summary}\n\n回营后将结算收获并推进营地流程。", string.IsNullOrWhiteSpace(status) ? "确认前仍可继续探索" : status, string.IsNullOrWhiteSpace(status) ? TabletopEventPrimaryTone.Narrative : TabletopEventPrimaryTone.Failure);

            TabletopEventChoiceCard3D confirm = TabletopEventChoiceCard3D.Create(transform, new Vector3(-0.82f, 0f, -0.55f));
            confirm.Present("结算并回营", "将采集物转入营地，结束本次狩猎。", !requestInFlight, requestInFlight ? "正在结算" : "点击确认", ConfirmAsync);
            TabletopEventChoiceCard3D cancel = TabletopEventChoiceCard3D.Create(transform, new Vector3(0.82f, 0f, -0.55f));
            bool canContinue = !requestInFlight && input?.IsReturnCheckpointLocked != true;
            cancel.Present("继续探索", "收起回营卡，保留当前狩猎进度。", canContinue, canContinue ? string.Empty : "检查点已锁定", RequestClose);
        }

        private void ConfirmAsync()
        {
            if (requestInFlight || input == null)
                return;
            ExecuteRetreatAsync().Forget();
        }

        private async UniTaskVoid ExecuteRetreatAsync()
        {
            requestInFlight = true;
            PresentConfirmation("正在等待狩猎与战役流程提交……");
            HuntRetreatCommandResult result;
            try
            {
                result = await input.RequestRetreatAsync();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                result = HuntRetreatCommandResult.Failed("回营流程异常，请重试。");
            }
            if (this == null)
                return;

            requestInFlight = false;
            if (!result.Succeeded)
            {
                PresentConfirmation(result.Reason);
                return;
            }
            confirmationOpen = false;
            ReleaseInputGuard();
            ClearCards();
        }

        private void AcquireInputGuard()
        {
            if (holdsInputGuard)
                return;
            if (inputOwnerId == 0)
                inputOwnerId = -Interlocked.Increment(ref nextInputOwnerId);
            PlayableHuntInputGuard.Acquire(inputOwnerId);
            holdsInputGuard = true;
        }

        private Vector3 GetLauncherPosition()
        {
            if (manager?.Map == null || manager.Map.Count == 0)
                return new Vector3(-MapEdgeOffset, CardHeight, 0f);

            float minimumX = float.MaxValue;
            float minimumZ = float.MaxValue;
            float maximumZ = float.MinValue;
            foreach (Vector2Int coordinate in manager.Map.Keys)
            {
                Vector3 position = manager.TileToWorld(coordinate);
                minimumX = Mathf.Min(minimumX, position.x);
                minimumZ = Mathf.Min(minimumZ, position.z);
                maximumZ = Mathf.Max(maximumZ, position.z);
            }
            Vector3 worldPosition = new(minimumX - MapEdgeOffset, CardHeight, (minimumZ + maximumZ) * 0.5f);
            return transform.parent != null ? transform.parent.InverseTransformPoint(worldPosition) : worldPosition;
        }

        private void ReleaseInputGuard()
        {
            if (!holdsInputGuard)
                return;
            PlayableHuntInputGuard.Release(inputOwnerId);
            holdsInputGuard = false;
        }

        private void ClearCards()
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private void OnDisable()
        {
            confirmationOpen = false;
            requestInFlight = false;
            ReleaseInputGuard();
            ClearCards();
        }

        private void OnDestroy() => ReleaseInputGuard();
    }
}
