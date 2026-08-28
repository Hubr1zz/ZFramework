using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>内容引用已解析后的普通狩猎恢复载荷；不包含 View 或 ActionEnvironment 对象。</summary>
    public sealed class PlayableHuntRuntimeState
    {
        public int Year { get; set; }
        public List<HunterInstance> Hunters { get; set; } = new();
        public int SelectedHunterId { get; set; }
        public Vector2Int SquadPosition { get; set; }
        public Dictionary<Vector2Int, HexTileInstance> Map { get; set; } = new();
        public StatefulRandomState RandomState { get; set; }
        public int RescuedPopulation { get; set; }
    }
}
