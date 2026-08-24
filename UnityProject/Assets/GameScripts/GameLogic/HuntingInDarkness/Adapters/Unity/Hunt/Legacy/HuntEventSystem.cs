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
        private readonly IRandomSource rng;
        /// <summary>所有狩猎阶段事件池</summary>
        public List<EventData> HuntEventPool { get; set; } = new();

        public HuntEventSystem(IRandomSource rng)
        {
            this.rng = rng;
        }

        public void ResetSession(int year = 1)
        {
            _ = year;
        }

        // ─── 地块翻开事件 ─────────────────────────────────────────

        /// <summary>只返回地块显式配置事件；普通探索风险由 Hunt ActionQueue 的噪音牌堆决定。</summary>
        public EventData SelectTileRevealEvent(HexTileInstance tile)
        {
            if (tile == null || tile.HasBossEncounter) return null;
            if (tile.Config?.tileRevealEvent != null)
            {
                Debug.Log($"[HuntEvent] 地块 {tile.AxialCoord} 规则事件：{tile.Config.tileRevealEvent.eventName}");
                return tile.Config.tileRevealEvent;
            }
            return null;
        }

        /// <summary>移动不再额外抽取不可见概率事件。</summary>
        public EventData SelectSquadMoveEvent(HexTileInstance tile)
        {
            if (tile == null || tile.State != TileState.Revealed) return null;
            if (tile.HasBossEncounter)
                Debug.Log($"[HuntEvent] 移动到Boss遭遇地块 {tile.AxialCoord}");
            return null;
        }

        public EventData SelectNoiseEvent(IReadOnlyList<EventData> eligibleEvents)
        {
            if (eligibleEvents == null || eligibleEvents.Count == 0) return null;
            return HuntEventRules.PickWeighted(eligibleEvents, item => item.drawWeight, rng);
        }
    }
}
