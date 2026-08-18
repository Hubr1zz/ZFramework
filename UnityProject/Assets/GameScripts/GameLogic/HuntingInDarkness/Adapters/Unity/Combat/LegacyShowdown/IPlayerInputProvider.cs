using System.Collections.Generic;
using System.Threading;
using CardTactics.CombatSystem;
using Cysharp.Threading.Tasks;
using GameplayBase.CombatSystem;
using SO.Character;
using UnityEngine;

namespace GameplayBase.CombatSystem
{
    /// <summary>
    /// 玩家输入提供者接口。
    /// 战斗流程中所有"等待玩家操作"的点都通过此接口，
    /// 实现方负责显示UI、等待点击、返回结果。
    ///
    /// 解耦好处：
    /// - CombatManager 完全不知道 UI 怎么实现
    /// - 测试时可以注入一个 AutoInputProvider 自动走流程
    /// - 未来换 UI 框架不影响战斗逻辑
    /// </summary>
    public interface IPlayerInputProvider
    {
        /// <summary>
        /// 请求玩家执行一次掷骰/判定。
        /// 显示按钮，玩家点击后返回随机结果。
        /// </summary>
        /// <param name="prompt">提示文本（如"点击掷骰判定闪避"）</param>
        /// <param name="maxExclusive">随机范围上限（结果为 0 ~ maxExclusive-1）</param>
        /// <returns>骰子结果</returns>
        UniTask<int> RequestRoll(string prompt, int maxExclusive, CancellationToken cancellationToken = default);

        /// <summary>展示攻击结果（命中/未命中/中断），等待玩家确认后继续。</summary>
        UniTask ShowResult(string message, CancellationToken cancellationToken = default);
        UniTask<int> RequestSelectTarget(string prompt, List<int> validTargetIds, CancellationToken cancellationToken = default);
        /// <summary>
        /// 请求玩家选择一个棋盘格子。鼠标右键取消返回 null。
        /// </summary>
        UniTask<Vector2Int?> RequestSelectTile(string prompt, List<Vector2Int> validTiles, CancellationToken cancellationToken = default);
        UniTask<int> RequestSelectCard(string prompt, List<int> validCardIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// 播放Boss部位卡洗牌 + 翻牌动画。
        /// 完成后 toReveal 中每张卡的 IsFaceUp 已置为 true，BossCardTable 会响应对应事件。
        /// </summary>
        UniTask PlayShuffleAndReveal(
            List<HitLocationRuntimeState> allCards,
            List<HitLocationRuntimeState> toReveal,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 力量尝试阶段：等待玩家从已翻开的部位卡中点击选择一张。
        /// </summary>
        UniTask<HitLocationRuntimeState> RequestSelectRevealedCard(
            string prompt, List<HitLocationRuntimeState> revealedCards, CancellationToken cancellationToken = default);

        /// <summary>
        /// 请求玩家从装备栏选择一件武器。
        /// 由 PlayerSelectWeaponResolver 调用；只有一件候选武器时 resolver 直接返回，不调用此方法。
        /// </summary>
        UniTask<WeaponData> RequestSelectWeapon(string prompt, List<WeaponData> candidates, CancellationToken cancellationToken = default);
    }
}
