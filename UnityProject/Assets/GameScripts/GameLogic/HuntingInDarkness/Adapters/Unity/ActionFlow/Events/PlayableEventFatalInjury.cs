using System;
using System.Collections.Generic;
using Core;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Events
{
    public interface IPlayableEventFatalInjuryCommand
    {
        bool TryPrepare(EventEffect effect, HunterInstance actor, out PlayableEventFatalInjuryPreparation preparation, out string reason);
        bool TryCommit(PlayableEventFatalInjuryPreparation preparation, int facedownPosition, string eventId, int effectIndex, out PlayableEventEffectResult result, out string reason);
    }

    public sealed class PlayableEventFatalInjuryPreparation
    {
        private readonly string deckId;
        private readonly IReadOnlyList<DeathCardType> facedownCardTypes;

        internal PlayableEventFatalInjuryPreparation(EventEffect effect, HunterInstance actor, CharacterCombatStats combatStats, EventFatalInjuryPlan plan)
        {
            Effect = effect;
            Actor = actor;
            CombatStats = combatStats;
            Plan = plan;
            deckId = effect?.fatalDeckId?.Trim() ?? string.Empty;
            facedownCardTypes = plan.FacedownCardTypes;
        }

        internal EventEffect Effect { get; }
        internal HunterInstance Actor { get; }
        internal CharacterCombatStats CombatStats { get; }
        internal EventFatalInjuryPlan Plan { get; }
        public string DeckId => deckId;
        public int DeckSize => Plan.DeckSize;
        public bool RequiresDeathDraw => Plan.RequiresDeathDraw;
        public IReadOnlyList<DeathCardType> FacedownCardTypes => facedownCardTypes;
        public int SelectedPosition { get; private set; } = -1;

        internal void SetSelectedPosition(int position) => SelectedPosition = position;
    }

    /// <summary>把 Hunt 事件致命伤计划映射回持久化猎人，并复用 IHunterDeathCommand aftermath。</summary>
    public sealed class PlayableHuntFatalInjuryCommand : IPlayableEventFatalInjuryCommand
    {
        private readonly SettlementInstance settlement;
        private readonly IRandomSource effectRandom;
        private readonly IRandomSource shuffleRandom;
        private readonly IHunterDeathCommand hunterDeathCommand;

        public PlayableHuntFatalInjuryCommand(SettlementInstance settlement, IRandomSource random, IHunterDeathCommand hunterDeathCommand)
            : this(settlement, random, random, hunterDeathCommand)
        {
        }

        public PlayableHuntFatalInjuryCommand(SettlementInstance settlement, IRandomSource effectRandom, IRandomSource shuffleRandom, IHunterDeathCommand hunterDeathCommand)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.effectRandom = effectRandom ?? throw new ArgumentNullException(nameof(effectRandom));
            this.shuffleRandom = shuffleRandom ?? throw new ArgumentNullException(nameof(shuffleRandom));
            this.hunterDeathCommand = hunterDeathCommand;
        }

        public bool TryPrepare(EventEffect effect, HunterInstance actor, out PlayableEventFatalInjuryPreparation preparation, out string reason)
        {
            preparation = null;
            if (effect?.effectType != EventEffectType.FatalInjury)
            {
                reason = "事件效果不是致命伤。";
                return false;
            }
            if (!string.Equals(effect.targetName?.Trim(), "selected", StringComparison.OrdinalIgnoreCase))
            {
                reason = "致命伤必须作用于选中猎人。";
                return false;
            }
            if (!EventFatalInjuryRules.IsValidDeckId(effect.fatalDeckId))
            {
                reason = "致命伤牌堆 ID 无效。";
                return false;
            }
            if (!HunterRecoveryRules.TryParseBodyPart(effect.bodyPart, out HunterBodyPart bodyPart) || effect.value <= 0)
            {
                reason = "致命伤部位或伤害值无效。";
                return false;
            }
            if (actor == null || !actor.IsAlive || !ReferenceEquals(settlement.GetHunter(actor.InstanceId), actor))
            {
                reason = "事件没有属于当前营地且仍存活的猎人执行者。";
                return false;
            }

            var combatStats = new CharacterCombatStats();
            PlayableHunterInjuryAdapter.Apply(actor, combatStats);
            if (!EventFatalInjuryRules.TryPrepare(combatStats.InjuryState, bodyPart, effect.value, shuffleRandom, out EventFatalInjuryPlan plan, out reason)) return false;
            if (plan.RequiresDeathDraw && hunterDeathCommand == null)
            {
                reason = "致命伤死亡命令端口尚未注入。";
                return false;
            }
            preparation = new PlayableEventFatalInjuryPreparation(effect, actor, combatStats, plan);
            return true;
        }

        public bool TryCommit(PlayableEventFatalInjuryPreparation preparation, int facedownPosition, string eventId, int effectIndex, out PlayableEventEffectResult result, out string reason)
        {
            result = default;
            if (preparation == null || preparation.Actor == null || preparation.CombatStats == null)
            {
                reason = "致命伤准备结果缺失。";
                return false;
            }
            if (preparation.RequiresDeathDraw && (facedownPosition < 0 || facedownPosition >= preparation.DeckSize))
            {
                reason = "死亡牌堆选位无效。";
                return false;
            }

            HunterDamageResult damage;
            try
            {
                damage = preparation.Plan.Commit(effectRandom, PlayablePermanentInjuryRuntime.Resolver, facedownPosition);
            }
            catch (Exception exception)
            {
                reason = string.IsNullOrWhiteSpace(exception.Message) ? "致命伤计划已经失效。" : exception.Message;
                return false;
            }

            if (damage.IsDead)
            {
                string causeId = string.IsNullOrWhiteSpace(eventId) ? "hunt_fatal_injury" : $"{eventId}:fatal-injury";
                if (hunterDeathCommand == null)
                {
                    reason = "致命伤死亡命令端口尚未注入。";
                    return false;
                }
                if (!hunterDeathCommand.TryKill(preparation.Actor, causeId, preparation.Effect.description, out reason))
                    return false;
            }
            PlayableHunterInjuryAdapter.Sync(preparation.Actor, preparation.CombatStats);
            preparation.SetSelectedPosition(facedownPosition);
            string bodyPartId = HunterRecoveryRules.GetBodyPartId(damage.BodyPart);
            result = new PlayableEventEffectResult(effectIndex, preparation.Effect, PlayableEventEffectStatus.Applied, string.Empty, eventId, bodyPartId, preparation.Actor.InstanceId, damage.FatalInjuryTriggered || damage.HealthLost > 0 || damage.PermanentInjury != null, preparation.Plan.PreviousHealth, damage.RemainingHealth, damage.DeathDraw?.Card, damage.PermanentInjury?.Id, damage.IsDead, preparation.DeckId, facedownPosition);
            reason = string.Empty;
            return true;
        }
    }

    public struct HuntFatalInjuryResolvedEvent
    {
        public string EventId;
        public int EffectIndex;
        public int HunterId;
        public string BodyPartId;
        public string DeathDeckId;
        public int FacedownPosition;
        public bool FatalInjuryTriggered;
        public bool Survived;
        public bool HunterDied;
        public string PermanentInjuryId;
    }
}
