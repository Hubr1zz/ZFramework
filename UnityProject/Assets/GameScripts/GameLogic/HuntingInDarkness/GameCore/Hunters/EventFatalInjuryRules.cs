using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Hunters
{
    /// <summary>Hunt 事件致命伤使用的稳定死亡牌堆标识。</summary>
    public static class EventFatalInjuryRules
    {
        public const string HunterDeathDeckId = "hunter-death";

        public static bool IsValidDeckId(string deckId)
        {
            return string.Equals(deckId?.Trim(), HunterDeathDeckId, StringComparison.Ordinal);
        }

        public static bool TryPrepare(HunterInjuryState state, HunterBodyPart bodyPart, int incomingDamage, IRandomSource random, out EventFatalInjuryPlan plan, out string reason)
        {
            plan = default;
            if (state == null)
            {
                reason = "致命伤缺少猎人伤势状态。";
                return false;
            }
            if (random == null)
            {
                reason = "致命伤缺少随机源。";
                return false;
            }
            if (incomingDamage <= 0)
            {
                reason = "致命伤值必须大于零。";
                return false;
            }
            if (!Enum.IsDefined(typeof(HunterBodyPart), bodyPart))
            {
                reason = "致命伤部位无效。";
                return false;
            }

            HunterBodyPartState part = state.GetPart(bodyPart);
            int effectiveDamage = FlatArmorMitigationRule.Instance.GetDamageAfterArmor(incomingDamage, part.Armor);
            bool reachesFatalThreshold = !state.IsDead && effectiveDamage > 0 && part.CurrentHealth <= effectiveDamage;
            DeathDeckDrawOrder drawOrder = reachesFatalThreshold ? state.DeathDeck.PrepareDraw(random) : null;
            plan = new EventFatalInjuryPlan(state, bodyPart, incomingDamage, part.CurrentHealth, state.DeathDeck.Cards.Count, drawOrder);
            reason = string.Empty;
            return true;
        }
    }

    /// <summary>表现完成前只持有本计划；牌堆和伤势状态均在 Commit 后才改变。</summary>
    public readonly struct EventFatalInjuryPlan
    {
        private readonly HunterInjuryState owner;
        private readonly int expectedHealth;
        private readonly int expectedDeckCount;

        internal EventFatalInjuryPlan(HunterInjuryState owner, HunterBodyPart bodyPart, int incomingDamage, int expectedHealth, int expectedDeckCount, DeathDeckDrawOrder drawOrder)
        {
            this.owner = owner;
            BodyPart = bodyPart;
            IncomingDamage = incomingDamage;
            this.expectedHealth = expectedHealth;
            this.expectedDeckCount = expectedDeckCount;
            DrawOrder = drawOrder;
        }

        public HunterBodyPart BodyPart { get; }
        public int IncomingDamage { get; }
        public int PreviousHealth => expectedHealth;
        public DeathDeckDrawOrder DrawOrder { get; }
        public bool RequiresDeathDraw => DrawOrder != null;
        public int DeckSize => expectedDeckCount;
        public IReadOnlyList<DeathCardType> FacedownCardTypes
        {
            get
            {
                if (DrawOrder == null || owner == null)
                    return Array.Empty<DeathCardType>();
                var cards = new DeathCardType[DrawOrder.Count];
                for (int position = 0; position < cards.Length; position++)
                    cards[position] = owner.DeathDeck.Cards[DrawOrder.ResolveCardIndex(position)];
                return Array.AsReadOnly(cards);
            }
        }

        public HunterDamageResult Commit(IRandomSource random, IPermanentInjuryResolver permanentInjuryResolver, int facedownPosition)
        {
            if (owner == null)
                throw new InvalidOperationException("致命伤计划没有所属伤势状态。 ");
            if (owner.IsDead)
                throw new InvalidOperationException("致命伤计划已经提交或所属猎人已经死亡。 ");
            HunterBodyPartState part = owner.GetPart(BodyPart);
            if (part.CurrentHealth != expectedHealth || owner.DeathDeck.Cards.Count != expectedDeckCount)
                throw new InvalidOperationException("致命伤计划对应的猎人状态已经变化。 ");
            if (DrawOrder == null || expectedHealth == 0)
                return owner.ApplyDamage(BodyPart, IncomingDamage, random, permanentInjuryResolver: permanentInjuryResolver, deathDrawOrder: DrawOrder, deathCardPosition: facedownPosition);

            HunterDamageResult healthDamage = owner.ApplyDamage(BodyPart, IncomingDamage, random);
            if (healthDamage.RemainingHealth > 0)
                throw new InvalidOperationException("致命伤计划的伤害不足以触发死亡牌堆。 ");
            HunterDamageResult deathDraw = owner.ApplyDamage(BodyPart, 1, random, armorRule: IgnoreArmorMitigationRule.Instance, permanentInjuryResolver: permanentInjuryResolver, deathDrawOrder: DrawOrder, deathCardPosition: facedownPosition);
            return new HunterDamageResult(BodyPart, IncomingDamage, healthDamage.ArmorPrevented, healthDamage.HealthLost, deathDraw.RemainingHealth, true, deathDraw.DeathDraw, deathDraw.PermanentInjury, deathDraw.IsDead);
        }

        private sealed class IgnoreArmorMitigationRule : IArmorMitigationRule
        {
            public static readonly IgnoreArmorMitigationRule Instance = new IgnoreArmorMitigationRule();

            public int GetDamageAfterArmor(int incomingDamage, int armor) => Math.Max(0, incomingDamage);
        }
    }
}
