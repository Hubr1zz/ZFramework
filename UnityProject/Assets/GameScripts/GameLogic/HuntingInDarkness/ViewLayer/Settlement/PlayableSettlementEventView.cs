using System.Collections.Generic;
using System.Text;
using System.Threading;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.ViewLayer.Tabletop;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Settlement
{
    /// <summary>营地与狩猎共用的桌面事件输入端口；只返回玩家决定，不提交规则状态。</summary>
    public sealed class PlayableSettlementEventView : MonoBehaviour, IPlayableEventInput
    {
        private enum EventPromptKind
        {
            None,
            Narrative,
            Choice,
            Check,
            Result
        }

        private static int nextInputOwnerId;
        private GameManager manager;
        private int inputOwnerId;
        private EventPromptKind prompt;
        private EventData currentEvent;
        private HunterInstance currentActor;
        private IReadOnlyList<HunterInstance> candidateHunters = System.Array.Empty<HunterInstance>();
        private IPlayableEventResourceAvailability resourceAvailability;
        private TabletopEventPanel3D panel;
        private UniTaskCompletionSource narrativeSource;
        private UniTaskCompletionSource<PlayableEventChoiceSelection> choiceSource;
        private UniTaskCompletionSource<PlayableEventCheckDecision> checkSource;
        private UniTaskCompletionSource resultSource;

        public bool IsPresenting => prompt != EventPromptKind.None;
        public TabletopEventPanel3D ActivePanel => panel;

        public void Initialize(GameManager gameManager)
        {
            manager = gameManager;
            EnsureInputOwnerId();
            manager?.SetPlayableEventInput(this);
        }

        public async UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken)
        {
            BeginPrompt(EventPromptKind.Narrative, gameEvent, actor, null);
            narrativeSource = new UniTaskCompletionSource();
            try
            {
                PresentNarrative();
                await narrativeSource.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                narrativeSource = null;
                EndPrompt(EventPromptKind.Narrative);
            }
        }

        public async UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, IPlayableEventResourceAvailability resourceAvailability, CancellationToken cancellationToken)
        {
            BeginPrompt(EventPromptKind.Choice, gameEvent, actor, hunters);
            this.resourceAvailability = resourceAvailability;
            choiceSource = new UniTaskCompletionSource<PlayableEventChoiceSelection>();
            try
            {
                PresentChoices();
                return await choiceSource.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                choiceSource = null;
                EndPrompt(EventPromptKind.Choice);
            }
        }

        public async UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken)
        {
            if (transaction == null) throw new System.ArgumentNullException(nameof(transaction));
            BeginPrompt(EventPromptKind.Check, transaction.GameEvent, transaction.Actor, null);
            checkSource = new UniTaskCompletionSource<PlayableEventCheckDecision>();
            try
            {
                PresentCheck(transaction);
                return await checkSource.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                checkSource = null;
                EndPrompt(EventPromptKind.Check);
            }
        }

        public async UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken)
        {
            BeginPrompt(EventPromptKind.Result, gameEvent, null, null);
            resultSource = new UniTaskCompletionSource();
            try
            {
                PresentResult(result);
                await resultSource.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                resultSource = null;
                EndPrompt(EventPromptKind.Result);
            }
        }

        private void PresentNarrative()
        {
            string actionLabel = currentEvent.eventType == GameEventType.Combat ? "迎接战斗" : "接受结果";
            var choices = new[]
            {
                new TabletopEventChoicePresentation(actionLabel, "点击屏幕按钮继续事件", true, string.Empty, () => narrativeSource?.TrySetResult())
            };
            PresentPanel(currentEvent.eventName, currentEvent.displayText, ActorFooter(currentActor), TabletopEventPrimaryTone.Narrative, choices);
        }

        private void PresentChoices()
        {
            var choices = new List<TabletopEventChoicePresentation>();
            int availableCount = 0;
            int optionCount = currentEvent.options?.Count ?? 0;
            for (int index = 0; index < optionCount; index++)
            {
                int optionIndex = index;
                EventOption option = currentEvent.options[index];
                if (option == null)
                {
                    choices.Add(new TabletopEventChoicePresentation($"选项 {index + 1}", "事件数据缺失。", false, "无法选择", null));
                    continue;
                }
                bool available = CanPresentOption(option, out string reason);
                if (available)
                    availableCount++;
                string optionTitle = string.IsNullOrWhiteSpace(option.optionText) ? $"选项 {index + 1}" : option.optionText;
                string requirements = PlayableEventOptionAvailability.GetRequirements(option, resourceAvailability);
                string body = DescribeCheck(option, currentActor);
                if (!string.IsNullOrWhiteSpace(requirements))
                    body += $"\n{requirements}";
                body += DescribeOutcomes(option);
                choices.Add(new TabletopEventChoicePresentation(optionTitle, body, available, available ? "点击选择" : reason, () => SelectOption(optionIndex)));
            }
            if (availableCount == 0)
                choices.Add(new TabletopEventChoicePresentation("接受沉默", "当前没有可行行动，按叙事结果继续。", true, string.Empty, () => choiceSource?.TrySetResult(new PlayableEventChoiceSelection(-1, null))));
            PresentPanel(currentEvent.eventName, currentEvent.displayText, ActorFooter(currentActor), TabletopEventPrimaryTone.Narrative, choices);
        }

        private void SelectOption(int optionIndex)
        {
            if (choiceSource == null || currentEvent.options == null || optionIndex < 0 || optionIndex >= currentEvent.options.Count) return;
            EventOption option = currentEvent.options[optionIndex];
            if (option == null) return;
            if (!CanPresentOption(option, out _)) return;
            bool needsHunter = option.checkType != CheckType.None || PlayableEventOptionAvailability.RequiresHunter(option);
            if (currentActor != null || !needsHunter)
            {
                choiceSource.TrySetResult(new PlayableEventChoiceSelection(optionIndex, currentActor));
                return;
            }
            PresentHunterSelection(optionIndex);
        }

        private void PresentHunterSelection(int optionIndex)
        {
            EventOption option = currentEvent.options[optionIndex];
            var choices = new List<TabletopEventChoicePresentation>();
            foreach (HunterInstance hunter in candidateHunters)
            {
                if (hunter == null) continue;
                HunterInstance selectedHunter = hunter;
                bool available = PlayableEventOptionAvailability.CanUse(option, hunter, manager.SettlementData, resourceAvailability, out string reason);
                string body = $"{DescribeCheck(option, hunter)}\n意志 {hunter.Willpower}/{hunter.WillpowerMax}";
                body += DescribeOutcomes(option);
                choices.Add(new TabletopEventChoicePresentation(hunter.Name, body, available, available ? "点击派出" : reason, () => choiceSource?.TrySetResult(new PlayableEventChoiceSelection(optionIndex, selectedHunter))));
            }
            choices.Add(new TabletopEventChoicePresentation("返回", "重新查看事件选项", true, string.Empty, PresentChoices));
            string hunterAction = $"执行{GetCheckName(option.checkType)}判定";
            bool oldMaid = option.checkPresentation == EventCheckPresentationKind.OldMaid;
            if (oldMaid)
                hunterAction = "抽牌";
            else if (option.checkType == CheckType.None)
                hunterAction = "执行行动";
            string bodyText = $"{option.optionText}\n\n选择{hunterAction}的猎人。";
            PresentPanel(currentEvent.eventName, bodyText, "猎人选择", TabletopEventPrimaryTone.Check, choices);
        }

        private void PresentCheck(PlayableEventChoiceTransaction transaction)
        {
            bool oldMaid = transaction.Option.checkPresentation == EventCheckPresentationKind.OldMaid;
            bool usesCards = transaction.Option.checkPresentation != EventCheckPresentationKind.PhysicalDice;
            string valueLabel = usesCards ? "牌面合计" : "骰点";
            string body = oldMaid ? $"{transaction.Option.optionText}\n\n抽牌结果：{(transaction.Success ? "安全牌" : "无面牌")}\n\n{(transaction.Success ? "避开鬼牌" : "抽中鬼牌")}" : $"{transaction.Option.optionText}\n\n{DescribeCheck(transaction.Option, transaction.Actor)}\n实际：{valueLabel} {transaction.RollValue} + 属性 {transaction.Bonus} = 总值 {transaction.Total}\n目标 {transaction.Target}\n\n{(transaction.Success ? "判定成功" : "判定失败")}";
            body += DescribeResolvedOutcome(transaction.Option, transaction.Success);
            if (transaction.HasRerolled)
                body += oldMaid ? "\n已消耗 1 意志并使命运 +1；重抽并保留安全结果。" : $"\n已消耗 1 意志并使命运 +1；重新{(usesCards ? "抽牌" : "投掷")}后保留较高结果。";
            var choices = new List<TabletopEventChoicePresentation>
            {
                new("接受结果", "提交当前判定", true, string.Empty, () => checkSource?.TrySetResult(PlayableEventCheckDecision.Accept))
            };
            if (transaction.CanReroll)
                choices.Insert(0, new TabletopEventChoicePresentation(usesCards ? "重抽" : "重投", "费用：意志 -1、命运 +1。" + (oldMaid ? "重抽后保留安全结果。" : "重投后保留较高结果。"), true, string.Empty, () => checkSource?.TrySetResult(PlayableEventCheckDecision.Reroll)));
            PresentPanel(transaction.GameEvent.eventName, body, ActorFooter(transaction.Actor), transaction.Success ? TabletopEventPrimaryTone.Success : TabletopEventPrimaryTone.Failure, choices);
        }

        private void PresentResult(EventResolutionResult result)
        {
            string body = string.IsNullOrWhiteSpace(result.ResultText) ? result.Success ? "判定成功。" : "判定失败。" : result.ResultText;
            var choices = new[]
            {
                new TabletopEventChoicePresentation("继续", "收起事件卡并推进事件链", true, string.Empty, () => resultSource?.TrySetResult())
            };
            PresentPanel(currentEvent?.eventName ?? "事件结果", body, result.RollValue > 0 ? $"最终判定值 {result.RollValue}" : string.Empty, result.Success ? TabletopEventPrimaryTone.Success : TabletopEventPrimaryTone.Failure, choices);
        }

        private void PresentPanel(string title, string body, string footer, TabletopEventPrimaryTone tone, IReadOnlyList<TabletopEventChoicePresentation> choices)
        {
            Transform parent = manager.TabletopPresentationRoot != null ? manager.TabletopPresentationRoot : transform;
            if (panel == null)
                panel = TabletopEventPanel3D.Create(parent);
            else if (panel.transform.parent != parent)
                panel.transform.SetParent(parent, true);
            Vector3 anchor = manager.ResolveTabletopEventAnchor(currentActor) + new Vector3(0f, 0.62f, -2.35f);
            panel.Present(anchor, title, body, footer, tone, choices);
        }

        private bool CanPresentOption(EventOption option, out string reason)
        {
            if (option == null)
            {
                reason = "事件数据缺失。";
                return false;
            }
            if (currentActor != null)
                return PlayableEventOptionAvailability.CanUse(option, currentActor, manager.SettlementData, resourceAvailability, out reason);
            bool needsHunter = option.checkType != CheckType.None || PlayableEventOptionAvailability.RequiresHunter(option);
            if (!needsHunter)
                return PlayableEventOptionAvailability.CanUse(option, null, manager.SettlementData, resourceAvailability, out reason);
            foreach (HunterInstance hunter in candidateHunters)
                if (hunter != null && PlayableEventOptionAvailability.CanUse(option, hunter, manager.SettlementData, resourceAvailability, out _))
                {
                    reason = string.Empty;
                    return true;
                }
            reason = "当前没有猎人满足该选项。";
            return false;
        }

        private void BeginPrompt(EventPromptKind nextPrompt, EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters)
        {
            if (manager == null) throw new System.InvalidOperationException("事件 View 尚未初始化。");
            if (prompt != EventPromptKind.None) throw new System.InvalidOperationException("事件输入端口已经在处理另一项请求。");
            prompt = nextPrompt;
            currentEvent = gameEvent ?? throw new System.ArgumentNullException(nameof(gameEvent));
            currentActor = actor;
            candidateHunters = hunters ?? System.Array.Empty<HunterInstance>();
            EnsureInputOwnerId();
            PlayableHuntInputGuard.Acquire(inputOwnerId);
        }

        private void EndPrompt(EventPromptKind completedPrompt)
        {
            if (prompt != completedPrompt) return;
            prompt = EventPromptKind.None;
            currentEvent = null;
            currentActor = null;
            candidateHunters = System.Array.Empty<HunterInstance>();
            resourceAvailability = null;
            panel?.Close();
            PlayableHuntInputGuard.Release(inputOwnerId);
        }

        private void EnsureInputOwnerId()
        {
            if (inputOwnerId == 0)
                inputOwnerId = Interlocked.Increment(ref nextInputOwnerId);
        }

        private static string ActorFooter(HunterInstance actor) => actor != null ? $"关联猎人 · {actor.Name}" : string.Empty;

        private static string DescribeCheck(EventOption option, HunterInstance actor)
        {
            if (option == null || option.checkType == CheckType.None) return "无需判定 · 直接结算";
            if (option.checkPresentation == EventCheckPresentationKind.OldMaid) return "抽鬼牌 · 不使用属性骰 · 安全牌 / 无面牌";
            int count = PlayableEventCheckRules.ResolveCount(option);
            int sides = PlayableEventCheckRules.ResolveSides(option);
            int minRoll = count;
            int maxRoll = count * sides;
            string rollLabel = option.checkPresentation == EventCheckPresentationKind.PhysicalDice ? "骰点" : "牌面合计";
            string bonus = actor == null ? "选定猎人属性" : GetCheckBonus(actor, option.checkType).ToString();
            string totalRange = actor == null ? "选定猎人属性" : $"{minRoll + GetCheckBonus(actor, option.checkType)}–{maxRoll + GetCheckBonus(actor, option.checkType)}";
            string formula = option.checkPresentation == EventCheckPresentationKind.PhysicalDice ? $"{count}d{sides}" : $"{count}张牌（每张 1–{sides}）";
            if (actor == null) return $"判定式：{formula} + {GetCheckName(option.checkType)}({bonus})\n{rollLabel}范围 {minRoll}–{maxRoll} · 总值范围待选猎人确定 · 成功边界 ≥ {option.checkTarget}\n选择猎人后显示成功/失败区间";
            int threshold = option.checkTarget - GetCheckBonus(actor, option.checkType);
            return $"判定式：{formula} + {GetCheckName(option.checkType)}({bonus})\n{rollLabel}范围 {minRoll}–{maxRoll} · 总值范围 {totalRange} · 成功边界 ≥ {option.checkTarget}\n{DescribeRollOutcome(rollLabel, minRoll, maxRoll, threshold)}";
        }

        private static string DescribeRollOutcome(string rollLabel, int minRoll, int maxRoll, int threshold)
        {
            if (threshold <= minRoll) return $"{rollLabel}{minRoll}–{maxRoll}：成功（全部满足）";
            if (threshold > maxRoll) return $"{rollLabel}{minRoll}–{maxRoll}：失败（全部不满足）";
            return $"{rollLabel}{minRoll}–{threshold - 1}：失败\n{rollLabel}{threshold}–{maxRoll}：成功";
        }

        private static string DescribeOutcomes(EventOption option)
        {
            if (option == null) return string.Empty;
            var builder = new StringBuilder();
            AppendOutcome(builder, "成功", option.successText, option.successEffects, option.successChain);
            if (option.checkType != CheckType.None)
                AppendOutcome(builder, "失败", option.failText, option.failEffects, option.failChain);
            return builder.ToString();
        }

        private static string DescribeResolvedOutcome(EventOption option, bool success)
        {
            if (option == null) return string.Empty;
            var builder = new StringBuilder();
            IReadOnlyList<EventEffect> effects = success ? option.successEffects : option.failEffects;
            IReadOnlyList<EventData> chain = success ? option.successChain : option.failChain;
            AppendOutcome(builder, success ? "本次成功结果" : "本次失败结果", success ? option.successText : option.failText, effects, chain);
            return builder.ToString();
        }

        private static void AppendOutcome(StringBuilder builder, string label, string resultText, IReadOnlyList<EventEffect> effects, IReadOnlyList<EventData> chain)
        {
            builder.Append($"\n\n{label}：{(string.IsNullOrWhiteSpace(resultText) ? "未配置结果文本" : resultText)}");
            if (effects != null)
                foreach (EventEffect effect in effects)
                    if (effect != null && !string.IsNullOrWhiteSpace(effect.description))
                        builder.Append($"\n影响：{effect.description.Trim()}");
            if (chain != null)
                foreach (EventData child in chain)
                {
                    string childName = child?.eventName?.Trim();
                    if (!string.IsNullOrWhiteSpace(childName)) builder.Append($"\n后续事件：{childName}");
                }
        }

        private static int GetCheckBonus(HunterInstance hunter, CheckType checkType)
        {
            if (hunter == null) return 0;
            return checkType switch
            {
                CheckType.Courage => hunter.Courage,
                CheckType.Luck => hunter.Luck,
                CheckType.Strength => hunter.Stats?.strength ?? 0,
                CheckType.Evasion => hunter.Stats?.evasion ?? 0,
                CheckType.Understanding => hunter.Understanding,
                _ => 0
            };
        }

        private static string GetCheckName(CheckType checkType)
        {
            return checkType switch
            {
                CheckType.Courage => "胆识",
                CheckType.Luck => "命运",
                CheckType.Strength => "力量",
                CheckType.Evasion => "敏捷",
                CheckType.Understanding => "知识",
                _ => "无属性"
            };
        }

        private void OnDestroy()
        {
            prompt = EventPromptKind.None;
            resourceAvailability = null;
            narrativeSource?.TrySetCanceled();
            choiceSource?.TrySetCanceled();
            checkSource?.TrySetCanceled();
            resultSource?.TrySetCanceled();
            PlayableHuntInputGuard.Release(inputOwnerId);
            if (panel != null)
            {
                panel.Close();
                Destroy(panel.gameObject);
            }
            panel = null;
            manager?.ClearPlayableEventInput(this);
        }
    }
}
