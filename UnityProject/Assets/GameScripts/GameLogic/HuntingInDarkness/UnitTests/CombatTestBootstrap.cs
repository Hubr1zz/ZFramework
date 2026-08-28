using System.Collections.Generic;
using Config;
using Core;
using GameplayBase;
using GameplayBase.Board;
using GameplayBase.CombatSystem;
using GameplayBase.Config;
using HuntingInDarkness.Testing;
using Sirenix.OdinInspector;
using SO.Combat;
using TMPro;
using UnityEngine;

namespace UnitTests
{
    /// <summary>
    /// 战斗系统独立测试引导器（仅用于 Combat 测试场景）。
    ///
    /// 设计目标：在不改动正式流程的前提下，让 Combat 场景一启动就直接进入一场 Boss 决战。
    /// 做法：用本组件 Inspector 上的「场地规则 + 猎人小队 + Boss」组装一个 <see cref="BattleSetup"/>，
    /// 通过 GameManager 公共测试配置 API 注入，并强制其以 BossFight 阶段启动。
    ///
    /// 用法：
    ///   1. 在 Combat 场景放一个空 GameObject，挂上本脚本；
    ///   2. Field Rules 拖入一个 CombatFieldRulesSO（地图尺寸、猎人/Boss 出生槽、组件池）；
    ///   3. Hunter Squad 拖入猎人配置（CharacterConfigSO，可多个，顺序对应出生槽）；
    ///   4. Boss 拖入 BossConfigSO；
    ///   5. 运行场景即直接开打。
    ///
    /// 注意：场景中不要再额外放一个手动配置的 GameManager，避免单例冲突。
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatTestBootstrap : StandaloneGameTestEntry
    {
        [Header("场地规则（CombatFieldRulesSO）")][InlineEditor]
        [SerializeField] private CombatFieldRulesSO fieldRules;

        [Header("猎人小队（CharacterConfigSO，顺序对应出生槽）")]
        [SerializeField] private List<CharacterConfigSO> hunterSquad = new();

        [Header("Boss 配置（BossConfigSO）")]
        [SerializeField] private BossConfigSO boss;

        [Header("棋盘格距（cellSize）")]
        [Tooltip("生成棋盘的格距，决定棋盘物理大小。半径在场地规则的 mapRadius 里调。")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("可选：场景里的 HexGridGizmos 预览网格；填了会在编辑期自动同步半径+格距，免去手动对齐")]
        [SerializeField] private HexGridGizmos previewGizmos;

        [Header("可选：实体工厂（要用角色 Prefab 时配）")]
        [Tooltip("把场景里挂了 EntityCreator(并指定了 Character Entity Prefab)的物体拖进来；留空则角色走程序化胶囊回退")]
        [SerializeField] private UI.EntityCreator entityCreator;

        [Header("可选：中文字体（缺省则中文图集不预热，不影响战斗逻辑）")]
        [SerializeField] private TMP_FontAsset chineseFontAsset;
        [SerializeField] private TextAsset chineseCharacterSet;

        [Header("调试")]
        [Tooltip("勾选后把战斗相关 EventBus 事件打到 Console，便于观察攻击流程")]
        [SerializeField] private bool logCombatEvents = true;

        private void Awake()
        {
            if (GameManager.Instance != null)
            {
                Debug.LogWarning("[CombatTestBootstrap] 场景中已存在 GameManager，跳过引导。");
                return;
            }

            if (fieldRules == null)
                Debug.LogWarning("[CombatTestBootstrap] 未配置场地规则（fieldRules），将使用默认半径并自动放置实体。");
            if (hunterSquad == null || hunterSquad.Count == 0)
                Debug.LogWarning("[CombatTestBootstrap] 未配置任何猎人（hunterSquad 为空）。");
            if (boss == null)
                Debug.LogWarning("[CombatTestBootstrap] 未配置 Boss（boss 为空）。");

            var setup = new BattleSetup
            {
                FieldRules  = fieldRules,
                HunterSquad = hunterSquad,
                Boss        = boss
            };

            // 1) 先建一个 inactive 的 GameObject。inactive 状态下 AddComponent
            //    不会立即触发 Awake，于是可以先注入载荷与 dev 起始阶段再激活。
            var gmGo = new GameObject("GameManager (CombatTest)");
            gmGo.SetActive(false);
            var gm = gmGo.AddComponent<GameManager>();

            // 2) 通过统一组合根配置注入装配与显式测试起始阶段。
            gm.ConfigureCampaign(new CampaignBootstrapRequest
            {
                BattleSetup = setup,
                CellSize = cellSize,
                EntityCreator = entityCreator,
                ChineseFontAsset = chineseFontAsset,
                ChineseCharacterSet = chineseCharacterSet,
                DevelopmentStartPhase = GamePhase.BossFight
            });

            // 4) 可选战斗事件日志。
            if (logCombatEvents)
            {
                EventBus.Subscribe<AttackCompletedEvent>(OnAttackCompleted);
                EventBus.Subscribe<CharacterWoundedEvent>(OnCharacterWounded);
                EventBus.Subscribe<HitLocationDestroyedEvent>(OnHitLocationDestroyed);
            }

            // 5) 激活 → 触发 GameManager.Awake / Start，直接进入 BossFight。
            gmGo.SetActive(true);
            Debug.Log("[CombatTestBootstrap] GameManager 已以 BossFight 阶段启动，战斗开始。");
        }

        /// <summary>编辑期把预览网格同步成实际会生成的棋盘（半径来自场地规则，格距来自本组件）。</summary>
        private void OnValidate()
        {
            if (cellSize <= 0f) cellSize = 0.01f;
            if (previewGizmos == null) return;
            int radius = fieldRules != null ? fieldRules.mapRadius : 3;
            previewGizmos.ApplyPreview(radius, cellSize);
        }

        private void OnDestroy()
        {
            if (!logCombatEvents) return;
            EventBus.Unsubscribe<AttackCompletedEvent>(OnAttackCompleted);
            EventBus.Unsubscribe<CharacterWoundedEvent>(OnCharacterWounded);
            EventBus.Unsubscribe<HitLocationDestroyedEvent>(OnHitLocationDestroyed);
        }

        // ─── 事件日志 ─────────────────────────────────────────────────

        private void OnAttackCompleted(AttackCompletedEvent e) =>
            Debug.Log($"[CombatTest] 攻击完成 attacker=#{e.AttackerId} defender=#{e.DefenderId} " +
                      $"bossAttacker={e.AttackerIsBoss} completed={e.Completed} " +
                      $"abort={(string.IsNullOrEmpty(e.AbortReason) ? "-" : e.AbortReason)}");

        private void OnCharacterWounded(CharacterWoundedEvent e) =>
            Debug.Log($"[CombatTest] 角色#{e.CharacterId} {e.BodyPart} 受伤 " +
                      $"伤害{e.IncomingDamage} 护甲抵消{e.ArmorPrevented} " +
                      $"生命损失{e.HealthLost} 剩余{e.RemainingHealth} " +
                      $"致命伤={e.FatalInjuryTriggered} 永久损伤+{e.PermanentWoundsAdded}");

        private void OnHitLocationDestroyed(HitLocationDestroyedEvent e) =>
            Debug.Log($"[CombatTest] Boss 部位被摧毁：{e.PartName}");
    }
}
