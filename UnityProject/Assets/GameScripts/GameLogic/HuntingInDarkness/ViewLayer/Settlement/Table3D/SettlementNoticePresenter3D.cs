using System;
using System.Collections.Generic;
using Core;
using GameplayBase;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.ViewLayer.Tabletop;
using UnityEngine;

namespace UI
{
    /// <summary>按提交顺序展示营地成长、损失与熟练度反馈的非阻塞实体消息桌。</summary>
    public sealed class SettlementNoticePresenter3D : MonoBehaviour
    {
        private const string HuntDepartureBlockedKey = "hunt-departure-blocked";
        [SerializeField, Min(1f)] private float visibleSeconds = 10f;
        [SerializeField] private Vector3 anchorOffset = new(0f, 0.66f, -3.65f);

        private readonly Queue<SettlementNotice> pendingNotices = new();
        private Func<GamePhase> phaseProvider;
        private Func<Transform> presentationRootProvider;
        private GameObject presentationRoot;
        private TabletopEventPrimaryCard3D noticeCard;
        private TabletopEventChoiceCard3D dismissCard;
        private SettlementNotice activeNotice;
        private SettlementNotice pendingDepartureBlockedNotice;
        private SettlementNotice interruptedNotice;
        private float interruptedRemainingSeconds;
        private float remainingSeconds;

        public bool IsPresenting => activeNotice != null && presentationRoot != null && presentationRoot.activeSelf;
        public int PendingNoticeCount => pendingNotices.Count;
        public string ActiveNoticeTitle => activeNotice?.Title ?? string.Empty;
        public string ActiveNoticeBody => activeNotice?.Body ?? string.Empty;

        public void Initialize(Func<GamePhase> currentPhase, Func<Transform> tabletopRoot)
        {
            if (phaseProvider != null || currentPhase == null || tabletopRoot == null) return;
            phaseProvider = currentPhase;
            presentationRootProvider = tabletopRoot;
            EventBus.Subscribe<HunterGrowthMilestoneReachedEvent>(OnGrowthMilestoneReached);
            EventBus.Subscribe<HunterDiedEvent>(OnHunterDied);
            EventBus.Subscribe<WeaponMasteryChangedEvent>(OnWeaponMasteryChanged);
            EventBus.Subscribe<HunterRetiredEvent>(OnHunterRetired);
            EventBus.Subscribe<HuntCompletedEvent>(OnHuntCompleted);
        }

        public void ResetForCampaignChange()
        {
            pendingNotices.Clear();
            pendingDepartureBlockedNotice = null;
            interruptedNotice = null;
            interruptedRemainingSeconds = 0f;
            activeNotice = null;
            remainingSeconds = 0f;
            if (presentationRoot != null)
                presentationRoot.SetActive(false);
        }

        public void PresentHuntDepartureBlocked(string reason)
        {
            string message = string.IsNullOrWhiteSpace(reason) ? "当前暂时无法出猎。" : reason.Trim();
            var notice = new SettlementNotice(HuntDepartureBlockedKey, "暂不能出猎", message, "完成当前营地流程后可重试", TabletopEventPrimaryTone.Failure);
            if (activeNotice?.Key == HuntDepartureBlockedKey)
            {
                activeNotice = notice;
                remainingSeconds = visibleSeconds;
                PresentActiveNotice();
                return;
            }
            if (activeNotice != null)
            {
                interruptedNotice = activeNotice;
                interruptedRemainingSeconds = remainingSeconds;
                activeNotice = notice;
                pendingDepartureBlockedNotice = null;
                remainingSeconds = visibleSeconds;
                PresentActiveNotice();
                return;
            }
            pendingDepartureBlockedNotice = notice;
        }

        public void ClearHuntDepartureBlocked()
        {
            pendingDepartureBlockedNotice = null;
            if (activeNotice?.Key == HuntDepartureBlockedKey)
                DismissCurrent();
        }

        private void Update()
        {
            if (phaseProvider?.Invoke() != GamePhase.Settlement) return;
            if (activeNotice == null)
            {
                ShowNext();
                return;
            }

            remainingSeconds -= Time.unscaledDeltaTime;
            if (remainingSeconds <= 0f)
                DismissCurrent();
        }

        private void Enqueue(SettlementNotice notice)
        {
            if (notice == null)
                return;
            pendingNotices.Enqueue(notice);
        }

        private void ShowNext()
        {
            if (pendingDepartureBlockedNotice == null && pendingNotices.Count == 0)
                return;
            activeNotice = pendingDepartureBlockedNotice;
            pendingDepartureBlockedNotice = null;
            if (activeNotice == null)
                activeNotice = pendingNotices.Dequeue();
            remainingSeconds = visibleSeconds;
            PresentActiveNotice();
        }

        private void PresentActiveNotice()
        {
            EnsureCards();
            presentationRoot.SetActive(true);
            noticeCard.Present(activeNotice.Title, activeNotice.Body, activeNotice.Footer, activeNotice.Tone);
            bool hasPendingNotice = interruptedNotice != null || pendingDepartureBlockedNotice != null || pendingNotices.Count > 0;
            int pendingNoticeCount = pendingNotices.Count + (pendingDepartureBlockedNotice != null ? 1 : 0) + (interruptedNotice != null ? 1 : 0);
            dismissCard.Present("收起记录", hasPendingNotice ? $"随后还有 {pendingNoticeCount} 条营地消息" : "返回营地桌面", true, "点击继续", DismissCurrent);
        }

        private void EnsureCards()
        {
            Transform parent = presentationRootProvider?.Invoke() ?? transform;
            if (presentationRoot != null)
            {
                if (presentationRoot.transform.parent != parent)
                    presentationRoot.transform.SetParent(parent, false);
                presentationRoot.transform.localPosition = anchorOffset;
                return;
            }

            presentationRoot = new GameObject("SettlementNoticeTable3D");
            presentationRoot.transform.SetParent(parent, false);
            presentationRoot.transform.localPosition = anchorOffset;
            noticeCard = TabletopEventPrimaryCard3D.Create(presentationRoot.transform);
            noticeCard.MoveTo(Vector3.zero);
            dismissCard = TabletopEventChoiceCard3D.Create(presentationRoot.transform, new Vector3(1.95f, 0f, -0.52f));
            presentationRoot.SetActive(false);
        }

        private void DismissCurrent()
        {
            if (activeNotice?.Key == HuntDepartureBlockedKey && interruptedNotice != null)
            {
                activeNotice = interruptedNotice;
                interruptedNotice = null;
                remainingSeconds = interruptedRemainingSeconds;
                interruptedRemainingSeconds = 0f;
                PresentActiveNotice();
                return;
            }
            activeNotice = null;
            remainingSeconds = 0f;
            if (presentationRoot != null)
                presentationRoot.SetActive(false);
        }

        private void OnGrowthMilestoneReached(HunterGrowthMilestoneReachedEvent evt)
        {
            string attributeName = evt.Attribute == HunterGrowthChoice.Courage ? "胆识" : "知识";
            string body = $"{evt.HunterName} 达成{attributeName} {evt.Threshold}\n{evt.DisplayName}";
            if (!string.IsNullOrWhiteSpace(evt.Description))
                body += $"\n\n{evt.Description}";
            Enqueue(new SettlementNotice("成长里程碑", body, "营地会记住这次改变", TabletopEventPrimaryTone.Success));
        }

        private void OnHunterDied(HunterDiedEvent evt)
        {
            string body = $"{evt.HunterName} 没能从黑暗中回来。";
            if (!string.IsNullOrWhiteSpace(evt.CauseText))
                body += $"\n{evt.CauseText}";
            if (evt.InspiredHunterCount > 0 && evt.GrowthPerHunter > 0)
                body += $"\n\n{evt.InspiredHunterCount} 名同伴各获得 {evt.GrowthPerHunter} 点成长。";
            Enqueue(new SettlementNotice("营地失去了一位猎人", body, $"第 {evt.Year} 年", TabletopEventPrimaryTone.Failure));
        }

        private void OnWeaponMasteryChanged(WeaponMasteryChangedEvent evt)
        {
            string masteryName = string.IsNullOrWhiteSpace(evt.MasteryName) ? evt.WeaponName : evt.MasteryName;
            string action = evt.Source == WeaponMasteryGainSource.Training ? "完成训练" : $"使用 {evt.WeaponName} 造成有效伤害";
            string body = $"{evt.HunterName} {action}\n\n{masteryName}熟练度 {evt.OldValue} → {evt.NewValue}";
            if (evt.ReachedMilestoneNames != null && evt.ReachedMilestoneNames.Length > 0)
                body += $"\n达成：{string.Join("、", evt.ReachedMilestoneNames)}";
            Enqueue(new SettlementNotice("武器熟练度成长", body, "经验已经写入猎人记录", TabletopEventPrimaryTone.Success));
        }

        private void OnHunterRetired(HunterRetiredEvent evt)
        {
            string hunterName = string.IsNullOrWhiteSpace(evt.HunterName) ? $"猎人 {evt.HunterId}" : evt.HunterName;
            string body = $"{hunterName} 在年龄 {evt.Age} 结束了出猎生涯。";
            body += evt.ReturnedEquipmentCount > 0 ? $"\n\n{evt.ReturnedEquipmentCount} 件装备已归还营地仓库。" : "\n\n没有需要归还的装备。";
            string footer = evt.Year > 0 ? $"第 {evt.Year} 年 · 退休记录已写入年鉴" : "退休记录已写入年鉴";
            Enqueue(new SettlementNotice("猎人退休归档", body, footer, TabletopEventPrimaryTone.Narrative));
        }

        private void OnHuntCompleted(HuntCompletedEvent evt)
        {
            string outcome = evt.BossDefeated ? "讨伐成功" : "从黑暗中归来";
            string completedSeason = string.IsNullOrWhiteSpace(evt.CompletedSeasonDisplayName) ? $"第 {evt.CompletedSeasonIndex + 1} 季" : evt.CompletedSeasonDisplayName;
            string advancedSeason = string.IsNullOrWhiteSpace(evt.AdvancedToSeasonDisplayName) ? $"第 {evt.AdvancedToSeasonIndex + 1} 季" : evt.AdvancedToSeasonDisplayName;
            string completedPeriod = $"第 {evt.CompletedYear} 年·{completedSeason}";
            string advancedPeriod = $"第 {evt.AdvancedToYear} 年·{advancedSeason}";
            string body = $"{completedPeriod} · {outcome}\n远征已归档，营地进入 {advancedPeriod}\n\n出发 {evt.HuntersDeployed} · 损失 {evt.HuntersLost} · 带回 {evt.CollectedItemCount} 件物品";
            string footer = $"年鉴现有 {evt.TotalHunts} 条狩猎记录";
            TabletopEventPrimaryTone tone = evt.HuntersLost > 0 ? TabletopEventPrimaryTone.Failure : TabletopEventPrimaryTone.Success;
            string title = evt.CompletedYear == evt.AdvancedToYear ? "季节推进 · 回营" : "新年抵达 · 回营";
            Enqueue(new SettlementNotice(null, title, body, footer, tone));
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<HunterGrowthMilestoneReachedEvent>(OnGrowthMilestoneReached);
            EventBus.Unsubscribe<HunterDiedEvent>(OnHunterDied);
            EventBus.Unsubscribe<WeaponMasteryChangedEvent>(OnWeaponMasteryChanged);
            EventBus.Unsubscribe<HunterRetiredEvent>(OnHunterRetired);
            EventBus.Unsubscribe<HuntCompletedEvent>(OnHuntCompleted);
            if (presentationRoot != null)
                Destroy(presentationRoot);
        }

        private sealed class SettlementNotice
        {
            public string Key { get; }
            public string Title { get; }
            public string Body { get; }
            public string Footer { get; }
            public TabletopEventPrimaryTone Tone { get; }

            public SettlementNotice(string title, string body, string footer, TabletopEventPrimaryTone tone)
                : this(null, title, body, footer, tone)
            {
            }

            public SettlementNotice(string key, string title, string body, string footer, TabletopEventPrimaryTone tone)
            {
                Key = key ?? string.Empty;
                Title = title ?? string.Empty;
                Body = body ?? string.Empty;
                Footer = footer ?? string.Empty;
                Tone = tone;
            }
        }
    }
}
