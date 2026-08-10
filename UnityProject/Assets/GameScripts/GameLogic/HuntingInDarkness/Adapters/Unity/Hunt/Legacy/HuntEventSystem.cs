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

        /// <summary>所有狩猎阶段事件池</summary>
        public List<EventData> HuntEventPool { get; set; } = new();

        public HuntEventSystem(EventSystem eventSystem, IRandomSource rng)
        {
            _eventSystem = eventSystem;
            _rng         = rng;
        }

        // ─── 地块翻开事件 ─────────────────────────────────────────

        /// <summary>
        /// 地块翻开时触发：先检查地块自带的 tileRevealEvent，
        /// 再从池中随机抽取一个狩猎事件（30%概率）。
        /// </summary>
        public void OnTileRevealed(HexTileInstance tile, HunterInstance selectedHunter)
        {
            // 地块规则事件（必触发）
            if (tile.Config?.tileRevealEvent != null)
            {
                Debug.Log($"[HuntEvent] 地块 {tile.AxialCoord} 规则事件：{tile.Config.tileRevealEvent.eventName}");
                _eventSystem.TriggerEvent(tile.Config.tileRevealEvent, selectedHunter);
                return;
            }

            // Boss遭遇不走事件系统（由 HuntManager 直接切Boss战）
            if (tile.HasBossEncounter) return;

            // 随机狩猎事件（30%）
            if (HuntEventRules.ShouldTrigger(0.30, _rng))
            {
                var evt = PickRandomHuntEvent();
                if (evt != null)
                {
                    Debug.Log($"[HuntEvent] 随机触发狩猎事件：{evt.eventName}");
                    _eventSystem.TriggerEvent(evt, selectedHunter);
                }
            }
        }

        /// <summary>猎人移动到已翻开地块时的事件检查</summary>
        public void OnSquadMoved(HexTileInstance tile, HunterInstance selectedHunter)
        {
            if (tile.State != TileState.Revealed) return;

            // Boss遭遇：移动到Boss地块时触发（由 HuntManager 处理）
            if (tile.HasBossEncounter)
            {
                Debug.Log($"[HuntEvent] 移动到Boss遭遇地块 {tile.AxialCoord}");
                return;
            }

            // 小概率触发随机事件（15%）
            if (HuntEventRules.ShouldTrigger(0.15, _rng))
            {
                var evt = PickRandomHuntEvent();
                if (evt != null)
                    _eventSystem.TriggerEvent(evt, selectedHunter);
            }
        }

        // ─── 内部工具 ─────────────────────────────────────────────

        private EventData PickRandomHuntEvent()
        {
            var pool = HuntEventPool.FindAll(e =>
                e != null && e.category == EventCategory.Hunt);
            if (pool.Count == 0) pool = HuntEventPool;
            if (pool.Count == 0) return null;

            return HuntEventRules.PickWeighted(pool, item => item.drawWeight, _rng);
        }
    }
}
