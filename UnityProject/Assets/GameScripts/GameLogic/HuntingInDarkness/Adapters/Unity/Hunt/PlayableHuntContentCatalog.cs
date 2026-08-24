using System.Collections.Generic;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>
    /// 狩猎阶段的 Unity 内容目录，负责把可编辑资产映射到现有 HuntManager 会话。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayableHuntContentCatalog", menuName = "Hunting in Darkness/Hunt Content Catalog")]
    public sealed class PlayableHuntContentCatalog : ScriptableObject
    {
        [SerializeField] private HexTileData startingTile;
        [SerializeField] private List<HexTileData> tilePool = new();
        [SerializeField] private List<EventData> eventPool = new();
        [SerializeField] private PlayableHuntNoiseProfile noiseProfile = new();

        public bool IsConfigured => startingTile != null && tilePool.Exists(tile => tile != null) && noiseProfile?.IsConfigured == true;
        public HexTileData StartingTile => startingTile;
        public IReadOnlyList<HexTileData> TilePool => tilePool;
        public IReadOnlyList<EventData> EventPool => eventPool;
        public PlayableHuntNoiseProfile NoiseProfile => noiseProfile;

        public bool IsAvailableForYear(int currentYear, out string reason)
        {
            if (!IsConfigured)
            {
                reason = "狩猎内容或噪音风险牌堆尚未配置。";
                return false;
            }
            if (noiseProfile.GetEligibleDangerEvents(currentYear).Count == 0)
            {
                reason = $"噪音风险牌堆在第 {Mathf.Max(1, currentYear)} 年没有可用危险事件。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public void ApplyTo(HuntManager manager)
        {
            if (manager == null || !IsConfigured) return;
            manager.StartingTileConfig = startingTile;
            manager.TilePool = tilePool.FindAll(tile => tile != null);
            manager.HuntEvents.HuntEventPool = PlayableEventTableRuntime.ExtendHunt(eventPool);
            manager.NoiseProfile = noiseProfile?.IsConfigured == true ? noiseProfile : null;
        }
    }

    /// <summary>
    /// 由组合根设定、Hunt Adapter 消费的短生命期运行时桥接。
    /// </summary>
    public static class PlayableHuntContentRuntime
    {
        private static PlayableHuntContentCatalog catalog;
        private static PlayableHuntContentBundle currentBundle;
        public static PlayableHuntContentCatalog Catalog => catalog;
        public static PlayableHuntRoutePlan CurrentRoutePlan => currentBundle?.DefaultRoute;
        public static PlayableHuntContentBundle CurrentBundle => currentBundle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            PlayableHuntContentBundle retired = SwapBundle(null);
            RetireBundle(retired);
            catalog = null;
        }

        public static void Configure(PlayableHuntContentCatalog contentCatalog)
        {
            if (contentCatalog == null)
            {
                PlayableHuntContentBundle retired = SwapBundle(null);
                PlayableHuntDestinationRuntime.ReleaseBundle(retired);
                RetireBundle(retired);
                catalog = null;
                return;
            }
            if (currentBundle != null)
            {
                Debug.LogError("[PlayableHuntContent] 活动狩猎内容 Bundle 存在，拒绝兼容目录重配置。");
                return;
            }
            catalog = contentCatalog;
        }

        internal static void ConfigureForInstallation(PlayableHuntContentCatalog contentCatalog)
        {
            catalog = contentCatalog;
        }

        public static void ApplyTo(HuntManager manager)
        {
            if (manager == null) return;
            if (currentBundle != null) return;
            if (catalog != null) catalog.ApplyTo(manager);
        }

        internal static PlayableHuntContentBundle SwapBundle(PlayableHuntContentBundle replacement)
        {
            PlayableHuntContentBundle previous = currentBundle;
            currentBundle = replacement;
            return previous;
        }

        internal static void RetireBundle(PlayableHuntContentBundle bundle)
        {
            if (bundle == null || ReferenceEquals(bundle, currentBundle)) return;
            bundle.Retire();
        }

        internal static bool IsEventGenerationLeased(PlayableEventTableGeneration generation) => currentBundle?.Leases(generation) == true;
    }
}
