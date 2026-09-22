namespace HuntingInDarkness.GameCore.Settlement
{
    public static class WeaponTrainingRules
    {
        public static bool CanTrain(bool hunterAvailable, bool inventionUnlocked, int resourceAmount, int resourceCost, string masteryId, int experience, out string reason)
        {
            if (!hunterAvailable)
            {
                reason = "该猎人当前无法训练";
                return false;
            }
            if (!inventionUnlocked)
            {
                reason = "需要先掌握武器训练";
                return false;
            }
            if (string.IsNullOrWhiteSpace(masteryId) || experience <= 0 || resourceCost < 0)
            {
                reason = "训练配置无效";
                return false;
            }
            if (resourceAmount < resourceCost)
            {
                reason = "训练资源不足";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }
}
