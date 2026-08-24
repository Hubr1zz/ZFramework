using System;
using System.Collections.Generic;
using GameplayBase;
using SO.Boss.ActionCard;
using TEngine;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 迁移兼容门面。实际派发由 TEngine GameEvent 负责；旧调用点无需一次性重写。
    /// 用法：
    ///   EventBus.Subscribe&lt;CardFlippedEvent&gt;(OnCardFlipped);
    ///   EventBus.Publish(new CardFlippedEvent(card, oldFace, newFace));
    /// </summary>
    public static class EventBus
    {
        private static readonly List<Subscription> subscriptions = new();

        private sealed class Subscription
        {
            public Delegate Handler { get; }
            public Action Cleanup { get; }

            public Subscription(Delegate handler, Action cleanup)
            {
                Handler = handler;
                Cleanup = cleanup;
            }
        }

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            if (GameEvent.AddEventListener(EventRoute<T>.Name, handler))
                subscriptions.Add(new Subscription(handler, () => GameEvent.RemoveEventListener(EventRoute<T>.Name, handler)));
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;

            int index = subscriptions.FindLastIndex(subscription => subscription.Handler.Equals(handler));
            if (index < 0) return;

            var cleanup = subscriptions[index].Cleanup;
            subscriptions.RemoveAt(index);
            cleanup();
        }

        public static void Publish<T>(T evt) where T : struct
        {
            GameEvent.Send(EventRoute<T>.Name, evt);
        }

        /// <summary>测试/场景切换时清理</summary>
        public static void Clear()
        {
            var cleanupActions = subscriptions.ConvertAll(subscription => subscription.Cleanup);
            subscriptions.Clear();
            for (int i = cleanupActions.Count - 1; i >= 0; i--)
                cleanupActions[i]();
        }

        private static class EventRoute<T> where T : struct
        {
            internal static readonly string Name = "HuntingInDarkness.Event." + typeof(T).FullName;
        }
    }

    // ─────────────────────────────────────────────
    // 事件定义：全部用 struct，零GC
    // ─────────────────────────────────────────────

    /// <summary>回合阶段切换</summary>
    public struct TurnPhaseChangedEvent
    {
        public TurnPhase PreviousPhase;
        public TurnPhase NewPhase;
        public int TurnNumber;
    }

    /// <summary>角色被选中准备行动</summary>
    public struct CharacterSelectedEvent
    {
        public int CharacterId;
    }

    /// <summary>卡牌被打出</summary>
    public struct CardPlayedEvent
    {
        public int CardInstanceId;
        public int OwnerCharacterId;
        public int TimePointCost;
    }

    /// <summary>卡牌翻面（正→背）</summary>
    public struct CardFlippedEvent
    {
        public int CardInstanceId;
        public int OwnerCharacterId;
        public CardFace OldFace;
        public CardFace NewFace;
    }

    /// <summary>卡牌恢复（背→正）</summary>
    public struct CardRestoredEvent
    {
        public int CardInstanceId;
        public int OwnerCharacterId;
    }

    /// <summary>卡牌被弃置换资源（右键正面卡→获得资源→翻面）</summary>
    public struct CardDiscardedEvent
    {
        public int CardInstanceId;
        public int OwnerCharacterId;
        public int CurrencyReward;
        public int TimePointReward;
    }

    /// <summary>时点变化</summary>
    public struct TimePointChangedEvent
    {
        public int EntityId;     // 角色或Boss的ID
        public bool IsBoss;
        public int OldValue;
        public int NewValue;
    }

    /// <summary>角色耗尽（时点超限）</summary>
    public struct CharacterExhaustedEvent
    {
        public int CharacterId;
    }

    /// <summary>取消角色选中（点击空白区域时触发）</summary>
    public struct CharacterDeselectedEvent { }

    /// <summary>游戏大阶段切换</summary>
    public struct GamePhaseChangedEvent
    {
        public GamePhase PreviousPhase;
        public GamePhase NewPhase;
    }

    // ─── 营地/狩猎 事件 ───

    /// <summary>年份推进</summary>
    public struct YearAdvancedEvent
    {
        public int NewYear;
    }

    /// <summary>事件（叙事/抉择/战斗）触发</summary>
    public struct GameEventTriggeredEvent
    {
        public string EventId; // SO名称或GUID
    }

    /// <summary>猎人列表变化（招募、死亡）</summary>
    public struct HunterRosterChangedEvent { }

    /// <summary>资源存储变化</summary>
    public struct ResourceChangedEvent
    {
        public string ResourceName;
        public int OldAmount;
        public int NewAmount;
    }

    /// <summary>猎人小队出发（营地→狩猎）</summary>
    public struct HuntDepartedEvent
    {
        public int[] HunterIds;
    }

    /// <summary>狩猎记录已经写入营地年鉴，供只读表现刷新。</summary>
    public struct HuntCompletedEvent
    {
        public int CompletedYear;
        public int TotalHunts;
        public int HuntersDeployed;
        public int HuntersLost;
        public int CollectedResourceCount;
        public bool BossDefeated;
        public int AdvancedToYear;
    }

    // ─── Boss决战 ───

    /// <summary>Boss被击败（全部部位卡摧毁，或玩家手动触发）</summary>
    public struct BossDefeatedEvent { }

    /// <summary>游戏结束（全部猎人死亡）</summary>
    public struct GameOverEvent
    {
        public string Reason;
    }

    /// <summary>Boss开始抽卡展示</summary>
    public struct BossDrawEvent
    {
        public BossActionCardData[] DrawnCards;
    }

    /// <summary>Boss执行行动卡</summary>
    public struct BossActionExecutedEvent
    {
        public int ActionCardId;
    }

    /// <summary>回合结束（用于触发回合结束类条件）</summary>
    public struct TurnEndEvent
    {
        public TurnPhase EndingPhase;
        public int TurnNumber;
    }

    // ─── 棋盘 / 相机 ───

    public struct EntityMovedEvent
    {
        public int EntityId;
        public Vector2Int FromTile;
        public Vector2Int ToTile;
    }

    /// <summary>角色注视变更（供 CameraController 按角色/Boss 位置计算镜头位姿）</summary>
    public struct BoardFocusChangedEvent
    {
        public bool HasFocus;
        public Vector3 CharacterWorldPosition;
        public Vector3 BossWorldPosition;
    }

    /// <summary>角色面板近景变更；进入时使用 ViewPoint 的世界位姿。</summary>
    public struct CharacterDetailFocusChangedEvent
    {
        public bool HasFocus;
        public Vector3 CameraWorldPosition;
        public Quaternion CameraWorldRotation;
    }

    /// <summary>棋盘生成完毕（供 CameraController 计算 WASD 平移的动态死区范围）</summary>
    public struct BoardReadyEvent
    {
        public int   MapRadius;
        public float CellSize;
    }
}
