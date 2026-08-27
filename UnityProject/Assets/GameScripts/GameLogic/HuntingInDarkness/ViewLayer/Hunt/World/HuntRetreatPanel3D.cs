using System.Collections.Generic;
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
        private string selectedAbandonedResourceId = string.Empty;

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
            selectedAbandonedResourceId = string.Empty;
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
            selectedAbandonedResourceId = string.Empty;
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
            HuntRetreatPreview preview = input?.GetRetreatPreview() ?? HuntRetreatPreview.Empty;
            if (!preview.RequiresAbandonment || !preview.Materials.Any(material => material.ContentId == selectedAbandonedResourceId))
                selectedAbandonedResourceId = string.Empty;
            int collectibleCount = preview.Materials.Sum(material => material.Count);
            string mode = preview.IsAtCamp
                ? "安全撤退：小队已在营地，携带素材将全部带回。"
                : preview.RequiresAbandonment
                ? "紧急撤退：小队远离营地，必须放弃一份携带素材。"
                : "紧急撤退：小队远离营地，但当前没有携带素材。";
            string summary = preview.Materials.Count == 0
                ? "无"
                : string.Join("、", preview.Materials.Select(material => $"{material.DisplayName} ×{material.Count}"));
            string calendarSummary = FormatCalendarSummary(preview.Calendar);
            bool calendarAvailable = preview.Calendar.IsAvailable;
            string calendarUnavailableReason = ResolveCalendarUnavailableReason(preview.Calendar);
            string primaryStatus = !string.IsNullOrWhiteSpace(status)
                ? status
                : calendarAvailable ? "确认前仍可继续探索" : calendarUnavailableReason;
            TabletopEventPrimaryCard3D primary = TabletopEventPrimaryCard3D.Create(transform);
            primary.MoveTo(new Vector3(0f, 0f, 1.75f));
            primary.Present("返回营地？", $"{mode}\n出发猎人 · {hunterCount}\n失去猎人 · {lostCount}\n携带素材 · {collectibleCount}\n{summary}\n\n{calendarSummary}", primaryStatus, string.IsNullOrWhiteSpace(status) && calendarAvailable ? TabletopEventPrimaryTone.Narrative : TabletopEventPrimaryTone.Failure);

            if (preview.RequiresAbandonment)
                PresentMaterialChoices(preview.Materials);

            bool canConfirm = !requestInFlight && calendarAvailable && (!preview.RequiresAbandonment || !string.IsNullOrWhiteSpace(selectedAbandonedResourceId));
            float actionRowZ = ResolveActionRowZ(preview);
            TabletopEventChoiceCard3D confirm = TabletopEventChoiceCard3D.Create(transform, new Vector3(-0.82f, 0f, actionRowZ));
            string confirmStatus = requestInFlight ? "正在结算" : !calendarAvailable ? "时间线不可用" : canConfirm ? "点击确认" : "请先选择放弃素材";
            confirm.Present("结算并回营", preview.RequiresAbandonment ? "提交选定的放弃素材并结束狩猎。" : "将采集物转入营地，结束本次狩猎。", canConfirm, confirmStatus, ConfirmAsync);
            TabletopEventChoiceCard3D cancel = TabletopEventChoiceCard3D.Create(transform, new Vector3(0.82f, 0f, actionRowZ));
            bool canContinue = !requestInFlight && input?.IsReturnCheckpointLocked != true;
            cancel.Present("继续探索", "收起回营卡，保留当前狩猎进度。", canContinue, canContinue ? string.Empty : "检查点已锁定", RequestClose);
        }

        private void PresentMaterialChoices(IReadOnlyList<HuntRetreatMaterial> materials)
        {
            int columns = Mathf.Min(3, materials.Count);
            for (int index = 0; index < materials.Count; index++)
            {
                HuntRetreatMaterial material = materials[index];
                int row = index / columns;
                int column = index % columns;
                float x = (column - (columns - 1) * 0.5f) * 0.95f;
                float z = 0.25f - row * 0.62f;
                TabletopEventChoiceCard3D card = TabletopEventChoiceCard3D.Create(transform, new Vector3(x, 0f, z));
                bool selected = material.ContentId == selectedAbandonedResourceId;
                card.Present(selected ? $"已放弃 · {material.DisplayName}" : $"放弃 · {material.DisplayName}", $"携带 {material.Count} · 放弃 1 份", !requestInFlight, selected ? "当前选择" : "点击选择", () => SelectAbandonedResource(material.ContentId));
            }
        }

        private void SelectAbandonedResource(string contentId)
        {
            if (requestInFlight || string.IsNullOrWhiteSpace(contentId)) return;
            selectedAbandonedResourceId = contentId;
            PresentConfirmation(string.Empty);
        }

        private static float ResolveActionRowZ(HuntRetreatPreview preview)
        {
            if (!preview.RequiresAbandonment)
                return -0.55f;
            int materialRows = Mathf.CeilToInt(preview.Materials.Count / 3f);
            return 0.25f - (materialRows - 1) * 0.62f - 0.78f;
        }

        private static string FormatCalendarSummary(HuntReturnCalendarPreview calendar)
        {
            if (!calendar.IsAvailable)
                return $"回营时间暂不可确认：{ResolveCalendarUnavailableReason(calendar)}";
            if (calendar.AnnualEventGateOpens)
                return $"成功回营后：第 {calendar.NextYear} 年 · {calendar.NextSeasonName}；将进入年度事件结算（如有）。";
            return $"成功回营后：第 {calendar.NextYear} 年 · {calendar.NextSeasonName}；不会创建新年度事件。";
        }

        private static string ResolveCalendarUnavailableReason(HuntReturnCalendarPreview calendar)
            => string.IsNullOrWhiteSpace(calendar.Reason) ? "回营时间预览不可用。" : calendar.Reason;

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
                result = await input.RequestRetreatAsync(new HuntRetreatDecision(selectedAbandonedResourceId));
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
            selectedAbandonedResourceId = string.Empty;
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
