using System;
using System.Collections.Generic;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.ActionFlow.Settlement;
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

        public SettlementInstance Data => binding.Data;

        // ─── 子系统 ──────────────────────────────────────────────

        public TimelineSystem Timeline => binding.Timeline;
        public EventSystem Events => binding.Events;
        public InventionSystem Inventions => binding.Inventions;
        public WorkshopSystem Workshop => binding.Workshop;
        public HunterManagementSystem HunterMgmt => binding.HunterMgmt;

        private readonly Func<IRandomSource> randomFactory;
        private RuntimeBinding binding;
        private PlayableSettlementContentPlan contentPlan;
        private bool preparedCandidate;
        private bool candidateConsumed;
        internal IRandomSource RandomSource => binding.Random;

        // ─── 组合根端口 ─────────────────────────────────────────

        /// <summary>由战役组合根注入的权威出猎请求端口。</summary>
        public ISettlementDepartureRequestPort DepartureRequestPort { get; set; }

        // ─── 构造 ────────────────────────────────────────────────

        public SettlementManager(int seed = 0)
        {
            randomFactory = seed != 0 ? () => new SystemRandomSource(seed) : () => new SystemRandomSource();
            binding = new RuntimeBinding(new SettlementInstance(), randomFactory());
        }

        private SettlementManager(SettlementInstance data, Func<IRandomSource> randomFactory)
        {
            this.randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
            binding = new RuntimeBinding(data ?? throw new ArgumentNullException(nameof(data)), randomFactory());
        }

        // ─── 生命周期 ────────────────────────────────────────────

        /// <summary>进入营地阶段（狩猎结算后调用）</summary>
        public IReadOnlyList<EventData> OnEnter()
        {
            var works = OnEnterWorkItems();
            var events = new List<EventData>(works.Count);
            foreach (SettlementEventWork work in works)
                if (work.Event != null) events.Add(work.Event);
            return events;
        }

        public IReadOnlyList<SettlementEventWork> OnEnterWorkItems()
        {
            Debug.Log($"[SettlementManager] 进入营地阶段 — 年份 {Data.CurrentYear}");
            return Timeline.GetEventWorkItemsForYear(Data.CurrentYear);
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
            if (DepartureRequestPort == null)
            {
                Debug.LogWarning("[SettlementManager] 出发失败：未配置权威出猎请求端口。");
                return false;
            }

            return DepartureRequestPort.RequestDeparture(hunterIds);
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
        /// 在独立候选运行图上验证外部数据，成功后以单一 Binding 引用替换当前图。
        /// 正式组合根应优先准备完整候选 Manager，再交换权威 Manager 引用。
        /// </summary>
        public bool TryInjectData(SettlementInstance data, out string reason)
        {
            if (ReferenceEquals(data, Data))
            {
                reason = "不能将当前权威营地数据作为可消费候选重新注入。";
                return false;
            }
            if (!TryPrepareCandidate(data, randomFactory, out SettlementManager candidate, out reason)) return false;
            candidate.Events.OnEventTriggered = Events.OnEventTriggered;
            candidate.Events.OnEventChainCompleted = Events.OnEventChainCompleted;
            if (!candidate.TryConsumePreparedCandidate(out reason)) return false;
            binding = candidate.binding;
            contentPlan = candidate.contentPlan;
            return true;
        }

        [Obsolete("正式读档请通过候选 SettlementManager 和 GameManager 权威引用交换。")]
        public void InjectData(SettlementInstance data)
        {
            if (!TryInjectData(data, out string reason))
                Debug.LogError($"[SettlementManager] 数据注入失败，已保留原运行图：{reason}");
        }

        /// <summary>消费尚未归属运行态的反序列化数据；无论成功或失败，调用方都不得再次使用 ownedData。</summary>
        internal static bool TryPrepareCandidate(SettlementInstance ownedData, out SettlementManager candidate, out string reason)
        {
            return TryPrepareCandidate(ownedData, () => new SystemRandomSource(), out candidate, out reason);
        }

        internal bool TryConsumePreparedCandidate(out string reason)
        {
            if (!preparedCandidate || candidateConsumed)
            {
                reason = "营地候选未准备或已经提交。";
                return false;
            }
            if (!PlayableSettlementContentRuntime.IsCurrentPlan(contentPlan))
            {
                reason = "营地候选使用的内容计划已经失效。";
                return false;
            }
            candidateConsumed = true;
            reason = string.Empty;
            return true;
        }

        private static bool TryPrepareCandidate(SettlementInstance ownedData, Func<IRandomSource> randomFactory, out SettlementManager candidate, out string reason)
        {
            candidate = null;
            if (ownedData == null)
            {
                reason = "营地候选数据为空。";
                return false;
            }
            try
            {
                ownedData.RuntimeDeparturePreparationToken = string.Empty;
                ClearDerivedHunterState(ownedData);
                var prepared = new SettlementManager(ownedData, randomFactory) { preparedCandidate = true };
                if (!PlayableSettlementContentRuntime.TryApplyTo(prepared, out reason)) return false;
                prepared.contentPlan = PlayableSettlementContentRuntime.CurrentPlan;
                if (!PlayableSettlementContentRuntime.IsCurrentPlan(prepared.contentPlan))
                {
                    reason = "营地候选未绑定有效的内容计划。";
                    return false;
                }
                candidate = prepared;
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = $"营地候选投影异常：{exception.Message}";
                return false;
            }
        }

        private static void ClearDerivedHunterState(SettlementInstance data)
        {
            foreach (HunterInstance hunter in data.Hunters ?? new List<HunterInstance>())
            {
                if (hunter == null) continue;
                hunter.Equipment ??= new List<ItemInstance>();
                hunter.Collectibles ??= new List<ItemInstance>();
                hunter.Equipment.Clear();
                hunter.Collectibles.Clear();
            }
        }

        private sealed class RuntimeBinding
        {
            public RuntimeBinding(SettlementInstance data, IRandomSource random)
            {
                Data = data;
                Random = random ?? throw new ArgumentNullException(nameof(random));
                Timeline = new TimelineSystem(data, Random);
                HunterMgmt = new HunterManagementSystem(data, Random);
                Events = new EventSystem(data, Random, Timeline, HunterMgmt);
                Inventions = new InventionSystem(data);
                Workshop = new WorkshopSystem(data, Inventions);
            }

            public SettlementInstance Data { get; }
            public IRandomSource Random { get; }
            public TimelineSystem Timeline { get; }
            public EventSystem Events { get; }
            public InventionSystem Inventions { get; }
            public WorkshopSystem Workshop { get; }
            public HunterManagementSystem HunterMgmt { get; }
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
