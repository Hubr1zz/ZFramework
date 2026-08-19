using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunt;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>
    /// 狩猎阶段事件选择器（纯 C#）。
    /// 只负责地块翻开、移动等触发规则；事件执行由 Hunt ActionQueue 负责。
    /// </summary>
    public class HuntEventSystem
    {
        private readonly IRandomSource    _rng;
        private readonly HashSet<Vector2Int> checkedMoveTiles = new();
        private int currentYear = 1;

        /// <summary>所有狩猎阶段事件池</summary>
        public List<EventData> HuntEventPool { get; set; } = new();

        public HuntEventSystem(IRandomSource rng)
        {
            _rng         = rng;
        }

        public void ResetSession(int year = 1)
        {
            checkedMoveTiles.Clear();
            currentYear = Mathf.Max(1, year);
        }

        // ─── 地块翻开事件 ─────────────────────────────────────────

        /// <summary>先检查地块自带事件，再按 30% 概率从当前事件池选择一个事件。</summary>
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

        /// <summary>猎人首次移动到已翻开地块时的事件选择。</summary>
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
