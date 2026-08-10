using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameplayBase.Board
{
    [ExecuteAlways]
    public class HexGridGizmos : MonoBehaviour
    {
        public enum GridShape { Hex, Square }

        [Header("Grid Type")]
        [SerializeField] private GridShape shape = GridShape.Hex;

        [Header("Hex Settings")]
        [SerializeField] private int radius = 3;

        [Header("Square Settings")]
        [SerializeField] private int columns = 7;
        [SerializeField] private int rows    = 7;

        [Header("Shared")]
        [SerializeField] private float cellSize   = 1f;
        [SerializeField] private Color gridColor  = new(0.4f, 0.85f, 1f, 0.9f);
        [SerializeField] private Color labelColor = Color.white;
        [SerializeField] private float yOffset    = 0.12f;
        [SerializeField] private int   labelSize  = 11;

        /// <summary>
        /// 让预览网格匹配实际生成的棋盘（半径 + 格距）。
        /// 由 CombatTestBootstrap 在编辑期同步调用，避免手动对齐 cellSize。
        /// </summary>
        public void ApplyPreview(int hexRadius, float cell)
        {
            shape    = GridShape.Hex;
            radius   = Mathf.Max(1, hexRadius);
            cellSize = cell;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gridColor;

#if UNITY_EDITOR
            var style = new GUIStyle
            {
                fontSize  = labelSize,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = labelColor }
            };
#endif
            if (shape == GridShape.Hex)
            {
                foreach (var coord in GenerateRadial(radius))
                {
                    Vector3 center = HexToWorld(coord) + Vector3.up * yOffset;
                    DrawHexWire(center);
#if UNITY_EDITOR
                    Handles.Label(center, $"({coord.x},{coord.y})", style);
#endif
                }
            }
            else
            {
                foreach (var coord in GenerateSquare(columns, rows))
                {
                    Vector3 center = SquareToWorld(coord) + Vector3.up * yOffset;
                    DrawSquareWire(center);
#if UNITY_EDITOR
                    Handles.Label(center, $"({coord.x},{coord.y})", style);
#endif
                }
            }
        }

        // ── Hex ──────────────────────────────────────────────────────────

        private void DrawHexWire(Vector3 center)
        {
            Vector3 prev = HexCorner(center, 5);
            for (int i = 0; i < 6; i++)
            {
                Vector3 curr = HexCorner(center, i);
                Gizmos.DrawLine(prev, curr);
                prev = curr;
            }
        }

        private Vector3 HexCorner(Vector3 center, int index)
        {
            float rad = Mathf.Deg2Rad * (60f * index + 30f); // pointy-top
            return new Vector3(
                center.x + cellSize * Mathf.Cos(rad),
                center.y,
                center.z + cellSize * Mathf.Sin(rad));
        }

        private Vector3 HexToWorld(Vector2Int coord)
        {
            float x = cellSize * (Mathf.Sqrt(3f) * coord.x + Mathf.Sqrt(3f) / 2f * coord.y);
            float z = cellSize * 1.5f * coord.y;
            return transform.position + new Vector3(x, 0f, z);
        }

        private static List<Vector2Int> GenerateRadial(int r)
        {
            var list = new List<Vector2Int>();
            for (int q = -r; q <= r; q++)
            for (int s = -r; s <= r; s++)
                if (Mathf.Abs(q) + Mathf.Abs(s) + Mathf.Abs(q + s) <= 2 * r)
                    list.Add(new Vector2Int(q, s));
            return list;
        }

        // ── Square ────────────────────────────────────────────────────────

        private void DrawSquareWire(Vector3 center)
        {
            float h = cellSize * 0.5f;
            Vector3 a = center + new Vector3(-h, 0,  h);
            Vector3 b = center + new Vector3( h, 0,  h);
            Vector3 c = center + new Vector3( h, 0, -h);
            Vector3 d = center + new Vector3(-h, 0, -h);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }

        private Vector3 SquareToWorld(Vector2Int coord)
        {
            // 原点居中：偏移使整个网格以 transform.position 为中心
            float offsetX = (columns - 1) * 0.5f;
            float offsetZ = (rows    - 1) * 0.5f;
            float x = (coord.x - offsetX) * cellSize;
            float z = (coord.y - offsetZ) * cellSize;
            return transform.position + new Vector3(x, 0f, z);
        }

        private static List<Vector2Int> GenerateSquare(int cols, int rs)
        {
            var list = new List<Vector2Int>(cols * rs);
            for (int x = 0; x < cols; x++)
            for (int y = 0; y < rs;   y++)
                list.Add(new Vector2Int(x, y));
            return list;
        }
    }
}