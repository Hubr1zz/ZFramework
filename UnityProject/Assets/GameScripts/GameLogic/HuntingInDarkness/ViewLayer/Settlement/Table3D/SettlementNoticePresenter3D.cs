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
        [SerializeField, Min(1f)] private float visibleSeconds = 10f;
        [SerializeField] private Vector3 anchorOffset = new(0f, 0.66f, -3.65f);

        private readonly Queue<SettlementNotice> pendingNotices = new();
        private GameManager manager;
        private GameObject presentationRoot;
        private TabletopEventPrimaryCard3D noticeCard;
        private TabletopEventChoiceCard3D dismissCard;
        private SettlementNotice activeNotice;
        private float remainingSeconds;

        public bool IsPresenting => activeNotice != null && presentationRoot != null && presentationRoot.activeSelf;
        public int PendingNoticeCount => pendingNotices.Count;
        public string ActiveNoticeTitle => activeNotice?.Title ?? string.Empty;

        public void Initialize(GameManager gameManager)
        {
            if (manager != null || gameManager == null)
                return;
            manager = gameManager;
            EventBus.Subscribe<HunterGrowthMilestoneReachedEvent>(OnGrowthMilestoneReached);
            EventBus.Subscribe<HunterDiedEvent>(OnHunterDied);
            EventBus.Subscribe<WeaponMasteryChangedEvent>(OnWeaponMasteryChanged);
            EventBus.Subscribe<HuntCompletedEvent>(OnHuntCompleted);
        }

        private void Update()
        {
            if (manager == null || manager.CurrentGamePhase != GamePhase.Settlement)
                return;
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
            if (pendingNotices.Count == 0)
                return;
            EnsureCards();
            activeNotice = pendingNotices.Dequeue();
            remainingSeconds = visibleSeconds;
            presentationRoot.SetActive(true);
            noticeCard.Present(activeNotice.Title, activeNotice.Body, activeNotice.Footer, activeNotice.Tone);
            dismissCard.Present("收起记录", pendingNotices.Count > 0 ? $"随后还有 {pendingNotices.Count} 条营地消息" : "返回营地桌面", true, "点击继续", DismissCurrent);
        }

        private void EnsureCards()
        {
            Transform parent = manager.TabletopPresentationRoot != null ? manager.TabletopPresentationRoot : transform;
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

        private void OnHuntCompleted(HuntCompletedEvent evt)
        {
            string outcome = evt.BossDefeated ? "讨伐成功" : "从黑暗中归来";
            string body = $"第 {evt.CompletedYear} 年 · {outcome}\n本年狩猎 {evt.HuntsCompletedInYear}/{evt.HuntsPerYear}\n\n出发 {evt.HuntersDeployed} · 损失 {evt.HuntersLost} · 带回 {evt.CollectedResourceCount} 项物资";
            string footer = evt.AdvancedToYear > 0 ? $"营地进入第 {evt.AdvancedToYear} 年" : $"年鉴现有 {evt.TotalHunts} 条狩猎记录";
            TabletopEventPrimaryTone tone = evt.HuntersLost > 0 ? TabletopEventPrimaryTone.Failure : TabletopEventPrimaryTone.Success;
            Enqueue(new SettlementNotice("狩猎记录归档", body, footer, tone));
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<HunterGrowthMilestoneReachedEvent>(OnGrowthMilestoneReached);
            EventBus.Unsubscribe<HunterDiedEvent>(OnHunterDied);
            EventBus.Unsubscribe<WeaponMasteryChangedEvent>(OnWeaponMasteryChanged);
            EventBus.Unsubscribe<HuntCompletedEvent>(OnHuntCompleted);
            if (presentationRoot != null)
                Destroy(presentationRoot);
        }

        private sealed class SettlementNotice
        {
            public string Title { get; }
            public string Body { get; }
            public string Footer { get; }
            public TabletopEventPrimaryTone Tone { get; }

            public SettlementNotice(string title, string body, string footer, TabletopEventPrimaryTone tone)
            {
                Title = title ?? string.Empty;
                Body = body ?? string.Empty;
                Footer = footer ?? string.Empty;
                Tone = tone;
            }
        }
    }
}
