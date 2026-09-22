using UnityEngine;

namespace GameplayBase.Board
{
    /// <summary>
    /// 六边形朝向（pointy-top，axial 坐标）。
    /// 枚举按逆时针旋转顺序排列，<see cref="HexDirections.Rotate"/> 可按步数旋转。
    /// </summary>
    public enum HexDirection
    {
        E  = 0, // 东     (+1, 0)
        NE = 1, // 东北   ( 0,+1)
        NW = 2, // 西北   (-1,+1)
        W  = 3, // 西     (-1, 0)
        SW = 4, // 西南   ( 0,-1)
        SE = 5, // 东南   (+1,-1)
    }

    /// <summary>HexDirection 工具：偏移、邻格、旋转。</summary>
    public static class HexDirections
    {
        public const int Count = 6;

        // 与 HexDirection 枚举一一对应（旋转顺序）
        private static readonly Vector2Int[] _offsets =
        {
            new( 1,  0), // E
            new( 0,  1), // NE
            new(-1,  1), // NW
            new(-1,  0), // W
            new( 0, -1), // SW
            new( 1, -1), // SE
        };

        public static Vector2Int Offset(HexDirection dir) => _offsets[(int)dir];

        public static Vector2Int Neighbor(Vector2Int coord, HexDirection dir) =>
            coord + _offsets[(int)dir];

        /// <summary>按 steps 步旋转（正数逆时针，负数顺时针）。</summary>
        public static HexDirection Rotate(HexDirection dir, int steps)
        {
            int i = (((int)dir + steps) % Count + Count) % Count;
            return (HexDirection)i;
        }

        public static HexDirection Opposite(HexDirection dir) => Rotate(dir, 3);
    }
}
