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
    /// 事件规则与效果解析器（纯 C#）。
    /// 由阶段 ActionQueue 传入显式事件、执行者与效果端口；不持有事件队列或 View 回调。
    /// </summary>
    public partial class EventSystem
    {
        private readonly SettlementInstance _settlement;
        private readonly IRandomSource      _rng;
        private readonly IDelayedEventScheduler delayedEventScheduler;
        private readonly IHunterDeathCommand hunterDeathCommand;

        internal SettlementInstance Settlement => _settlement;
        internal IRandomSource RandomSource => _rng;
        internal IHunterDeathCommand HunterDeathCommand => hunterDeathCommand;

        public EventSystem(SettlementInstance settlement, IRandomSource rng, IDelayedEventScheduler delayedEventScheduler = null, IHunterDeathCommand hunterDeathCommand = null)
        {
            _settlement = settlement;
            _rng        = rng;
            this.delayedEventScheduler = delayedEventScheduler;
            this.hunterDeathCommand = hunterDeathCommand;
        }

        /// <summary>结算单个叙事节点并返回后续节点，不触碰共享事件队列。</summary>
        public IReadOnlyList<EventData> ResolveNarrativeStandalone(EventData gameEvent, HunterInstance actor = null, IPlayableEventResourceCommand resourceCommand = null, IPlayableEventWorldCommand worldCommand = null, IPlayableEventSettlementCommand settlementCommand = null)
        {
            PlayableEventNodeCommitResult result = ResolveNarrativeNode(gameEvent, actor, false, resourceCommand, worldCommand, settlementCommand);
            return result.EncounterIds.Count > 0 ? System.Array.Empty<EventData>() : result.ChainedEvents;
        }

        /// <summary>结算单个节点并捕获跨环境遭遇请求，避免 Action 流程依赖全局字符串事件。</summary>
        public PlayableEventNodeCommitResult ResolveNarrativeNodeStandalone(EventData gameEvent, HunterInstance actor = null, IPlayableEventResourceCommand resourceCommand = null, IPlayableEventWorldCommand worldCommand = null, IPlayableEventSettlementCommand settlementCommand = null, IPlayableEventItemCommand itemCommand = null, IPlayableEventPopulationCommand populationCommand = null)
        {
            return ResolveNarrativeNode(gameEvent, actor, true, resourceCommand, worldCommand, settlementCommand, itemCommand, populationCommand);
        }

        private PlayableEventNodeCommitResult ResolveNarrativeNode(EventData gameEvent, HunterInstance actor, bool captureEncounterRequests, IPlayableEventResourceCommand resourceCommand = null, IPlayableEventWorldCommand worldCommand = null, IPlayableEventSettlementCommand settlementCommand = null, IPlayableEventItemCommand itemCommand = null, IPlayableEventPopulationCommand populationCommand = null)
        {
            if (gameEvent == null) return new PlayableEventNodeCommitResult(System.Array.Empty<EventData>(), System.Array.Empty<string>(), PlayableEventEffectBatchResult.Empty);
            var encounterIds = new List<string>();
            var effectResults = new List<PlayableEventEffectResult>();
            if (gameEvent.eventType == GameEventType.Combat && !string.IsNullOrWhiteSpace(gameEvent.combatEncounterId))
                RecordEncounter(gameEvent.combatEncounterId, encounterIds);
            if (gameEvent.immediateEffects != null)
                for (int effectIndex = 0; effectIndex < gameEvent.immediateEffects.Count; effectIndex++)
                    effectResults.Add(ApplyEffect(gameEvent.immediateEffects[effectIndex], actor, actor, encounterIds, resourceCommand, worldCommand, settlementCommand, effectIndex, gameEvent.ContentId, itemCommand, populationCommand));
            if (gameEvent.eventType == GameEventType.Combat && encounterIds.Count == 0)
                RecordEncounter(gameEvent.combatEncounterId, encounterIds);
            if (!captureEncounterRequests)
                PublishEncounters(encounterIds, gameEvent.name);
            return new PlayableEventNodeCommitResult(gameEvent.chainedEvents, encounterIds, new PlayableEventEffectBatchResult(effectResults));
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

        public RerollResult TryReroll(HunterInstance hunter, int currentRoll, int newRoll, int minimumRoll = 1, int maximumRoll = 10)
        {
            if (newRoll < minimumRoll || newRoll > maximumRoll) return new RerollResult { Success = false, FinalRoll = currentRoll };
            RerollOutcome outcome = EventRules.TryReroll(hunter, currentRoll, newRoll, minimumRoll, maximumRoll);
            if (!outcome.Success) return new RerollResult { Success = false, FinalRoll = currentRoll };
            Debug.Log($"[EventSystem] 物理重投 {hunter.Name}：{currentRoll} → {outcome.NewRoll}（取最高 {outcome.FinalRoll}）");
            return new RerollResult { Success = true, NewRoll = outcome.NewRoll, FinalRoll = outcome.FinalRoll };
        }

        // ─── 效果执行 ────────────────────────────────────────────

        public void ApplyEffect(EventEffect effect, HunterInstance target)
        {
            PlayableEventEffectResult result = ApplyEffect(effect, target, target, null, null, null, null, -1);
            if (!result.Succeeded)
            {
                if (effect?.effectType == EventEffectType.UnlockInvention && result.Reason.StartsWith("未注册发明："))
                    Debug.LogWarning($"[EventSystem] 无法解锁未注册发明：{effect.targetName}");
                else
                    Debug.LogWarning($"[EventSystem] 效果未应用：{result.Reason}");
            }
        }

        private PlayableEventEffectResult ApplyEffect(EventEffect effect, HunterInstance target, HunterInstance eventActor, List<string> encounterIds = null, IPlayableEventResourceCommand resourceCommand = null, IPlayableEventWorldCommand worldCommand = null, IPlayableEventSettlementCommand settlementCommand = null, int effectIndex = -1, string eventId = "", IPlayableEventItemCommand itemCommand = null, IPlayableEventPopulationCommand populationCommand = null, IPlayableEventFatalInjuryCommand fatalInjuryCommand = null, IReadOnlyDictionary<int, PlayableEventFatalInjuryPreparation> fatalInjuryPreparations = null)
        {
            if (effect == null) return FailedEffect(effectIndex, effect, "事件效果为空。", eventId);
            if (effect.effectType == EventEffectType.AdvanceYear)
                return FailedEffect(effectIndex, effect, "推进年份效果已禁用；年份只能由回营日历提交。", eventId);
            if (effect.effectType == EventEffectType.CreateHuntNoiseLease)
            {
                if (settlementCommand == null)
                    return FailedEffect(effectIndex, effect, "营地风险租约端口尚未注入。", eventId);
                if (!settlementCommand.TryApply(effect, out PlayableHuntNoiseLeaseChange change, out string reason))
                    return FailedEffect(effectIndex, effect, reason, eventId);
                return SucceededEffect(effectIndex, effect, eventId, change.LeaseId, 0, change.Changed, 0, change.NoiseModifier);
            }
            if (effect.effectType == EventEffectType.ExhaustCurrentHuntTileResources)
            {
                if (worldCommand == null)
                    return FailedEffect(effectIndex, effect, "狩猎事件世界效果端口尚未注入。", eventId);
                if (!worldCommand.TryApply(effect, out PlayableEventWorldChange change, out string reason))
                    return FailedEffect(effectIndex, effect, reason, eventId);
                return SucceededEffect(effectIndex, effect, eventId, change.TargetId, 0, change.Changed, 0, change.AffectedCount);
            }
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
            if (effect.effectType == EventEffectType.FatalInjury)
            {
                if (fatalInjuryCommand == null || fatalInjuryPreparations == null || !fatalInjuryPreparations.TryGetValue(effectIndex, out PlayableEventFatalInjuryPreparation preparation))
                    return FailedEffect(effectIndex, effect, "致命伤效果必须由 Hunt ActionQueue 在桌面表现后提交。", eventId);
                return fatalInjuryCommand.TryCommit(preparation, preparation.SelectedPosition, eventId, effectIndex, out PlayableEventEffectResult result, out string reason)
                    ? result
                    : FailedEffect(effectIndex, effect, reason, eventId);
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

            if (effect.effectType == EventEffectType.AddItem || effect.effectType == EventEffectType.RemoveItem)
            {
                if (itemCommand == null)
                    return FailedEffect(effectIndex, effect, "狩猎物品变化端口尚未注入。", eventId);
                string itemId = PlayableSettlementItemRegistry.ResolveContentId(effect.targetName);
                HunterInstance actor = eventActor ?? target;
                PlayableEventItemChange change;
                string reason;
                bool applied = effect.effectType == EventEffectType.AddItem
                    ? itemCommand.TryAdd(itemId, effect.value, actor, out change, out reason)
                    : itemCommand.TryRemove(itemId, effect.value, actor, out change, out reason);
                if (!applied)
                    return FailedEffect(effectIndex, effect, reason, eventId);
                if (change.Changed)
                {
                    EventBus.Publish(new PlayableEventItemChangedEvent { ItemId = change.ItemId, ActorId = change.ActorId, OldAmount = change.OldAmount, NewAmount = change.NewAmount });
                }
                return SucceededEffect(effectIndex, effect, eventId, change.ItemId, change.ActorId, change.Changed, change.OldAmount, change.NewAmount);
            }

            if (effect.effectType == EventEffectType.RescuePopulation)
            {
                if (populationCommand == null)
                    return FailedEffect(effectIndex, effect, "狩猎人口救援端口尚未注入。", eventId);
                if (!string.IsNullOrWhiteSpace(effect.targetName) || !string.IsNullOrWhiteSpace(effect.bodyPart))
                    return FailedEffect(effectIndex, effect, "救援人口效果不得指定目标内容或部位。", eventId);
                HunterInstance actor = eventActor ?? target;
                if (!populationCommand.TryRescue(effect.value, actor, out PlayableEventPopulationChange change, out string reason))
                    return FailedEffect(effectIndex, effect, reason, eventId);
                return SucceededEffect(effectIndex, effect, eventId, "rescued-population", actor?.InstanceId ?? 0, change.Changed, change.OldAmount, change.NewAmount);
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
                if (effect.effectType == EventEffectType.AddResource && outcome.NewAmount > outcome.OldAmount)
                    _settlement.DiscoverMaterial(outcome.ResourceId);
                EventBus.Publish(new ResourceChangedEvent
                {
                    ResourceName = outcome.ResourceId,
                    OldAmount = outcome.OldAmount,
                    NewAmount = outcome.NewAmount
                });
            }

            if (outcome.TriggerCombat)
                RecordEncounter(effect.targetName, encounterIds);
            if (!outcome.Handled)
                return FailedEffect(effectIndex, effect, string.IsNullOrWhiteSpace(outcome.Reason) ? $"未处理的效果类型：{effect.effectType}" : outcome.Reason, eventId);
            return SucceededEffect(effectIndex, effect, eventId);
        }

        private static PlayableEventEffectResult SucceededEffect(int effectIndex, EventEffect effect, string eventId) => new(effectIndex, effect, PlayableEventEffectStatus.Applied, string.Empty, eventId);

        private static PlayableEventEffectResult SucceededEffect(int effectIndex, EventEffect effect, string eventId, string resolvedTargetId, int targetActorId, bool stateChanged) => new(effectIndex, effect, PlayableEventEffectStatus.Applied, string.Empty, eventId, resolvedTargetId, targetActorId, stateChanged);

        private static PlayableEventEffectResult SucceededEffect(int effectIndex, EventEffect effect, string eventId, string resolvedTargetId, int targetActorId, bool stateChanged, int previousValue, int currentValue) => new(effectIndex, effect, PlayableEventEffectStatus.Applied, string.Empty, eventId, resolvedTargetId, targetActorId, stateChanged, previousValue, currentValue);

        private static PlayableEventEffectResult FailedEffect(int effectIndex, EventEffect effect, string reason, string eventId) => new(effectIndex, effect, PlayableEventEffectStatus.Failed, reason, eventId);

        // ─── 工具 ────────────────────────────────────────────────

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
