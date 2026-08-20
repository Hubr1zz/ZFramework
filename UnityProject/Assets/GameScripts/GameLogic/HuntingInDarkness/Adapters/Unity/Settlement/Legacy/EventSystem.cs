using System.Collections.Generic;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    /// <summary>
    /// 事件处理系统（纯 C#）。
    /// 职责：事件触发流程（显示文本 → 选项 → 判定 → 效果 → 子事件链），
    /// 骰子投掷，意志点重投机制。
    /// </summary>
    public partial class EventSystem
    {
        private readonly SettlementInstance _settlement;
        private readonly IRandomSource      _rng;
        private readonly IDelayedEventScheduler delayedEventScheduler;

        /// <summary>当事件需要展示 UI 时，调用此回调（SettlementUIManager 注入）</summary>
        public System.Action<EventData, HunterInstance> OnEventTriggered;

        /// <summary>当事件结束（包含子事件链全部处理完）后调用</summary>
        public System.Action OnEventChainCompleted;
        internal SettlementInstance Settlement => _settlement;

        // 当前处理中的事件队列（子事件链用）
        private readonly Queue<EventData> _pendingChain = new();
        private HunterInstance            _selectedHunter;

        public EventSystem(SettlementInstance settlement, IRandomSource rng, IDelayedEventScheduler delayedEventScheduler = null)
        {
            _settlement = settlement;
            _rng        = rng;
            this.delayedEventScheduler = delayedEventScheduler;
        }

        // ─── 触发事件 ────────────────────────────────────────────

        /// <summary>触发单个事件，指定生效猎人（可为 null 表示营地整体）</summary>
        public void TriggerEvent(EventData evt, HunterInstance hunter = null)
        {
            if (evt == null) return;
            _selectedHunter = hunter;
            _pendingChain.Clear();
            _pendingChain.Enqueue(evt);
            ProcessNextInChain();
        }

        /// <summary>触发多个事件（按顺序依次处理）</summary>
        public void TriggerEventList(List<EventData> events, HunterInstance hunter = null)
        {
            _selectedHunter = hunter;
            _pendingChain.Clear();
            foreach (var e in events)
                if (e != null) _pendingChain.Enqueue(e);
            ProcessNextInChain();
        }

        private void ProcessNextInChain()
        {
            if (_pendingChain.Count == 0)
            {
                OnEventChainCompleted?.Invoke();
                return;
            }
            var next = _pendingChain.Dequeue();
            EventBus.Publish(new GameEventTriggeredEvent { EventId = next.name });
            OnEventTriggered?.Invoke(next, _selectedHunter);
        }

        // ─── 叙事事件结算 ────────────────────────────────────────

        /// <summary>
        /// 叙事事件：玩家点击"确认"后调用。
        /// 执行效果，追加子事件链，继续处理。
        /// </summary>
        public void ResolveNarrative(EventData evt)
        {
            PlayableEventNodeCommitResult result = ResolveNarrativeNode(evt, _selectedHunter, false);
            if (result.EncounterIds.Count > 0)
            {
                _pendingChain.Clear();
                return;
            }
            EnqueueChain(result.ChainedEvents);
            ProcessNextInChain();
        }

        /// <summary>结算单个叙事节点并返回后续节点，不触碰共享事件队列。</summary>
        public IReadOnlyList<EventData> ResolveNarrativeStandalone(EventData gameEvent, HunterInstance actor = null)
        {
            PlayableEventNodeCommitResult result = ResolveNarrativeNode(gameEvent, actor, false);
            return result.EncounterIds.Count > 0 ? System.Array.Empty<EventData>() : result.ChainedEvents;
        }

        /// <summary>结算单个节点并捕获跨环境遭遇请求，避免 Action 流程依赖全局字符串事件。</summary>
        public PlayableEventNodeCommitResult ResolveNarrativeNodeStandalone(EventData gameEvent, HunterInstance actor = null)
        {
            return ResolveNarrativeNode(gameEvent, actor, true);
        }

        private PlayableEventNodeCommitResult ResolveNarrativeNode(EventData gameEvent, HunterInstance actor, bool captureEncounterRequests)
        {
            if (gameEvent == null) return new PlayableEventNodeCommitResult(System.Array.Empty<EventData>(), System.Array.Empty<string>());
            var encounterIds = new List<string>();
            if (gameEvent.eventType == GameEventType.Combat && !string.IsNullOrWhiteSpace(gameEvent.combatEncounterId))
                RecordEncounter(gameEvent.combatEncounterId, encounterIds);
            if (gameEvent.immediateEffects != null)
                foreach (EventEffect effect in gameEvent.immediateEffects)
                    ApplyEffect(effect, actor, actor, encounterIds);
            if (gameEvent.eventType == GameEventType.Combat && encounterIds.Count == 0)
                RecordEncounter(gameEvent.combatEncounterId, encounterIds);
            if (!captureEncounterRequests)
                PublishEncounters(encounterIds, gameEvent.name);
            MarkEventCompleted(gameEvent);
            return new PlayableEventNodeCommitResult(gameEvent.chainedEvents, encounterIds);
        }

        // ─── 抉择事件结算 ────────────────────────────────────────

        /// <summary>
        /// 抉择事件：玩家选择一个选项后调用。
        /// 进行判定（有需要时）并执行对应效果。
        /// 返回 EventResolutionResult 供 UI 展示结果文本。
        /// </summary>
        public EventResolutionResult ResolveChoice(EventData evt, int optionIndex,
            HunterInstance actor = null)
        {
            if (evt?.options == null || optionIndex < 0 || optionIndex >= evt.options.Count)
                return new EventResolutionResult { Success = false, ResultText = "无效选项" };

            var option = evt.options[optionIndex];
            actor ??= _selectedHunter;
            if (option.checkType != CheckType.None && actor == null)
                return new EventResolutionResult { Success = false, ResultText = "该选项需要一名猎人执行。" };
            if (!PlayableEventOptionAvailability.CanUse(option, actor, _settlement, out string unavailableReason))
                return new EventResolutionResult { Success = false, ResultText = unavailableReason };

            bool success = true;
            int  rollValue = 0;

            // 判定（有 checkType 时）
            if (option.checkType != CheckType.None)
            {
                rollValue = RollDice(1, 10);
                int bonus = GetCheckBonus(actor, option.checkType);
                success = EventRules.CheckSucceeded(rollValue, bonus, option.checkTarget);
            }

            // 执行效果
            var effects = success ? option.successEffects : option.failEffects;
            var encounterIds = new List<string>();
            if (evt.eventType == GameEventType.Combat && !string.IsNullOrWhiteSpace(evt.combatEncounterId))
                RecordEncounter(evt.combatEncounterId, encounterIds);
            if (effects != null)
                foreach (var effect in effects)
                    ApplyEffect(effect, actor, actor, encounterIds);
            if (evt.eventType == GameEventType.Combat && encounterIds.Count == 0)
                RecordEncounter(evt.combatEncounterId, encounterIds);
            PublishEncounters(encounterIds, evt.name);
            MarkEventCompleted(evt);

            var result = new EventResolutionResult
            {
                Success = success,
                RollValue = rollValue,
                ResultText = success ? option.successText : option.failText
            };
            if (encounterIds.Count > 0)
            {
                _pendingChain.Clear();
                return result;
            }

            // 追加子事件链
            EnqueueChain(success ? option.successChain : option.failChain);
            ProcessNextInChain();
            return result;
        }

        // ─── 骰子系统 ────────────────────────────────────────────

        /// <summary>投掷 diceCount 个 d[sides] 骰，返回总和</summary>
        public int RollDice(int diceCount, int sides)
        {
            return EventRules.RollDice(_rng, diceCount, sides);
        }

        /// <summary>
        /// 意志点重投：消耗1点意志点，重新投掷并取最高值。
        /// 成功消耗 → 返回新结果；失败（无意志点）→ 返回旧结果。
        /// </summary>
        public RerollResult TryReroll(HunterInstance hunter, int currentRoll, int diceCount, int sides)
        {
            RerollOutcome outcome = EventRules.TryReroll(
                hunter, currentRoll, diceCount, sides, _rng);
            if (!outcome.Success)
                return new RerollResult { Success = false, FinalRoll = currentRoll };

            int newRoll = outcome.NewRoll;
            int best    = outcome.FinalRoll;
            Debug.Log($"[EventSystem] 重投 {hunter.Name}：{currentRoll} → {newRoll}（取最高 {best}）");
            return new RerollResult { Success = true, NewRoll = newRoll, FinalRoll = best };
        }

        public RerollResult TryReroll(HunterInstance hunter, int currentRoll, int newRoll)
        {
            if (newRoll < 1 || newRoll > 10) return new RerollResult { Success = false, FinalRoll = currentRoll };
            RerollOutcome outcome = EventRules.TryReroll(hunter, currentRoll, newRoll);
            if (!outcome.Success) return new RerollResult { Success = false, FinalRoll = currentRoll };
            Debug.Log($"[EventSystem] 物理重投 {hunter.Name}：{currentRoll} → {outcome.NewRoll}（取最高 {outcome.FinalRoll}）");
            return new RerollResult { Success = true, NewRoll = outcome.NewRoll, FinalRoll = outcome.FinalRoll };
        }

        // ─── 效果执行 ────────────────────────────────────────────

        public void ApplyEffect(EventEffect effect, HunterInstance target)
        {
            ApplyEffect(effect, target, _selectedHunter ?? target);
        }

        private void ApplyEffect(EventEffect effect, HunterInstance target, HunterInstance eventActor, List<string> encounterIds = null)
        {
            if (effect == null) return;
            if (effect.effectType == EventEffectType.ScheduleEvent)
            {
                if (delayedEventScheduler == null)
                {
                    Debug.LogWarning($"[EventSystem] 无法安排延时事件 {effect.targetName}：Timeline 未注入");
                    return;
                }
                if (!delayedEventScheduler.TryScheduleEventAfterYears(effect.targetName, effect.value, out string reason))
                    Debug.LogWarning($"[EventSystem] 无法安排延时事件 {effect.targetName}：{reason}");
                return;
            }

            string targetId = effect.effectType == EventEffectType.AddResource || effect.effectType == EventEffectType.RemoveResource ? PlayableSettlementItemRegistry.ResolveContentId(effect.targetName) : effect.targetName;
            SettlementEffectOutcome outcome = SettlementEffectRules.Apply(
                ToCoreEffectKind(effect.effectType),
                targetId,
                effect.value,
                eventActor,
                target,
                _settlement.GetAvailableHunters(),
                _settlement.GetResource,
                _settlement.AddResource,
                _settlement.SpendResource,
                _settlement.UnlockInvention);

            if (outcome.Handled && effect.effectType == EventEffectType.AddAilment)
                PlayableSymptomRuntime.SynchronizeHunter(eventActor);
            if (outcome.Handled && (effect.effectType == EventEffectType.AddCourage || effect.effectType == EventEffectType.AddUnderstanding))
                PlayableGrowthMilestoneRuntime.Synchronize(_settlement);

            if (outcome.ResourceChanged)
            {
                EventBus.Publish(new ResourceChangedEvent
                {
                    ResourceName = outcome.ResourceId,
                    OldAmount = outcome.OldAmount,
                    NewAmount = outcome.NewAmount
                });
            }

            if (outcome.TriggerCombat)
                RecordEncounter(effect.targetName, encounterIds);
            if (outcome.AdvanceYear)
                Debug.Log("[EventSystem] 效果要求推进年份（由外部处理）");
            if (!outcome.Handled)
                Debug.LogWarning($"[EventSystem] 未处理的效果类型: {effect.effectType}");
        }

        // ─── 工具 ────────────────────────────────────────────────

        private void EnqueueChain(IEnumerable<EventData> chain)
        {
            if (chain == null) return;
            foreach (var e in chain)
                if (e != null) _pendingChain.Enqueue(e);
        }

        private static void RecordEncounter(string encounterId, List<string> encounterIds)
        {
            string resolvedId = encounterId?.Trim() ?? string.Empty;
            if (encounterIds == null)
            {
                EventBus.Publish(new PlayableEventEncounterRequestedEvent { EncounterId = resolvedId, SourceEventId = string.Empty });
                return;
            }
            if (!encounterIds.Contains(resolvedId))
                encounterIds.Add(resolvedId);
        }

        private static void PublishEncounters(IEnumerable<string> encounterIds, string sourceEventId)
        {
            foreach (string encounterId in encounterIds)
                EventBus.Publish(new PlayableEventEncounterRequestedEvent { EncounterId = encounterId, SourceEventId = sourceEventId ?? string.Empty });
        }

        private void MarkEventCompleted(EventData gameEvent)
        {
            if (gameEvent == null) return;
            var entry = _settlement.Timeline.FindLast(item => item.EventId == gameEvent.name && !item.IsCompleted);
            if (entry != null)
                entry.IsCompleted = true;
        }

        private static SettlementEffectKind ToCoreEffectKind(EventEffectType effectType)
        {
            return effectType switch
            {
                EventEffectType.AddResource => SettlementEffectKind.AddResource,
                EventEffectType.RemoveResource => SettlementEffectKind.RemoveResource,
                EventEffectType.AddWillpower => SettlementEffectKind.AddWillpower,
                EventEffectType.RemoveWillpower => SettlementEffectKind.RemoveWillpower,
                EventEffectType.AddLuck => SettlementEffectKind.AddLuck,
                EventEffectType.AddInsanity => SettlementEffectKind.AddInsanity,
                EventEffectType.AddCourage => SettlementEffectKind.AddCourage,
                EventEffectType.AddUnderstanding => SettlementEffectKind.AddUnderstanding,
                EventEffectType.AddTrait => SettlementEffectKind.AddTrait,
                EventEffectType.AddAilment => SettlementEffectKind.AddAilment,
                EventEffectType.UnlockInvention => SettlementEffectKind.UnlockInvention,
                EventEffectType.TriggerCombat => SettlementEffectKind.TriggerCombat,
                EventEffectType.AdvanceYear => SettlementEffectKind.AdvanceYear,
                _ => SettlementEffectKind.Unsupported
            };
        }

        private int GetCheckBonus(HunterInstance hunter, CheckType checkType)
        {
            if (hunter == null) return 0;
            return checkType switch
            {
                CheckType.Courage       => hunter.Courage,
                CheckType.Luck          => hunter.Luck,
                CheckType.Strength      => hunter.Stats.strength,
                CheckType.Evasion       => hunter.Stats.evasion,
                CheckType.Understanding => hunter.Understanding,
                _ => 0
            };
        }
    }

    // ─── 结果数据 ────────────────────────────────────────────────

    public struct EventResolutionResult
    {
        public bool   Success;
        public int    RollValue;
        public string ResultText;
    }

    public struct RerollResult
    {
        public bool Success;
        public int  NewRoll;
        public int  FinalRoll;
    }
}
