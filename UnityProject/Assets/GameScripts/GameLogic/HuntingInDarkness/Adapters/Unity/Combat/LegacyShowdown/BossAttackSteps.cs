using CardTactics.CombatSystem;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
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
        public async UniTask Execute(AttackContext context, IPlayerInputProvider input)
        {
            var defenderStats = context.DefenderStats;

            string prompt =
                $"Boss 发动攻击！\n" +
                $"你的闪避值: {defenderStats.Evasion}\n" +
                $"点击掷骰判定闪避";

            int roll = await input.RequestRoll(prompt, 100);
            context.RollResult = roll;

            bool dodged = CombatRules.IsBossAttackDodged(roll, defenderStats);
            context.HitResult = dodged ? HitResult.Failure : HitResult.Success;

            Debug.Log($"[Combat] Boss攻击闪避判定: 骰子 {roll} vs 闪避 {defenderStats.Evasion}" +
                      $" → {(dodged ? "闪避成功" : "被命中")}");

            string resultMsg = dodged
                ? "闪避成功！躲开了Boss的攻击"
                : "未能闪避，被Boss命中！";
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

        public BossAttackWoundStep(
            int woundCount,
            HunterBodyPart bodyPart,
            IRandomSource random,
            IArmorMitigationRule armorRule = null,
            IPermanentInjuryResolver permanentInjuryResolver = null)
        {
            _woundCount = woundCount;
            _bodyPart = bodyPart;
            _random = random ?? throw new System.ArgumentNullException(nameof(random));
            _armorRule = armorRule;
            _permanentInjuryResolver = permanentInjuryResolver;
        }

        public async UniTask Execute(AttackContext context, IPlayerInputProvider input)
        {
            if (context.HitResult != HitResult.Success)
                return;

            var defenderStats = context.DefenderStats;
            HunterDamageResult damage = defenderStats.ApplyDamage(
                _bodyPart,
                _woundCount,
                _random,
                _armorRule,
                _permanentInjuryResolver);

            string msg = $"{_bodyPart} 受到 {damage.IncomingDamage} 点伤害，" +
                         $"护甲抵消 {damage.ArmorPrevented}，" +
                         $"剩余生命 {damage.RemainingHealth}。";

            if (damage.IsDead)
            {
                msg += "\n<color=#ff4444>抽到死亡牌，角色永久死亡！</color>";
                EventBus.Publish(new CharacterDiedEvent
                {
                    CharacterId = context.DefenderId
                });
            }
            else
            {
                int permanentWoundsAdded = damage.PermanentInjury != null ? 1 : 0;
                if (permanentWoundsAdded > 0)
                    defenderStats.AddPermanentWounds(permanentWoundsAdded);
                if (damage.FatalInjuryTriggered)
                {
                    msg += "\n抽到存活牌，死亡牌堆加入 1 张死亡牌。";
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
        }
    }
}
