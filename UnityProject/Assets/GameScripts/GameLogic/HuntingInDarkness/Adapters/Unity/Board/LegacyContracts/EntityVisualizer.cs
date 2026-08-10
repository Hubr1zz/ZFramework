using System;
using System.Collections.Generic;
using Core;
using TMPro;
using UnityEngine;

namespace GameplayBase.Board
{
    /// <summary>
    /// 棋盘上的实体可视化：胶囊体占位 + 头顶 TP 标签 + 点击回调。
    /// </summary>
    public class EntityVisualizer
    {
        private readonly BoardManager _boardManager;
        private readonly Transform _parent;
        private readonly float _characterHeight;
        private readonly float _characterRadius;
        private readonly float _bossHeight;
        private readonly float _bossRadius;
        private readonly Color _characterColor;
        private readonly Color _bossColor;

        private readonly Dictionary<int, GameObject> _entityObjects = new();
        private readonly Dictionary<int, GameObject> _tpLabelObjects = new();
        private readonly Dictionary<int, TextMeshPro> _tpLabels = new();
        private readonly Dictionary<int, float> _entityHeights = new();

        /// <summary>实体被点击时触发（由 GameManager 注入）</summary>
        public Action<int> OnEntityClicked;

        /// <summary>返回实体当前 TP（由 GameManager 注入）</summary>
        public Func<int, int> GetCurrentTP;

        /// <summary>返回实体本回合 TP 上限（由 GameManager 注入）</summary>
        public Func<int, int> GetTPLimit;

        public EntityVisualizer(
            BoardManager boardManager,
            Transform parent,
            float characterHeight = 1.0f,
            float characterRadius = 0.25f,
            float bossHeight      = 1.6f,
            float bossRadius      = 0.4f,
            Color? characterColor = null,
            Color? bossColor      = null)
        {
            _boardManager     = boardManager;
            _parent           = parent;
            _characterHeight  = characterHeight;
            _characterRadius  = characterRadius;
            _bossHeight       = bossHeight;
            _bossRadius       = bossRadius;
            _characterColor   = characterColor ?? new Color(0.25f, 0.45f, 0.95f, 1f);
            _bossColor        = bossColor      ?? new Color(0.85f, 0.15f, 0.15f, 1f);

            EventBus.Subscribe<TimePointChangedEvent>(OnTimePointChanged);
        }

        public void SpawnEntity(int entityId, Vector2Int tile, bool isBoss)
        {
            if (_entityObjects.ContainsKey(entityId)) return;

            float height = isBoss ? _bossHeight : _characterHeight;
            float radius = isBoss ? _bossRadius : _characterRadius;

            var tileBase = _boardManager.TileToWorld(tile); // y = 0
            var worldPos = tileBase;
            worldPos.y  += height * 0.5f;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = isBoss ? $"Boss_{entityId}" : $"Character_{entityId}";
            go.transform.SetParent(_parent);
            go.transform.position   = worldPos;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

            var rend = go.GetComponent<Renderer>();
            rend.material = new Material(rend.sharedMaterial)
            {
                color = isBoss ? _bossColor : _characterColor
            };

            var col = go.GetComponent<Collider>();
            if (col != null)
                col.enabled = !isBoss; // Boss 不需要点击

            if (!isBoss)
            {
                var handler = go.AddComponent<EntityClickHandler>();
                handler.EntityId  = entityId;
                handler.OnClicked = (id) => OnEntityClicked?.Invoke(id);

                CreateTPLabel(entityId, tileBase, height);
            }

            _entityObjects[entityId]  = go;
            _entityHeights[entityId]  = height;
        }

        public void MoveEntity(int entityId, Vector2Int tile)
        {
            if (!_entityObjects.TryGetValue(entityId, out var go)) return;

            float height = _entityHeights.TryGetValue(entityId, out var h) ? h : _characterHeight;
            var tileBase = _boardManager.TileToWorld(tile);
            go.transform.position = tileBase + Vector3.up * (height * 0.5f);

            if (_tpLabelObjects.TryGetValue(entityId, out var labelGo))
                labelGo.transform.position = tileBase + Vector3.up * (height + 0.35f);
        }

        public void RemoveEntity(int entityId)
        {
            if (_entityObjects.TryGetValue(entityId, out var go))
            {
                UnityEngine.Object.Destroy(go);
                _entityObjects.Remove(entityId);
            }
            if (_tpLabelObjects.TryGetValue(entityId, out var labelGo))
            {
                UnityEngine.Object.Destroy(labelGo);
                _tpLabelObjects.Remove(entityId);
                _tpLabels.Remove(entityId);
            }
            _entityHeights.Remove(entityId);
        }

        // ─── TP 标签 ─────────────────────────────────────────────────────

        private void CreateTPLabel(int entityId, Vector3 tileBase, float height)
        {
            var labelGo = new GameObject($"TimePointLabel_{entityId}");
            labelGo.transform.SetParent(_parent);
            labelGo.transform.position = tileBase + Vector3.up * (height + 0.35f);
            labelGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.fontSize   = 0.38f;
            tmp.alignment  = TextAlignmentOptions.Center;
            tmp.color      = Color.white;
            tmp.text       = "--";
            tmp.rectTransform.sizeDelta = new Vector2(1.2f, 0.45f);

            _tpLabelObjects[entityId] = labelGo;
            _tpLabels[entityId]       = tmp;
        }

        private void RefreshTPLabel(int entityId)
        {
            if (!_tpLabels.TryGetValue(entityId, out var tmp)) return;
            if (GetCurrentTP == null || GetTPLimit == null) return;

            int current   = GetCurrentTP(entityId);
            int limit     = GetTPLimit(entityId);
            int remaining = limit - current;

            if (limit <= 0)
            {
                tmp.text  = "--";
                tmp.color = Color.white;
                return;
            }

            tmp.text = $"{remaining}/{limit}";
            tmp.color = remaining > 0
                ? Color.white
                : remaining == 0
                    ? new Color(1f, 0.85f, 0.1f)   // 黄：恰好耗尽
                    : new Color(1f, 0.3f, 0.3f);    // 红：溢出
        }

        private void OnTimePointChanged(TimePointChangedEvent evt)
        {
            if (!evt.IsBoss)
                RefreshTPLabel(evt.EntityId);
        }

        /// <summary>由 GameManager 在所有 Func 委托注入完成后调用，刷新初始显示</summary>
        public void RefreshAllTPLabels()
        {
            foreach (var id in _tpLabels.Keys)
                RefreshTPLabel(id);
        }
    }
}
