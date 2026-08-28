using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HuntingInDarkness.GameCore.Hunt;

namespace HuntingInDarkness.GameCore.Settlement
{
    public enum HuntReturnItemKind
    {
        Resource,
        StoredItem
    }

    /// <summary>跨 Adapter 的远征归来输入快照。只包含稳定值，不持有 Unity 资产或运行时对象。</summary>
    public readonly struct HuntReturnInput
    {
        public HuntReturnInput(string recordId, int schemaVersion, int year, int huntersDeployed, int huntersLost, IReadOnlyList<int> participantHunterIds, IReadOnlyList<string> collectedResourceIds)
            : this(recordId, schemaVersion, year, huntersDeployed, huntersLost, participantHunterIds, collectedResourceIds, null)
        {
        }

        public HuntReturnInput(string recordId, int schemaVersion, int year, int huntersDeployed, int huntersLost, IReadOnlyList<int> participantHunterIds, IReadOnlyList<string> collectedResourceIds, IReadOnlyList<HuntLootStack> collectedItems)
            : this(recordId, schemaVersion, year, huntersDeployed, huntersLost, participantHunterIds, collectedResourceIds, collectedItems, 0)
        {
        }

        public HuntReturnInput(string recordId, int schemaVersion, int year, int huntersDeployed, int huntersLost, IReadOnlyList<int> participantHunterIds, IReadOnlyList<string> collectedResourceIds, IReadOnlyList<HuntLootStack> collectedItems, int rescuedPopulation)
        {
            RecordId = recordId ?? string.Empty;
            SchemaVersion = schemaVersion;
            Year = year;
            HuntersDeployed = huntersDeployed;
            HuntersLost = huntersLost;
            ParticipantHunterIds = participantHunterIds ?? Array.Empty<int>();
            CollectedResourceIds = collectedResourceIds ?? Array.Empty<string>();
            CollectedItems = collectedItems ?? Array.Empty<HuntLootStack>();
            RescuedPopulation = rescuedPopulation;
        }

        public string RecordId { get; }
        public int SchemaVersion { get; }
        public int Year { get; }
        public int HuntersDeployed { get; }
        public int HuntersLost { get; }
        public IReadOnlyList<int> ParticipantHunterIds { get; }
        public IReadOnlyList<string> CollectedResourceIds { get; }
        public IReadOnlyList<HuntLootStack> CollectedItems { get; }
        public int RescuedPopulation { get; }
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

    public readonly struct HuntReturnItemState
    {
        public HuntReturnItemState(string itemId, HuntReturnItemKind kind, int currentAmount)
        {
            ItemId = itemId ?? string.Empty;
            Kind = kind;
            CurrentAmount = currentAmount;
        }

        public string ItemId { get; }
        public HuntReturnItemKind Kind { get; }
        public int CurrentAmount { get; }
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

    public readonly struct HuntReturnItemGrant
    {
        public HuntReturnItemGrant(string itemId, HuntReturnItemKind kind, int amount, int previousAmount, int newAmount)
        {
            ItemId = itemId ?? string.Empty;
            Kind = kind;
            Amount = amount;
            PreviousAmount = previousAmount;
            NewAmount = newAmount;
        }

        public string ItemId { get; }
        public HuntReturnItemKind Kind { get; }
        public int Amount { get; }
        public int PreviousAmount { get; }
        public int NewAmount { get; }
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
        internal HuntReturnPlan(string recordId, int year, bool legacy, bool alreadyApplied, int collectedResourceCount, int collectedItemCount, int rescuedPopulation, int previousPopulation, int newPopulation, IReadOnlyList<HuntReturnItemGrant> itemGrants, IReadOnlyList<HuntReturnParticipantPlan> participantPlans)
        {
            RecordId = recordId;
            Year = year;
            IsLegacyCompatibility = legacy;
            IsAlreadyApplied = alreadyApplied;
            CollectedResourceCount = collectedResourceCount;
            CollectedItemCount = collectedItemCount;
            RescuedPopulation = rescuedPopulation;
            PreviousPopulation = previousPopulation;
            NewPopulation = newPopulation;
            ItemGrants = new ReadOnlyCollection<HuntReturnItemGrant>(new List<HuntReturnItemGrant>(itemGrants));
            var resourceGrants = new List<HuntReturnResourceGrant>();
            foreach (HuntReturnItemGrant grant in itemGrants)
                if (grant.Kind == HuntReturnItemKind.Resource)
                    resourceGrants.Add(new HuntReturnResourceGrant(grant.ItemId, grant.Amount, grant.PreviousAmount, grant.NewAmount));
            ResourceGrants = new ReadOnlyCollection<HuntReturnResourceGrant>(resourceGrants);
            ParticipantPlans = new ReadOnlyCollection<HuntReturnParticipantPlan>(new List<HuntReturnParticipantPlan>(participantPlans));
        }

        public string RecordId { get; }
        public int Year { get; }
        public bool IsLegacyCompatibility { get; }
        public bool IsAlreadyApplied { get; }
        public int CollectedResourceCount { get; }
        public int CollectedItemCount { get; }
        public int RescuedPopulation { get; }
        public int PreviousPopulation { get; }
        public int NewPopulation { get; }
        public IReadOnlyList<HuntReturnItemGrant> ItemGrants { get; }
        public IReadOnlyList<HuntReturnResourceGrant> ResourceGrants { get; }
        public IReadOnlyList<HuntReturnParticipantPlan> ParticipantPlans { get; }
    }

    /// <summary>主动回营的纯规则预检，保证 Adapter 在任何领域状态变更前完成身份、容量和参与者核验。</summary>
    public static class HuntReturnRules
    {
        public const int LegacySchemaVersion = 0;
        public const int ResourceOnlySchemaVersion = 1;
        public const int GenericLootSchemaVersion = 2;
        public const int CurrentSchemaVersion = 3;
        public const int MaximumParticipants = 4;

        public static bool TryCreatePlan(HuntReturnInput input, int currentYear, IReadOnlyList<HuntReturnParticipantState> participants, IReadOnlyList<HuntReturnResourceState> resources, bool alreadyApplied, out HuntReturnPlan plan, out string reason)
        {
            if (input.RescuedPopulation != 0)
            {
                plan = null;
                return Fail("救援人口归来计划必须提供当前营地人口。", out reason);
            }
            var items = new List<HuntReturnItemState>();
            if (resources != null)
                foreach (HuntReturnResourceState resource in resources)
                    items.Add(new HuntReturnItemState(resource.ResourceId, HuntReturnItemKind.Resource, resource.CurrentAmount));
            return TryCreateItemPlan(input, currentYear, participants, items, 0, alreadyApplied, out plan, out reason);
        }

        public static bool TryCreateItemPlan(HuntReturnInput input, int currentYear, IReadOnlyList<HuntReturnParticipantState> participants, IReadOnlyList<HuntReturnItemState> items, bool alreadyApplied, out HuntReturnPlan plan, out string reason)
        {
            if (input.RescuedPopulation == 0) return TryCreateItemPlan(input, currentYear, participants, items, 0, alreadyApplied, out plan, out reason);
            plan = null;
            return Fail("救援人口归来计划必须提供当前营地人口。", out reason);
        }

        public static bool TryCreateItemPlan(HuntReturnInput input, int currentYear, IReadOnlyList<HuntReturnParticipantState> participants, IReadOnlyList<HuntReturnItemState> items, int currentPopulation, bool alreadyApplied, out HuntReturnPlan plan, out string reason)
        {
            plan = null;
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(input.RecordId)) return Fail("远征归来记录缺少稳定 ID。", out reason);
            if (!string.Equals(input.RecordId, input.RecordId.Trim(), StringComparison.Ordinal)) return Fail("远征归来记录 ID 包含无效空白。", out reason);
            if (input.SchemaVersion < LegacySchemaVersion || input.SchemaVersion > CurrentSchemaVersion) return Fail("远征归来记录版本不受支持。", out reason);
            if (alreadyApplied)
            {
                plan = new HuntReturnPlan(input.RecordId.Trim(), input.Year, input.SchemaVersion == LegacySchemaVersion, true, CountLegacyResources(input), CountItems(input), 0, currentPopulation, currentPopulation, Array.Empty<HuntReturnItemGrant>(), Array.Empty<HuntReturnParticipantPlan>());
                return true;
            }
            if (currentYear < 1 || input.Year != currentYear) return Fail("远征归来年份与营地当前年份不一致。", out reason);
            if (input.HuntersDeployed < 0 || input.HuntersDeployed > MaximumParticipants || input.HuntersLost < 0 || input.HuntersLost > input.HuntersDeployed) return Fail("远征归来猎人数量不合法。", out reason);
            if (input.SchemaVersion == LegacySchemaVersion)
            {
                if (input.RescuedPopulation != 0) return Fail("旧版远征归来记录不得写入 v3 救援人口。", out reason);
                plan = new HuntReturnPlan(input.RecordId.Trim(), input.Year, true, false, input.CollectedResourceIds.Count, input.CollectedResourceIds.Count, 0, currentPopulation, currentPopulation, Array.Empty<HuntReturnItemGrant>(), Array.Empty<HuntReturnParticipantPlan>());
                return true;
            }

            if (input.SchemaVersion < CurrentSchemaVersion && input.RescuedPopulation != 0) return Fail("旧版远征归来记录不得写入 v3 救援人口。", out reason);
            if (input.RescuedPopulation < 0 || currentPopulation < 0 || currentPopulation > int.MaxValue - input.RescuedPopulation) return Fail("远征救援人口数量无效或溢出。", out reason);

            if (input.ParticipantHunterIds.Count != input.HuntersDeployed) return Fail("远征参与猎人数量与记录不一致。", out reason);
            if (!TryBuildParticipantPlans(input, participants, out List<HuntReturnParticipantPlan> participantPlans, out reason)) return false;
            int lostCount = 0;
            foreach (HuntReturnParticipantPlan participant in participantPlans)
                if (!participant.IsAlive) lostCount++;
            if (lostCount != input.HuntersLost) return Fail("远征死亡人数与参与猎人状态不一致。", out reason);
            if (!TryBuildItemGrants(input, items, out List<HuntReturnItemGrant> itemGrants, out int resourceCount, out int itemCount, out reason)) return false;

            plan = new HuntReturnPlan(input.RecordId.Trim(), input.Year, false, false, resourceCount, itemCount, input.RescuedPopulation, currentPopulation, currentPopulation + input.RescuedPopulation, itemGrants, participantPlans);
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

        private static bool TryBuildItemGrants(HuntReturnInput input, IReadOnlyList<HuntReturnItemState> items, out List<HuntReturnItemGrant> grants, out int resourceCount, out int itemCount, out string reason)
        {
            grants = new List<HuntReturnItemGrant>();
            resourceCount = 0;
            itemCount = 0;
            reason = string.Empty;
            var states = new Dictionary<string, HuntReturnItemState>(StringComparer.Ordinal);
            if (items != null)
                foreach (HuntReturnItemState state in items)
                {
                    string id = state.ItemId?.Trim() ?? string.Empty;
                    if (id.Length == 0 || state.CurrentAmount < 0) return Fail("远征物品状态无效。", out reason);
                    if (!states.TryAdd(id, state)) return Fail("营地物品状态存在重复 ID。", out reason);
                }

            var amounts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (input.SchemaVersion == ResourceOnlySchemaVersion)
            {
                if (input.CollectedItems.Count > 0) return Fail("v1 远征归来记录不得写入 v2 物品清单。", out reason);
                foreach (string rawId in input.CollectedResourceIds)
                    if (!TryAccumulate(rawId, 1, HuntReturnItemKind.Resource, states, amounts, ref resourceCount, ref itemCount, out reason)) return false;
            }
            else
            {
                if (input.CollectedResourceIds.Count > 0) return Fail("v2+ 远征归来记录不得继续写入旧资源清单。", out reason);
                foreach (HuntLootStack stack in input.CollectedItems)
                {
                    if (stack == null || stack.Count <= 0) return Fail("远征携带物数量无效。", out reason);
                    string id = stack.ItemId?.Trim() ?? string.Empty;
                    if (!states.TryGetValue(id, out HuntReturnItemState state)) return Fail("远征包含未知物品 ID。", out reason);
                    if (!TryAccumulate(id, stack.Count, state.Kind, states, amounts, ref resourceCount, ref itemCount, out reason)) return false;
                }
            }

            foreach (KeyValuePair<string, int> pair in amounts)
            {
                HuntReturnItemState state = states[pair.Key];
                grants.Add(new HuntReturnItemGrant(pair.Key, state.Kind, pair.Value, state.CurrentAmount, state.CurrentAmount + pair.Value));
            }
            return true;
        }

        private static bool TryAccumulate(string rawId, int added, HuntReturnItemKind requiredKind, IReadOnlyDictionary<string, HuntReturnItemState> states, IDictionary<string, int> amounts, ref int resourceCount, ref int itemCount, out string reason)
        {
            reason = string.Empty;
            string id = rawId?.Trim() ?? string.Empty;
            if (id.Length == 0 || added <= 0 || !states.TryGetValue(id, out HuntReturnItemState state)) return Fail("远征包含未知物品 ID。", out reason);
            if (state.Kind != requiredKind) return Fail("远征物品类型与归来协议不一致。", out reason);
            int current = amounts.TryGetValue(id, out int amount) ? amount : 0;
            if (added > int.MaxValue - current || state.CurrentAmount > int.MaxValue - current - added || itemCount > int.MaxValue - added) return Fail("远征物品数量溢出。", out reason);
            amounts[id] = current + added;
            itemCount += added;
            if (state.Kind == HuntReturnItemKind.Resource)
            {
                if (resourceCount > int.MaxValue - added) return Fail("远征资源数量溢出。", out reason);
                resourceCount += added;
            }
            return true;
        }

        private static int CountLegacyResources(HuntReturnInput input) => input.SchemaVersion == ResourceOnlySchemaVersion ? input.CollectedResourceIds.Count : 0;

        private static int CountItems(HuntReturnInput input)
        {
            if (input.SchemaVersion == ResourceOnlySchemaVersion) return input.CollectedResourceIds.Count;
            long count = 0;
            foreach (HuntLootStack stack in input.CollectedItems)
                count += Math.Max(0, stack?.Count ?? 0);
            return (int)Math.Min(int.MaxValue, count);
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
