using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunt;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>
    /// 狩猎阶段事件系统（纯 C#）。
    /// 复用营地 EventSystem 的核心逻辑，
    /// 但添加狩猎专属的触发规则（地块翻开、移动到特定位置等）。
    /// </summary>
    public class HuntEventSystem
    {
        private readonly EventSystem      _eventSystem;   // 复用营地事件系统
        private readonly IRandomSource    _rng;
        private readonly HashSet<Vector2Int> checkedMoveTiles = new();
        private int currentYear = 1;

        /// <summary>所有狩猎阶段事件池</summary>
        public List<EventData> HuntEventPool { get; set; } = new();

        public HuntEventSystem(EventSystem eventSystem, IRandomSource rng)
        {
            _eventSystem = eventSystem;
            _rng         = rng;
        }

        public void ResetSession(int year = 1)
        {
            checkedMoveTiles.Clear();
            currentYear = Mathf.Max(1, year);
        }

        // ─── 地块翻开事件 ─────────────────────────────────────────

        /// <summary>
        /// 地块翻开时触发：先检查地块自带的 tileRevealEvent，
        /// 再从池中随机抽取一个狩猎事件（30%概率）。
        /// </summary>
        public void OnTileRevealed(HexTileInstance tile, HunterInstance selectedHunter)
        {
            EventData gameEvent = SelectTileRevealEvent(tile);
            if (gameEvent != null)
                _eventSystem.TriggerEvent(gameEvent, selectedHunter);
        }

        public EventData SelectTileRevealEvent(HexTileInstance tile)
        {
            if (tile == null || tile.HasBossEncounter) return null;
            if (tile.Config?.tileRevealEvent != null)
            {
                Debug.Log($"[HuntEvent] 地块 {tile.AxialCoord} 规则事件：{tile.Config.tileRevealEvent.eventName}");
                return tile.Config.tileRevealEvent;
            }
            if (!HuntEventRules.ShouldTrigger(0.30, _rng)) return null;
            EventData gameEvent = PickRandomHuntEvent();
            if (gameEvent != null)
                Debug.Log($"[HuntEvent] 随机触发狩猎事件：{gameEvent.eventName}");
            return gameEvent;
        }

        /// <summary>猎人移动到已翻开地块时的事件检查</summary>
        public void OnSquadMoved(HexTileInstance tile, HunterInstance selectedHunter)
        {
            EventData gameEvent = SelectSquadMoveEvent(tile);
            if (gameEvent != null)
                _eventSystem.TriggerEvent(gameEvent, selectedHunter);
        }

        public EventData SelectSquadMoveEvent(HexTileInstance tile)
        {
            if (tile == null || tile.State != TileState.Revealed) return null;
            if (tile.HasBossEncounter)
            {
                Debug.Log($"[HuntEvent] 移动到Boss遭遇地块 {tile.AxialCoord}");
                return null;
            }
            if (!checkedMoveTiles.Add(tile.AxialCoord) || !HuntEventRules.ShouldTrigger(0.15, _rng)) return null;
            return PickRandomHuntEvent();
        }

        // ─── 内部工具 ─────────────────────────────────────────────

        private EventData PickRandomHuntEvent()
        {
            var pool = HuntEventPool.FindAll(e =>
                IsAvailable(e) && e.category == EventCategory.Hunt);
            if (pool.Count == 0) pool = HuntEventPool.FindAll(IsAvailable);
            if (pool.Count == 0) return null;

            return HuntEventRules.PickWeighted(pool, item => item.drawWeight, _rng);
        }

        private bool IsAvailable(EventData gameEvent) => gameEvent != null && gameEvent.minYear <= currentYear && (gameEvent.maxYear <= 0 || currentYear <= gameEvent.maxYear);
    }
}
