using System.Collections.Generic;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    /// <summary>
    /// 营地阶段管理器（纯 C#）。
    /// 协调所有营地子系统（Timeline, Event, Invention, Workshop, HunterMgmt）。
    /// GameManager 在切入 Settlement 阶段时调用 OnEnter()。
    /// </summary>
    public class SettlementManager
    {
        // ─── 数据 ───────────────────────────────────────────────

        public SettlementInstance Data { get; private set; }

        // ─── 子系统 ──────────────────────────────────────────────

        public TimelineSystem        Timeline     { get; private set; }
        public EventSystem           Events       { get; private set; }
        public InventionSystem       Inventions   { get; private set; }
        public WorkshopSystem        Workshop     { get; private set; }
        public HunterManagementSystem HunterMgmt  { get; private set; }

        private readonly IRandomSource _rng;

        // ─── UI 回调（由 SettlementUIManager 注入）────────────────

        /// <summary>当进入狩猎阶段时触发（GameManager 监听，切换 Root）</summary>
        public System.Action<List<int>> OnDepartForHunt;

        // ─── 构造 ────────────────────────────────────────────────

        public SettlementManager(int seed = 0)
        {
            _rng  = seed != 0 ? new SystemRandomSource(seed) : new SystemRandomSource();
            Data  = new SettlementInstance();

            Timeline   = new TimelineSystem(Data, _rng);
            Events     = new EventSystem(Data, _rng, Timeline);
            Inventions = new InventionSystem(Data);
            Workshop   = new WorkshopSystem(Data, Inventions);
            HunterMgmt = new HunterManagementSystem(Data, _rng);

        }

        // ─── 生命周期 ────────────────────────────────────────────

        /// <summary>进入营地阶段（狩猎结算后调用）</summary>
        public IReadOnlyList<EventData> OnEnter(HuntRecord huntRecord = null)
        {
            Debug.Log($"[SettlementManager] 进入营地阶段 — 年份 {Data.CurrentYear}");

            List<EventData> yearEvents;
            if (huntRecord != null)
            {
                // 收集物已由 HuntManager 转入资源存储
                yearEvents = Timeline.AdvanceYear(huntRecord);
            }
            else
            {
                yearEvents = Timeline.GetEventsForYear(Data.CurrentYear);
            }

            return yearEvents;
        }

        /// <summary>离开营地阶段</summary>
        public void OnExit()
        {
            Debug.Log("[SettlementManager] 离开营地阶段");
        }

        // ─── 出发准备 ────────────────────────────────────────────

        /// <summary>
        /// 选定猎人列表，开始狩猎。
        /// 最多4名猎人，必须选至少1名。
        /// </summary>
        public bool TryDepart(List<int> hunterIds)
        {
            if (!DepartureRules.CanDepart(hunterIds, out string reason))
            {
                Debug.LogWarning($"[SettlementManager] 出发失败：{reason}");
                return false;
            }

            Data.DepartingHunterIds = new List<int>(hunterIds);
            Debug.Log($"[SettlementManager] 出发狩猎，猎人：{string.Join(",", hunterIds)}");
            EventBus.Publish(new HuntDepartedEvent { HunterIds = hunterIds.ToArray() });
            OnDepartForHunt?.Invoke(hunterIds);
            return true;
        }

        // ─── 初始化辅助 ──────────────────────────────────────────

        /// <summary>
        /// 开局初始化：添加3名默认猎人、基础资源。
        /// 如果已有猎人则跳过。
        /// </summary>
        public void EnsureStartingConditions()
        {
            if (PlayableSettlementContentRuntime.TryApplyTo(this)) return;
            if (Data.Hunters.Count > 0) return;

            HunterMgmt.AddStartingHunter("战士·陈");
            HunterMgmt.AddStartingHunter("斥候·林");
            HunterMgmt.AddStartingHunter("萨满·余");

            Data.AddResource("Bone",  3);
            Data.AddResource("Stone", 2);
            Data.AddResource("Hide",  2);

            Debug.Log("[SettlementManager] 初始条件已创建（3猎人，基础资源）");
        }

        // ─── 数据注入 ────────────────────────────────────────────

        /// <summary>
        /// 用外部数据替换当前 Data（读档专用）。
        /// 所有子系统同步重新绑定到新数据实例，保留原有事件回调。
        /// </summary>
        public void InjectData(SettlementInstance data)
        {
            if (data == null) return;
            Data       = data;
            Timeline   = new TimelineSystem(Data, _rng);
            Events     = new EventSystem(Data, _rng, Timeline);
            Inventions = new InventionSystem(Data);
            Workshop   = new WorkshopSystem(Data, Inventions);
            HunterMgmt = new HunterManagementSystem(Data, _rng);
            PlayableSettlementContentRuntime.TryApplyTo(this);
        }

        // ─── 开发者工具 ──────────────────────────────────────────

        /// <summary>快速添加资源（开发者模式）</summary>
        public void DevAddResource(string name, int amount)
        {
            Data.AddResource(name, amount);
            EventBus.Publish(new ResourceChangedEvent
            {
                ResourceName = name,
                OldAmount = Data.GetResource(name) - amount,
                NewAmount = Data.GetResource(name)
            });
        }

        /// <summary>快速招募猎人（开发者模式）</summary>
        public HunterInstance DevAddHunter(string name)
        {
            return HunterMgmt.Recruit(null, name);
        }
    }
}
