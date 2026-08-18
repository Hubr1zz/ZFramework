using CardTactics.CombatSystem;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.Combat;
using UnityEngine;

namespace GameplayBase.CombatSystem
{
    // ═══════════════════════════════════════════
    // Boss攻击角色 — 闪避判定步骤
    // ═══════════════════════════════════════════

    /// <summary>
    /// Boss攻击角色时的闪避判定。
    /// 从 AttackContext.DefenderStats 读取被攻击角色的闪避值。
    /// </summary>
    public class BossAttackDodgeStep : IAttackStep
    {
        private readonly int accuracy;
        private readonly int attemptIndex;
        private readonly int attemptCount;

        public BossAttackDodgeStep(int accuracy = 1, int attemptIndex = 1, int attemptCount = 1)
        {
            this.accuracy = Mathf.Max(1, accuracy);
            this.attemptIndex = Mathf.Max(1, attemptIndex);
            this.attemptCount = Mathf.Max(this.attemptIndex, attemptCount);
        }

        public async UniTask Execute(AttackContext context, IPlayerInputProvider input)
        {
            var defenderStats = context.DefenderStats;
            if (defenderStats == null || defenderStats.IsDead)
                return;

            BossHitDeckComposition deck = BossHitDeckRules.Build(accuracy, defenderStats.Evasion);
            if (deck.IsAutomaticHit)
            {
                context.RollResult = 0;
                context.HitResult = HitResult.Success;
                Debug.Log($"[Combat] Boss攻击 [{attemptIndex}/{attemptCount}]: 敏捷为0，自动命中");
                return;
            }

            float dodgeRate = 100f * deck.DodgeCards / deck.TotalCards;
            string prompt =
                $"<b>Boss 命中牌堆</b> [{attemptIndex}/{attemptCount}]\n" +
                $"怪物精准 {accuracy}：命中牌 {deck.HitCards} 张\n" +
                $"猎人敏捷 {defenderStats.Evasion}：闪避牌 {deck.DodgeCards} 张\n" +
                $"闪避率 {dodgeRate:0.#}%";

            if (input is IBossHitDeckInputProvider deckInput)
                context.RollResult = await deckInput.RequestDrawBossHitResult(prompt, deck);
            else
                context.RollResult = await input.RequestRoll(prompt, deck.TotalCards);

            BossHitDeckDraw draw = BossHitDeckRules.ResolveDraw(deck, context.RollResult);
            context.HitResult = draw.IsHit ? HitResult.Success : HitResult.Failure;

            Debug.Log($"[Combat] Boss攻击 [{attemptIndex}/{attemptCount}]: 结果牌 {draw.Card}（命中 {deck.HitCards} / 闪避 {deck.DodgeCards}）");

            string resultMsg = draw.IsHit
                ? "未能闪避，被Boss命中！"
                : "闪避成功！躲开了Boss的攻击";
            await input.ShowResult(resultMsg);
        }
    }

    // ═══════════════════════════════════════════
    // Boss攻击角色 — 伤口结算步骤
    // ═══════════════════════════════════════════

    /// <summary>
    /// 如果 Boss 攻击命中，按指定身体部位结算护甲、生命与致命伤。
    /// </summary>
    public class BossAttackWoundStep : IAttackStep
    {
        private readonly int _woundCount;
        private readonly HunterBodyPart _bodyPart;
        private readonly IRandomSource _random;
        private readonly IArmorMitigationRule _armorRule;
        private readonly IPermanentInjuryResolver _permanentInjuryResolver;
        private readonly ISurvivalEventResolver _survivalEventResolver;

        public BossAttackWoundStep(
            int woundCount,
            HunterBodyPart bodyPart,
            IRandomSource random,
            IArmorMitigationRule armorRule = null,
            IPermanentInjuryResolver permanentInjuryResolver = null,
            ISurvivalEventResolver survivalEventResolver = null)
        {
            _woundCount = woundCount;
            _bodyPart = bodyPart;
            _random = random ?? throw new System.ArgumentNullException(nameof(random));
            _armorRule = armorRule;
            _permanentInjuryResolver = permanentInjuryResolver;
            _survivalEventResolver = survivalEventResolver;
        }

        public async UniTask Execute(AttackContext context, IPlayerInputProvider input)
        {
            if (context.HitResult != HitResult.Success || context.DefenderStats == null || context.DefenderStats.IsDead)
                return;

            var defenderStats = context.DefenderStats;
            string bodyPartName = GetBodyPartDisplayName(_bodyPart);
            DeathDeckDrawOrder deathDrawOrder = null;
            int deathCardPosition = 0;
            if (defenderStats.WillTriggerFatalInjury(_bodyPart, _woundCount, _armorRule) && input is IDeathDeckInputProvider deathDeckInput)
            {
                DeathDeck deck = defenderStats.InjuryState.DeathDeck;
                var composition = new DeathDeckComposition(deck.SurvivalCardCount, deck.DeathCardCount);
                await input.ShowResult($"<b>死亡判定</b>\n\n这次伤害会击中已经归零的{bodyPartName}。\n当前牌堆：存活 {composition.SurvivalCards} 张 / 死亡 {composition.DeathCards} 张。\n\n确认后所有牌将翻至背面并洗混。");
                deathDrawOrder = deck.PrepareDraw(_random);
                deathCardPosition = await deathDeckInput.RequestDrawDeathCard("<b>牌已洗混</b>\n选择一张背面牌并承担结果。", composition);
            }
            HunterDamageResult damage = defenderStats.ApplyDamage(
                _bodyPart,
                _woundCount,
                _random,
                _armorRule,
                _permanentInjuryResolver,
                deathDrawOrder,
                deathCardPosition);

            string msg = $"{bodyPartName}受到 {damage.IncomingDamage} 点伤害，" +
                         $"护甲抵消 {damage.ArmorPrevented}，" +
                         $"剩余生命 {damage.RemainingHealth}。";

            if (damage.IsDead)
            {
                msg += "\n<color=#ff4444>翻开死亡牌：你失去希望。你死了。</color>";
            }
            else
            {
                int permanentWoundsAdded = damage.PermanentInjury != null ? 1 : 0;
                if (permanentWoundsAdded > 0)
                    defenderStats.AddPermanentWounds(permanentWoundsAdded);
                if (damage.FatalInjuryTriggered)
                {
                    msg += "\n<color=#e8c46a>翻开存活牌！</color> 死亡牌堆加入 1 张死亡牌。";
                    if (damage.PermanentInjury != null)
                        msg += $"\n获得永久损伤：{damage.PermanentInjury.DisplayName}";
                }
                EventBus.Publish(new CharacterWoundedEvent
                {
                    CharacterId = context.DefenderId,
                    BodyPart = damage.BodyPart,
                    IncomingDamage = damage.IncomingDamage,
                    ArmorPrevented = damage.ArmorPrevented,
                    HealthLost = damage.HealthLost,
                    RemainingHealth = damage.RemainingHealth,
                    FatalInjuryTriggered = damage.FatalInjuryTriggered,
                    PermanentWoundsAdded = permanentWoundsAdded,
                    TotalTemporaryWounds = defenderStats.TemporaryWounds,
                    TotalPermanentWounds = defenderStats.PermanentWounds
                });
            }

            await input.ShowResult(msg);
            if (damage.IsDead)
                EventBus.Publish(new CharacterDiedEvent { CharacterId = context.DefenderId });
            else if (damage.FatalInjuryTriggered && _survivalEventResolver != null)
                await _survivalEventResolver.ResolveAsync(context.DefenderId, damage, input);
        }

        private static string GetBodyPartDisplayName(HunterBodyPart bodyPart)
        {
            switch (bodyPart)
            {
                case HunterBodyPart.Head: return "头部";
                case HunterBodyPart.Torso: return "躯干";
                case HunterBodyPart.Arms: return "手臂";
                case HunterBodyPart.Legs: return "腿部";
                default: return bodyPart.ToString();
            }
        }
    }
}
