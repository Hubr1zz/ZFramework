using System;
using System.Collections.Generic;
using GameplayBase.Card.HitLocationCard;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
using SO.Boss.HitLocation;
using SO.Character;
using UnityEngine;

namespace GameplayBase.CombatSystem
{
    // ═══════════════════════════════════════════
    // 战斗枚举
    // ═══════════════════════════════════════════

    public enum HitResult
    {
        Success,
        Failure,
        Aborted
    }

    public enum HitLocationTriggerCondition
    {
        OnSuccess,
        OnFailure,
        Always
    }

    // ═══════════════════════════════════════════
    // 角色战斗属性
    // ═══════════════════════════════════════════

    [System.Serializable]
    public class CharacterCombatStats : CombatantStats
    {
        [NonSerialized]
        private HunterInjuryState _injuryState;

        public HunterInjuryState InjuryState =>
            _injuryState ??= new HunterInjuryState(HunterInjuryProfile.CreateDefault());
        public bool IsDead => InjuryState.IsDead;

        public void InitializeInjuryState(
            HunterInjuryProfile profile,
            DeathDeck deathDeck = null)
        {
            _injuryState = new HunterInjuryState(
                profile ?? throw new ArgumentNullException(nameof(profile)),
                deathDeck);
        }

        public HunterDamageResult ApplyDamage(
            HunterBodyPart bodyPart,
            int damage,
            IRandomSource random,
            IArmorMitigationRule armorRule = null,
            IPermanentInjuryResolver permanentInjuryResolver = null,
            DeathDeckDrawOrder deathDrawOrder = null,
            int deathCardPosition = 0)
        {
            return InjuryState.ApplyDamage(
                bodyPart,
                damage,
                random,
                armorRule,
                permanentInjuryResolver,
                deathDrawOrder,
                deathCardPosition);
        }

        public bool WillTriggerFatalInjury(HunterBodyPart bodyPart, int damage, IArmorMitigationRule armorRule = null) => InjuryState.WillTriggerFatalInjury(bodyPart, damage, armorRule);

        public CharacterCombatStats CreateRuntimeCopy()
        {
            return new CharacterCombatStats
            {
                Strength = Strength,
                Speed = Speed,
                Evasion = Evasion,
                PermanentWounds = PermanentWounds,
                TemporaryWounds = TemporaryWounds
            };
        }
    }

    // ─── 受击部位效果词条（配置在 HitLocationCardData 上）────────────────

    [System.Serializable]
    public class HitLocationEffectEntry
    {
        /// <summary>外层触发时机：OnSuccess / OnFailure / Always</summary>
        public HitLocationTriggerCondition triggerCondition;

        [SerializeReference]
        public HitLocationEffectData effectData;
    }

    // ═══════════════════════════════════════════
    // 部位卡运行时状态
    // ═══════════════════════════════════════════

    /// <summary>
    /// 追踪每张受击部位卡的运行时血量和翻面状态。
    /// 由 BossController 持有，跨攻击持久化。
    /// </summary>
    public class HitLocationRuntimeState
    {
        public HitLocationCardData Data;
        private readonly HitLocationState _domainState;

        public HitLocationState DomainState => _domainState;
        public int CurrentHp
        {
            get => _domainState.CurrentHp;
            set => _domainState.Restore(value, IsDestroyed, IsFaceUp);
        }
        public bool IsDestroyed
        {
            get => _domainState.IsDestroyed;
            set => _domainState.Restore(CurrentHp, value, IsFaceUp);
        }
        public bool IsFaceUp
        {
            get => _domainState.IsFaceUp;
            set => _domainState.Restore(CurrentHp, IsDestroyed, value);
        }

        public HitLocationRuntimeState(HitLocationCardData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            _domainState = new HitLocationState(new HitLocationDefinition(
                data.name,
                data.locationName,
                data.description,
                data.toughness,
                data.drawWeight,
                data.maxHp));
        }

        public void Reveal() => _domainState.Reveal();
        public void Hide() => _domainState.Hide();
        public bool ApplyDamage(int amount) => _domainState.ApplyDamage(amount);
    }

    // ═══════════════════════════════════════════
    // 攻击上下文
    // ═══════════════════════════════════════════

    /// <summary>
    /// 一次攻击流程的完整上下文。
    /// 攻击方和防御方的数据分开存放，不再混用。
    /// </summary>
    public class AttackContext
    {
        // ─── 参与者 ───
        public int  AttackerId;
        public int  DefenderId;
        public bool AttackerIsBoss;

        // ─── 攻击方数据（角色攻击Boss时填充） ───
        public CharacterCombatStats AttackerStats;
        public WeaponData           Weapon;

        // ─── 防御方数据（Boss攻击角色时填充） ───
        public CharacterCombatStats DefenderStats;

        // ─── 角色攻击Boss时的受击部位数据 ───
        public List<HitLocationRuntimeState> AllHitLocationStates;   // Boss全部受击部位运行时状态
        public List<HitLocationRuntimeState> RevealedHitLocations;   // 本次翻开待结算的受击部位
        public HitLocationCardData           CurrentHitLocation;

        // ─── 判定数据 ───
        public int       TotalAttackPower;
        public int       RollResult;
        public HitResult HitResult;
        public bool      IsCriticalHit;   // 攻击力超出韧性一定倍数时由步骤写入
        public int       DefenderToughness;

        // ─── 目标选择 ───
        public ITargetSelector EffectiveTargetSelector; // null = 默认受击部位流程
        public Vector2Int?     SelectedTile;            // 格子选择模式下选中的格子

        // ─── 流程控制 ───
        public bool IsAborted;

        // ─── 全局引用 ───
        public IGameContext GameContext;
        public IBoardQuery  BoardQuery;

        public void CalculateAttackPower()
        {
            WeaponProfile weapon = Weapon == null
                ? null
                : new WeaponProfile(Weapon.weaponName, Weapon.strengthBonus);
            TotalAttackPower = CombatRules.CalculateAttackPower(AttackerStats, weapon);
        }
    }

    // ═══════════════════════════════════════════
    // 攻击流程中断异常
    // ═══════════════════════════════════════════

    public class AttackAbortedException : Exception
    {
        public string        Reason  { get; }
        public AttackContext Context { get; }

        public AttackAbortedException(string reason, AttackContext context) : base(reason)
        {
            Reason  = reason;
            Context = context;
        }
    }
}
