using HuntingInDarkness.GameCore.Hunters;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    public static class PlayablePermanentInjuryRuntime
    {
        public static PlayablePermanentInjuryCatalog Catalog { get; private set; }
        public static IPermanentInjuryResolver Resolver => Catalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState() => Catalog = null;

        public static void Configure(PlayablePermanentInjuryCatalog catalog) => Catalog = catalog;
    }
}
