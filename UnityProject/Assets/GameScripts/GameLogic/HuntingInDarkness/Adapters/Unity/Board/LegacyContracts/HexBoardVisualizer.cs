using System.Collections.Generic;
using UnityEngine;

namespace GameplayBase.Board
{
    /// <summary>
    /// 可视化六边形棋盘。构造时立即生成格子，不依赖 MonoBehaviour 生命周期。
    /// 支持高亮显示指定格子（用于玩家输入提示）。
    /// </summary>
    public class HexBoardVisualizer
    {
        private readonly BoardManager _boardManager;
        private readonly Transform _parent;
        private readonly float _tileHeight;
        private readonly float _tileScale;
        private readonly Color _idleColor;
        private readonly Color _highlightColor;
        private readonly Color _occupiedColor;

        private readonly Dictionary<Vector2Int, GameObject> _tileObjects = new();
        private readonly Dictionary<Vector2Int, Renderer> _tileRenderers = new();
        private readonly HashSet<Vector2Int> _highlighted = new();

        public HexBoardVisualizer(
            BoardManager boardManager,
            Transform parent,
            float tileHeight      = 0.08f,
            float tileScale       = 0.92f,
            Color? idleColor      = null,
            Color? highlightColor = null,
            Color? occupiedColor  = null)
        {
            _boardManager    = boardManager;
            _parent          = parent;
            _tileHeight      = tileHeight;
            _tileScale       = tileScale;
            _idleColor       = idleColor      ?? new Color(0.35f, 0.35f, 0.40f, 1f);
            _highlightColor  = highlightColor ?? new Color(0.30f, 0.85f, 0.30f, 1f);
            _occupiedColor   = occupiedColor  ?? new Color(0.80f, 0.30f, 0.30f, 1f);

            BuildTiles();
        }

        private void BuildTiles()
        {
            foreach (var coord in _boardManager.GetAllCoords())
            {
                var worldPos = _boardManager.TileToWorld(coord);
                float scale  = _boardManager.CellSize * _tileScale;

                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = $"Tile({coord.x},{coord.y})";
                go.transform.SetParent(_parent);
                go.transform.position   = worldPos;
                go.transform.localScale = new Vector3(scale, _tileHeight, scale);

                var rend = go.GetComponent<Renderer>();
                rend.material = new Material(rend.sharedMaterial) { color = _idleColor };

                var col = go.GetComponent<Collider>();
                if (col != null) col.enabled = false;

                _tileObjects[coord]   = go;
                _tileRenderers[coord] = rend;
            }
        }

        public void Highlight(List<Vector2Int> tiles)
        {
            ClearHighlights();
            foreach (var tile in tiles)
            {
                if (_tileRenderers.TryGetValue(tile, out var rend))
                {
                    rend.material.color = _highlightColor;
                    _highlighted.Add(tile);
                }
            }
        }

        public void ClearHighlights()
        {
            foreach (var tile in _highlighted)
            {
                if (_tileRenderers.TryGetValue(tile, out var rend))
                    rend.material.color = _idleColor;
            }
            _highlighted.Clear();
        }

        public void MarkOccupied(Vector2Int tile)
        {
            if (_tileRenderers.TryGetValue(tile, out var rend))
                rend.material.color = _occupiedColor;
        }
    }
}
