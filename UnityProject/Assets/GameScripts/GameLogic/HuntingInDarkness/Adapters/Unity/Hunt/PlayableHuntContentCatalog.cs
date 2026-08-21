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

        public bool IsConfigured => startingTile != null && tilePool.Exists(tile => tile != null);
        public IReadOnlyList<EventData> EventPool => eventPool;

        public void ApplyTo(HuntManager manager)
        {
            if (manager == null || !IsConfigured) return;
            manager.StartingTileConfig = startingTile;
            manager.TilePool = tilePool.FindAll(tile => tile != null);
            manager.HuntEvents.HuntEventPool = PlayableEventTableRuntime.ExtendHunt(eventPool);
        }
    }

    /// <summary>
    /// 由组合根设定、Hunt Adapter 消费的短生命期运行时桥接。
    /// </summary>
    public static class PlayableHuntContentRuntime
    {
        private static PlayableHuntContentCatalog catalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            catalog = null;
        }

        public static void Configure(PlayableHuntContentCatalog contentCatalog)
        {
            catalog = contentCatalog;
        }

        public static void ApplyTo(HuntManager manager)
        {
            if (catalog == null) return;
            catalog.ApplyTo(manager);
        }
    }
}
