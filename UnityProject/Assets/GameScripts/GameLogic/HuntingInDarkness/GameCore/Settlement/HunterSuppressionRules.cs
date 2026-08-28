namespace HuntingInDarkness.GameCore.Settlement
{
    public enum HunterSuppressionState
    {
        Mad,
        Normal,
        Passive
    }

    public static class HunterSuppressionRules
    {
        public const int Minimum = 0;
        public const int Default = 4;
        public const int Maximum = 8;
        public const int MadMaximum = 2;
        public const int PassiveMinimum = 6;

        public static int Clamp(int value)
        {
            if (value < Minimum)
                return Minimum;
            if (value > Maximum)
                return Maximum;
            return value;
        }

        public static int Increase(int current, int positiveDelta)
        {
            int normalized = Clamp(current);
            if (positiveDelta <= 0)
                return normalized;
            if (positiveDelta >= Maximum - normalized)
                return Maximum;
            return normalized + positiveDelta;
        }

        public static HunterSuppressionState Classify(int value)
        {
            int normalized = Clamp(value);
            if (normalized <= MadMaximum)
                return HunterSuppressionState.Mad;
            if (normalized >= PassiveMinimum)
                return HunterSuppressionState.Passive;
            return HunterSuppressionState.Normal;
        }

        public static bool TryParseStateKey(string value, out HunterSuppressionState state)
        {
            string normalized = value?.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "mad":
                    state = HunterSuppressionState.Mad;
                    return true;
                case "normal":
                    state = HunterSuppressionState.Normal;
                    return true;
                case "passive":
                    state = HunterSuppressionState.Passive;
                    return true;
                default:
                    state = default;
                    return false;
            }
        }

        public static string GetStateKey(HunterSuppressionState state) => state switch
        {
            HunterSuppressionState.Mad => "mad",
            HunterSuppressionState.Normal => "normal",
            HunterSuppressionState.Passive => "passive",
            _ => string.Empty
        };

        public static bool TryNormalizeStateKey(string value, out string normalizedKey)
        {
            if (!TryParseStateKey(value, out HunterSuppressionState state))
            {
                normalizedKey = string.Empty;
                return false;
            }
            normalizedKey = GetStateKey(state);
            return true;
        }

        public static string GetDisplayName(HunterSuppressionState state) => state switch
        {
            HunterSuppressionState.Mad => "疯狂",
            HunterSuppressionState.Normal => "正常",
            HunterSuppressionState.Passive => "消极",
            _ => "未知"
        };

        public static string GetDisplayName(int value) => GetDisplayName(Classify(value));
    }
}
