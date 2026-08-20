using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.Settlement
{
    public interface IWeaponTrainingContent
    {
        string RequiredInventionId { get; }
        string CostResourceId { get; }
        int ResourceCost { get; }
        int Experience { get; }
        bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition family);
    }

    /// <summary>把现有 ScriptableObject 目录投影为训练 Action 所需的窄内容契约。</summary>
    public sealed class PlayableWeaponTrainingContentAdapter : IWeaponTrainingContent
    {
        private readonly Combat.PlayableWeaponMasteryCatalog catalog;

        public PlayableWeaponTrainingContentAdapter(Combat.PlayableWeaponMasteryCatalog catalog)
        {
            this.catalog = catalog;
        }

        public string RequiredInventionId => catalog?.TrainingInventionName ?? string.Empty;
        public string CostResourceId => catalog?.TrainingCostItem?.ContentId ?? string.Empty;
        public int ResourceCost => catalog?.TrainingCost ?? 0;
        public int Experience => catalog?.TrainingExperience ?? 0;
        public bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition family)
        {
            family = null;
            return catalog != null && catalog.TryGetFamily(masteryId, out family);
        }
    }
}
