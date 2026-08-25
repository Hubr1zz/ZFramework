using System;
using System.Collections.Generic;
using Core;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ContentTables;
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
        private readonly IHunterDeathCommand hunterDeathCommand;

        /// <summary>当事件需要展示 UI 时，调用此回调（SettlementUIManager 注入）</summary>
        public System.Action<EventData, HunterInstance> OnEventTriggered;

        /// <summary>当事件结束（包含子事件链全部处理完）后调用</summary>
        public System.Action OnEventChainCompleted;
        internal SettlementInstance Settlement => _settlement;

        // 当前处理中的事件队列（子事件链用）
        private readonly Queue<EventData> _pendingChain = new();
        private HunterInstance            _selectedHunter;

        public EventSystem(SettlementInstance settlement, IRandomSource rng, IDelayedEventScheduler delayedEventScheduler = null, IHunterDeathCommand hunterDeathCommand = null)
        {
            _settlement = settlement;
            _rng        = rng;
            this.delayedEventScheduler = delayedEventScheduler;
            this.hunterDeathCommand = hunterDeathCommand;
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
            MarkEventCompleted(evt);
            if (result.EncounterIds.Count > 0)
            {
                _pendingChain.Clear();
                return;
            }
            EnqueueChain(result.ChainedEvents);
            ProcessNextInChain();
        }

        /// <summary>结算单个叙事节点并返回后续节点，不触碰共享事件队列。</summary>
        public IReadOnlyList<EventData> ResolveNarrativeStandalone(EventData gameEvent, HunterInstance actor = null, IPlayableEventResourceCommand resourceCommand = null)
        {
            PlayableEventNodeCommitResult result = ResolveNarrativeNode(gameEvent, actor, false, resourceCommand);
            return result.EncounterIds.Count > 0 ? System.Array.Empty<EventData>() : result.ChainedEvents;
        }

        /// <summary>结算单个节点并捕获跨环境遭遇请求，避免 Action 流程依赖全局字符串事件。</summary>
        public PlayableEventNodeCommitResult ResolveNarrativeNodeStandalone(EventData gameEvent, HunterInstance actor = null, IPlayableEventResourceCommand resourceCommand = null)
        {
            return ResolveNarrativeNode(gameEvent, actor, true, resourceCommand);
        }

        private PlayableEventNodeCommitResult ResolveNarrativeNode(EventData gameEvent, HunterInstance actor, bool captureEncounterRequests, IPlayableEventResourceCommand resourceCommand = null)
        {
            if (gameEvent == null) return new PlayableEventNodeCommitResult(System.Array.Empty<EventData>(), System.Array.Empty<string>(), PlayableEventEffectBatchResult.Empty);
            var encounterIds = new List<string>();
            var effectResults = new List<PlayableEventEffectResult>();
            if (gameEvent.eventType == GameEventType.Combat && !string.IsNullOrWhiteSpace(gameEvent.combatEncounterId))
                RecordEncounter(gameEvent.combatEncounterId, encounterIds);
            if (gameEvent.immediateEffects != null)
                for (int effectIndex = 0; effectIndex < gameEvent.immediateEffects.Count; effectIndex++)
                    effectResults.Add(ApplyEffect(gameEvent.immediateEffects[effectIndex], actor, actor, encounterIds, resourceCommand, effectIndex, gameEvent.ContentId));
            if (gameEvent.eventType == GameEventType.Combat && encounterIds.Count == 0)
                RecordEncounter(gameEvent.combatEncounterId, encounterIds);
            if (!captureEncounterRequests)
                PublishEncounters(encounterIds, gameEvent.name);
            return new PlayableEventNodeCommitResult(gameEvent.chainedEvents, encounterIds, new PlayableEventEffectBatchResult(effectResults));
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
            bool requiresHunter = option.checkType != CheckType.None || PlayableEventOptionAvailability.RequiresHunter(option);
            if (requiresHunter && actor == null)
                return new EventResolutionResult { Success = false, ResultText = "该选项需要一名猎人执行。" };
            if (requiresHunter && !ReferenceEquals(_settlement.GetHunter(actor.InstanceId), actor))
                return new EventResolutionResult { Success = false, ResultText = "所选猎人不属于当前营地。" };
            if (PlayableEventOptionAvailability.HasHunterDeathEffect(option) && hunterDeathCommand == null)
                return new EventResolutionResult { Success = false, ResultText = "猎人死亡流程尚未准备完成。" };
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
            var effectResults = new List<PlayableEventEffectResult>();
            if (evt.eventType == GameEventType.Combat && !string.IsNullOrWhiteSpace(evt.combatEncounterId))
                RecordEncounter(evt.combatEncounterId, encounterIds);
            if (effects != null)
                for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                    effectResults.Add(ApplyEffect(effects[effectIndex], actor, actor, encounterIds, null, effectIndex, evt.ContentId));
            if (evt.eventType == GameEventType.Combat && encounterIds.Count == 0)
                RecordEncounter(evt.combatEncounterId, encounterIds);
            bool campaignEnded = _settlement.GetAliveHunters().Count == 0;
            if (campaignEnded)
                encounterIds.Clear();
            else
                PublishEncounters(encounterIds, evt.name);
            MarkEventCompleted(evt);

            var result = new EventResolutionResult
            {
                Success = success,
                RollValue = rollValue,
                ResultText = success ? option.successText : option.failText,
                EffectResults = new PlayableEventEffectBatchResult(effectResults)
            };
            if (campaignEnded)
            {
                _pendingChain.Clear();
                return result;
            }
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
            PlayableEventEffectResult result = ApplyEffect(effect, target, _selectedHunter ?? target, null, null, -1);
            if (!result.Succeeded)
            {
                if (effect?.effectType == EventEffectType.UnlockInvention && result.Reason.StartsWith("未注册发明："))
                    Debug.LogWarning($"[EventSystem] 无法解锁未注册发明：{effect.targetName}");
                else
                    Debug.LogWarning($"[EventSystem] 效果未应用：{result.Reason}");
            }
        }

        private PlayableEventEffectResult ApplyEffect(EventEffect effect, HunterInstance target, HunterInstance eventActor, List<string> encounterIds = null, IPlayableEventResourceCommand resourceCommand = null, int effectIndex = -1, string eventId = "")
        {
            if (effect == null) return FailedEffect(effectIndex, effect, "事件效果为空。", eventId);
            if (effect.effectType == EventEffectType.ActivateBloodline)
            {
                HunterInstance actor = target ?? eventActor;
                if (actor == null)
                    return FailedEffect(effectIndex, effect, "事件没有猎人执行者。", eventId);
                if (!PlayableBloodlineRuntime.Content.TryGet(effect.targetName, out HunterBloodlineDefinition bloodline))
                    return FailedEffect(effectIndex, effect, $"未注册血脉：{effect.targetName}", eventId);
                if (!HunterBloodlineRules.TryActivate(actor, bloodline.Id, out string reason))
                    return FailedEffect(effectIndex, effect, reason, eventId);
                actor.BloodlineName = bloodline.DisplayName;
                return SucceededEffect(effectIndex, effect, eventId);
            }
            if (effect.effectType == EventEffectType.ScheduleEvent)
            {
                if (delayedEventScheduler == null)
                    return FailedEffect(effectIndex, effect, "Timeline 未注入。", eventId);
                if (!delayedEventScheduler.TryScheduleEventAfterYears(effect.targetName, effect.value, out string reason))
                    return FailedEffect(effectIndex, effect, reason, eventId);
                return SucceededEffect(effectIndex, effect, eventId);
            }
            if (effect.effectType == EventEffectType.KillHunter)
            {
                HunterInstance actor = target ?? eventActor;
                if (actor == null)
                    return FailedEffect(effectIndex, effect, "事件没有猎人执行者。", eventId);
                if (hunterDeathCommand == null)
                    return FailedEffect(effectIndex, effect, "死亡命令端口尚未注入。", eventId);
                if (!hunterDeathCommand.TryKill(actor, effect.targetName, effect.description, out string reason))
                    return FailedEffect(effectIndex, effect, reason, eventId);
                return SucceededEffect(effectIndex, effect, eventId);
            }
            if (effect.effectType == EventEffectType.AddAilment)
            {
                HunterInstance actor = target ?? eventActor;
                if (actor == null || !ReferenceEquals(_settlement.GetHunter(actor.InstanceId), actor))
                    return FailedEffect(effectIndex, effect, "事件没有属于当前营地的猎人执行者。", eventId);
                if (!PlayableSymptomRuntime.TryAcquire(actor, effect.targetName, out SymptomDefinition definition, out bool added, out string reason))
                    return FailedEffect(effectIndex, effect, reason, eventId);
                return SucceededEffect(effectIndex, effect, eventId, definition.Id, actor.InstanceId, added);
            }
            if (effect.effectType == EventEffectType.AddRecoverableWound)
            {
                HunterInstance actor = target ?? eventActor;
                if (actor == null || !ReferenceEquals(_settlement.GetHunter(actor.InstanceId), actor))
                    return FailedEffect(effectIndex, effect, "事件没有属于当前营地的猎人执行者。", eventId);
                if (!string.Equals(effect.targetName?.Trim(), "selected", StringComparison.OrdinalIgnoreCase))
                    return FailedEffect(effectIndex, effect, "普通伤势必须明确作用于选中猎人。", eventId);
                if (!HunterRecoveryRules.TryApplyRecoverableWound(actor, effect.bodyPart, effect.value, out HunterRecoverableWoundResult wound, out string reason))
                    return FailedEffect(effectIndex, effect, reason, eventId);
                string bodyPartId = HunterRecoveryRules.GetBodyPartId(wound.BodyPart);
                return SucceededEffect(effectIndex, effect, eventId, bodyPartId, actor.InstanceId, wound.Changed, wound.PreviousHealth, wound.CurrentHealth);
            }

            if (resourceCommand != null && (effect.effectType == EventEffectType.AddResource || effect.effectType == EventEffectType.RemoveResource))
            {
                string resourceId = PlayableSettlementItemRegistry.ResolveContentId(effect.targetName);
                if (!resourceCommand.TryApply(effect.effectType, resourceId, effect.value, eventActor ?? target, out PlayableEventResourceChange change, out string reason))
                    return FailedEffect(effectIndex, effect, reason, eventId);
                if (change.Changed)
                {
                    EventBus.Publish(new PlayableEventResourceChangedEvent
                    {
                        Scope = change.Scope,
                        ResourceId = change.ResourceId,
                        OldAmount = change.OldAmount,
                        NewAmount = change.NewAmount
                    });
                }
                return SucceededEffect(effectIndex, effect, eventId);
            }

            string targetId = effect.targetName;
            InventionData targetInvention = null;
            if (effect.effectType == EventEffectType.AddResource || effect.effectType == EventEffectType.RemoveResource)
                targetId = PlayableSettlementItemRegistry.ResolveContentId(effect.targetName);
            if (effect.effectType == EventEffectType.UnlockInvention && !PlayableSettlementInventionRegistry.TryGet(effect.targetName, out targetInvention))
                return FailedEffect(effectIndex, effect, $"未注册发明：{effect.targetName}", eventId);
            if (targetInvention != null)
                targetId = targetInvention.ContentId;
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

            if (outcome.Handled && (effect.effectType == EventEffectType.AddCourage || effect.effectType == EventEffectType.AddUnderstanding))
                PlayableGrowthMilestoneRuntime.Synchronize(_settlement);
            if (outcome.Handled && effect.effectType == EventEffectType.UnlockInvention)
            {
                SettlementTimelineJournal.RecordInvention(_settlement, targetInvention.ContentId, targetInvention.inventionName);
                if (!PlayableSettlementModifierRuntime.Synchronize(_settlement, PlayableSettlementContentRuntime.Inventions, message => Debug.LogError($"[EventSystem] {message}")))
                    Debug.LogError($"[EventSystem] 发明 {targetInvention.ContentId} 的持续效果同步失败。");
            }

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
                return FailedEffect(effectIndex, effect, string.IsNullOrWhiteSpace(outcome.Reason) ? $"未处理的效果类型：{effect.effectType}" : outcome.Reason, eventId);
            return SucceededEffect(effectIndex, effect, eventId);
        }

        private static PlayableEventEffectResult SucceededEffect(int effectIndex, EventEffect effect, string eventId) => new(effectIndex, effect, PlayableEventEffectStatus.Applied, string.Empty, eventId);

        private static PlayableEventEffectResult SucceededEffect(int effectIndex, EventEffect effect, string eventId, string resolvedTargetId, int targetActorId, bool stateChanged) => new(effectIndex, effect, PlayableEventEffectStatus.Applied, string.Empty, eventId, resolvedTargetId, targetActorId, stateChanged);

        private static PlayableEventEffectResult SucceededEffect(int effectIndex, EventEffect effect, string eventId, string resolvedTargetId, int targetActorId, bool stateChanged, int previousValue, int currentValue) => new(effectIndex, effect, PlayableEventEffectStatus.Applied, string.Empty, eventId, resolvedTargetId, targetActorId, stateChanged, previousValue, currentValue);

        private static PlayableEventEffectResult FailedEffect(int effectIndex, EventEffect effect, string reason, string eventId) => new(effectIndex, effect, PlayableEventEffectStatus.Failed, reason, eventId);

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
            string canonicalId = gameEvent.ContentId;
            var entry = _settlement.Timeline.FindLast(item => item != null && !item.IsCompleted && (item.EventId == canonicalId || item.EventId == gameEvent.name));
            if (entry != null)
                entry.IsCompleted = true;
        }

        internal bool TryMarkTimelineEntryCompleted(AnnalEntry timelineEntry, string eventId)
        {
            if (timelineEntry == null) return true;
            if (_settlement?.Timeline == null || !_settlement.Timeline.Contains(timelineEntry) || timelineEntry.IsCompleted || !PlayableSettlementEventRegistry.IsTimelineEventEntry(timelineEntry) || !string.Equals(timelineEntry.EventId, eventId, System.StringComparison.Ordinal)) return false;
            timelineEntry.IsCompleted = true;
            return true;
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
        public PlayableEventEffectBatchResult EffectResults;
    }

    public struct RerollResult
    {
        public bool Success;
        public int  NewRoll;
        public int  FinalRoll;
    }
}
