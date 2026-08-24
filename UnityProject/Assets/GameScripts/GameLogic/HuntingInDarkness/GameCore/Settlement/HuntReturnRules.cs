using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HuntingInDarkness.GameCore.Settlement
{
    /// <summary>跨 Adapter 的远征归来输入快照。只包含稳定值，不持有 Unity 资产或运行时对象。</summary>
    public readonly struct HuntReturnInput
    {
        public HuntReturnInput(string recordId, int schemaVersion, int year, int huntersDeployed, int huntersLost, IReadOnlyList<int> participantHunterIds, IReadOnlyList<string> collectedResourceIds)
        {
            RecordId = recordId ?? string.Empty;
            SchemaVersion = schemaVersion;
            Year = year;
            HuntersDeployed = huntersDeployed;
            HuntersLost = huntersLost;
            ParticipantHunterIds = participantHunterIds ?? Array.Empty<int>();
            CollectedResourceIds = collectedResourceIds ?? Array.Empty<string>();
        }

        public string RecordId { get; }
        public int SchemaVersion { get; }
        public int Year { get; }
        public int HuntersDeployed { get; }
        public int HuntersLost { get; }
        public IReadOnlyList<int> ParticipantHunterIds { get; }
        public IReadOnlyList<string> CollectedResourceIds { get; }
    }

    public readonly struct HuntReturnParticipantState
    {
        public HuntReturnParticipantState(int hunterId, bool isAlive, HunterAvailabilityState availability, int age)
        {
            HunterId = hunterId;
            IsAlive = isAlive;
            Availability = availability;
            Age = age;
        }

        public int HunterId { get; }
        public bool IsAlive { get; }
        public HunterAvailabilityState Availability { get; }
        public int Age { get; }
    }

    public readonly struct HuntReturnResourceState
    {
        public HuntReturnResourceState(string resourceId, int currentAmount)
        {
            ResourceId = resourceId ?? string.Empty;
            CurrentAmount = currentAmount;
        }

        public string ResourceId { get; }
        public int CurrentAmount { get; }
    }

    public readonly struct HuntReturnResourceGrant
    {
        public HuntReturnResourceGrant(string resourceId, int amount, int previousAmount, int newAmount)
        {
            ResourceId = resourceId ?? string.Empty;
            Amount = amount;
            PreviousAmount = previousAmount;
            NewAmount = newAmount;
        }

        public string ResourceId { get; }
        public int Amount { get; }
        public int PreviousAmount { get; }
        public int NewAmount { get; }
    }

    public readonly struct HuntReturnParticipantPlan
    {
        public HuntReturnParticipantPlan(int hunterId, bool isAlive, bool shouldAdvance, bool shouldRetire, int previousAge)
        {
            HunterId = hunterId;
            IsAlive = isAlive;
            ShouldAdvance = shouldAdvance;
            ShouldRetire = shouldRetire;
            PreviousAge = previousAge;
        }

        public int HunterId { get; }
        public bool IsAlive { get; }
        public bool ShouldAdvance { get; }
        public bool ShouldRetire { get; }
        public int PreviousAge { get; }
    }

    /// <summary>已通过完整预检的归来提交计划。集合以只读包装暴露，避免执行阶段改变计划。</summary>
    public sealed class HuntReturnPlan
    {
        internal HuntReturnPlan(string recordId, int year, bool legacy, bool alreadyApplied, int collectedResourceCount, IReadOnlyList<HuntReturnResourceGrant> resourceGrants, IReadOnlyList<HuntReturnParticipantPlan> participantPlans)
        {
            RecordId = recordId;
            Year = year;
            IsLegacyCompatibility = legacy;
            IsAlreadyApplied = alreadyApplied;
            CollectedResourceCount = collectedResourceCount;
            ResourceGrants = new ReadOnlyCollection<HuntReturnResourceGrant>(new List<HuntReturnResourceGrant>(resourceGrants));
            ParticipantPlans = new ReadOnlyCollection<HuntReturnParticipantPlan>(new List<HuntReturnParticipantPlan>(participantPlans));
        }

        public string RecordId { get; }
        public int Year { get; }
        public bool IsLegacyCompatibility { get; }
        public bool IsAlreadyApplied { get; }
        public int CollectedResourceCount { get; }
        public IReadOnlyList<HuntReturnResourceGrant> ResourceGrants { get; }
        public IReadOnlyList<HuntReturnParticipantPlan> ParticipantPlans { get; }
    }

    /// <summary>主动回营的纯规则预检，保证 Adapter 在任何领域状态变更前完成身份、容量和参与者核验。</summary>
    public static class HuntReturnRules
    {
        public const int LegacySchemaVersion = 0;
        public const int CurrentSchemaVersion = 1;
        public const int MaximumParticipants = 4;

        public static bool TryCreatePlan(HuntReturnInput input, int currentYear, IReadOnlyList<HuntReturnParticipantState> participants, IReadOnlyList<HuntReturnResourceState> resources, bool alreadyApplied, out HuntReturnPlan plan, out string reason)
        {
            plan = null;
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(input.RecordId)) return Fail("远征归来记录缺少稳定 ID。", out reason);
            if (!string.Equals(input.RecordId, input.RecordId.Trim(), StringComparison.Ordinal)) return Fail("远征归来记录 ID 包含无效空白。", out reason);
            if (input.SchemaVersion < LegacySchemaVersion || input.SchemaVersion > CurrentSchemaVersion) return Fail("远征归来记录版本不受支持。", out reason);
            if (alreadyApplied)
            {
                plan = new HuntReturnPlan(input.RecordId.Trim(), input.Year, input.SchemaVersion == LegacySchemaVersion, true, input.CollectedResourceIds.Count, Array.Empty<HuntReturnResourceGrant>(), Array.Empty<HuntReturnParticipantPlan>());
                return true;
            }
            if (currentYear < 1 || input.Year != currentYear) return Fail("远征归来年份与营地当前年份不一致。", out reason);
            if (input.HuntersDeployed < 0 || input.HuntersDeployed > MaximumParticipants || input.HuntersLost < 0 || input.HuntersLost > input.HuntersDeployed) return Fail("远征归来猎人数量不合法。", out reason);
            if (input.SchemaVersion == LegacySchemaVersion)
            {
                plan = new HuntReturnPlan(input.RecordId.Trim(), input.Year, true, false, input.CollectedResourceIds.Count, Array.Empty<HuntReturnResourceGrant>(), Array.Empty<HuntReturnParticipantPlan>());
                return true;
            }

            if (input.ParticipantHunterIds.Count != input.HuntersDeployed) return Fail("远征参与猎人数量与记录不一致。", out reason);
            if (!TryBuildParticipantPlans(input, participants, out List<HuntReturnParticipantPlan> participantPlans, out reason)) return false;
            int lostCount = 0;
            foreach (HuntReturnParticipantPlan participant in participantPlans)
                if (!participant.IsAlive) lostCount++;
            if (lostCount != input.HuntersLost) return Fail("远征死亡人数与参与猎人状态不一致。", out reason);
            if (!TryBuildResourceGrants(input.CollectedResourceIds, resources, out List<HuntReturnResourceGrant> resourceGrants, out reason)) return false;

            plan = new HuntReturnPlan(input.RecordId.Trim(), input.Year, false, false, input.CollectedResourceIds.Count, resourceGrants, participantPlans);
            return true;
        }

        private static bool TryBuildParticipantPlans(HuntReturnInput input, IReadOnlyList<HuntReturnParticipantState> participants, out List<HuntReturnParticipantPlan> plans, out string reason)
        {
            plans = new List<HuntReturnParticipantPlan>();
            reason = string.Empty;
            var ids = new HashSet<int>();
            foreach (int hunterId in input.ParticipantHunterIds)
            {
                if (hunterId <= 0) return Fail("远征参与猎人 ID 无效。", out reason);
                if (!ids.Add(hunterId)) return Fail("远征参与猎人 ID 重复。", out reason);
            }

            var states = new Dictionary<int, HuntReturnParticipantState>();
            if (participants != null)
                foreach (HuntReturnParticipantState state in participants)
                    if (!states.TryAdd(state.HunterId, state)) return Fail("营地猎人状态存在重复 ID。", out reason);
            foreach (int hunterId in input.ParticipantHunterIds)
            {
                if (!states.TryGetValue(hunterId, out HuntReturnParticipantState state)) return Fail("远征参与猎人不存在于当前营地。", out reason);
                if (state.Age < 1) return Fail("远征参与猎人年龄无效。", out reason);
                if (state.IsAlive && state.Availability != HunterAvailabilityState.Active) return Fail("存活参与猎人当前不可用。", out reason);
                bool shouldAdvance = state.IsAlive && state.Availability == HunterAvailabilityState.Active;
                plans.Add(new HuntReturnParticipantPlan(hunterId, state.IsAlive, shouldAdvance, shouldAdvance && state.Age >= HunterAdvancementRules.MaximumAge, state.Age));
            }
            return true;
        }

        private static bool TryBuildResourceGrants(IReadOnlyList<string> collectedIds, IReadOnlyList<HuntReturnResourceState> resources, out List<HuntReturnResourceGrant> grants, out string reason)
        {
            grants = new List<HuntReturnResourceGrant>();
            reason = string.Empty;
            var states = new Dictionary<string, HuntReturnResourceState>(StringComparer.Ordinal);
            if (resources != null)
                foreach (HuntReturnResourceState state in resources)
                {
                    string id = state.ResourceId?.Trim() ?? string.Empty;
                    if (id.Length == 0 || state.CurrentAmount < 0) return Fail("远征资源状态无效。", out reason);
                    if (!states.TryAdd(id, state)) return Fail("营地资源状态存在重复 ID。", out reason);
                }

            var amounts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (collectedIds != null)
                foreach (string rawId in collectedIds)
                {
                    string id = rawId?.Trim() ?? string.Empty;
                    if (id.Length == 0 || !states.TryGetValue(id, out HuntReturnResourceState state)) return Fail("远征包含未知资源 ID。", out reason);
                    int amount = amounts.TryGetValue(id, out int current) ? current : 0;
                    if (amount == int.MaxValue) return Fail("远征资源数量溢出。", out reason);
                    amounts[id] = amount + 1;
                    if (state.CurrentAmount > int.MaxValue - amounts[id]) return Fail("远征资源数量溢出。", out reason);
                }
            foreach (KeyValuePair<string, int> pair in amounts)
            {
                HuntReturnResourceState state = states[pair.Key];
                grants.Add(new HuntReturnResourceGrant(pair.Key, pair.Value, state.CurrentAmount, state.CurrentAmount + pair.Value));
            }
            return true;
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
