using System;
using System.Collections.Generic;
using CardGame.ActionQueue;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.ActionFlow.Inventions
{
    /// <summary>把已掌握发明的跨阶段规则投影为对应 Runner 的 Reactor。</summary>
    public sealed class InventionActionEffectInstaller : IActionEnvironmentInstaller
    {
        private readonly Func<SettlementInstance> getSettlement;
        private readonly Func<IReadOnlyList<InventionData>> getInventions;

        public InventionActionEffectInstaller(Func<SettlementInstance> getSettlement, Func<IReadOnlyList<InventionData>> getInventions)
        {
            this.getSettlement = getSettlement ?? throw new ArgumentNullException(nameof(getSettlement));
            this.getInventions = getInventions ?? throw new ArgumentNullException(nameof(getInventions));
        }

        public bool Supports(ActionEnvironmentKind kind) => kind == ActionEnvironmentKind.Hunt;

        public void Install(IActionEnvironment environment, ActionEnvironmentInstallation installation)
        {
            if (environment == null) throw new ArgumentNullException(nameof(environment));
            if (installation == null) throw new ArgumentNullException(nameof(installation));
            installation.Add(environment.Reactors.RegisterGlobal(new HarvestActionEffectReactor(getSettlement, getInventions)));
        }

        private sealed class HarvestActionEffectReactor : GameActionReactor<BeginHarvestAction>
        {
            private readonly Func<SettlementInstance> getSettlement;
            private readonly Func<IReadOnlyList<InventionData>> getInventions;

            public HarvestActionEffectReactor(Func<SettlementInstance> getSettlement, Func<IReadOnlyList<InventionData>> getInventions)
            {
                this.getSettlement = getSettlement;
                this.getInventions = getInventions;
            }

            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(BeginHarvestAction action, ReactionContext context, ReactionResponse response)
            {
                SettlementInstance settlement = getSettlement.Invoke();
                IReadOnlyList<InventionData> inventions = getInventions.Invoke();
                if (settlement == null || inventions == null || action.Materials == null) return;

                foreach (ItemData material in action.Materials)
                {
                    if (material == null) continue;
                    float bonus = 0f;
                    var appliedEffectIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (InventionData invention in inventions)
                    {
                        if (invention == null || !settlement.IsInventionUnlocked(invention.ContentId) || invention.actionEffects == null) continue;
                        foreach (InventionActionEffect effect in invention.actionEffects)
                        {
                            string effectId = effect?.effectId?.Trim() ?? string.Empty;
                            if (effect == null || effectId.Length == 0 || float.IsNaN(effect.value) || float.IsInfinity(effect.value) || effect.kind != InventionActionEffectKind.ModifyHarvestHitChance || !appliedEffectIds.Add(effectId)) continue;
                            if (ContainsKeyword(material, effect.targetKeyword)) bonus += effect.value;
                        }
                    }
                    action.AddMaterialHitChance(material, bonus);
                }
            }

            private static bool ContainsKeyword(ItemData item, string expected)
            {
                if (item == null) return false;
                if (KeywordRules.Contains(item.keywords, expected)) return true;
                if (item.tags == null) return false;
                string normalizedExpected = KeywordRules.Normalize(expected);
                foreach (ItemTag tag in item.tags)
                    if (KeywordRules.Normalize(tag.ToString()) == normalizedExpected)
                        return true;
                return false;
            }
        }
    }
}
