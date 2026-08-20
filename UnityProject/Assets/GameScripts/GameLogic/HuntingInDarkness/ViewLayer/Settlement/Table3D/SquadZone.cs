using System.Collections.Generic;
using Cards3D;
using HuntingInDarkness.Data;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 营地桌面「出发小队」区：玩家把 HunterCard3D 拖入 4 槽的 SlotGrid 组队，
    /// 点击固定的「出发」卡 → 读取队伍 → 经 <see cref="OnDepartureRequested"/> 上报，
    /// 由 SettlementTable3D 转发给上层弹出确认窗。
    ///
    /// 区域（SlotGrid，接受 HunterProfile）与出发卡在场景预放并 Inspector 连线。
    /// </summary>
    public class SquadZone : MonoBehaviour
    {
        [SerializeField] private SlotGrid      _squadGrid;     // 4 槽，接受 HunterProfile
        [SerializeField] private DepartureCard _departureCard; // 固定出发卡

        /// <summary>点击出发卡时上报当前队伍。</summary>
        public System.Action<List<HunterInstance>> OnDepartureRequested;

        private void Awake()
        {
            if (_departureCard != null)
                _departureCard.OnDepart += RequestDeparture;
        }

        private void OnDestroy()
        {
            if (_departureCard != null)
                _departureCard.OnDepart -= RequestDeparture;
        }

        private void RequestDeparture() => OnDepartureRequested?.Invoke(GetSquad());

        /// <summary>当前在小队槽里的猎人（按槽顺序）。</summary>
        public List<HunterInstance> GetSquad()
        {
            var squad = new List<HunterInstance>();
            if (_squadGrid == null) return squad;
            foreach (var slot in _squadGrid.Slots)
            {
                if (slot.OccupantCard is HunterCard3D card && card.Hunter != null)
                    squad.Add(card.Hunter);
            }
            return squad;
        }

        /// <summary>重绑存档时移除旧猎人卡的槽位引用，卡牌实例由所属 HunterZone 释放。</summary>
        public void Clear()
        {
            if (_squadGrid == null) return;
            foreach (var slot in _squadGrid.Slots)
                slot.ClearCard();
        }
    }
}
