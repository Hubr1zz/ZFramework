using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cards3D
{
    [Serializable]
    public struct CardDimensions
    {
        [SerializeField, Min(0.01f)] private float width;
        [SerializeField, Min(0.01f)] private float height;
        [SerializeField, Min(0.001f)] private float depth;

        public CardDimensions(float width, float height, float depth)
        {
            this.width = width;
            this.height = height;
            this.depth = depth;
        }

        public float Width => width;
        public float Height => height;
        public float Depth => depth;
        public bool IsValid => width > 0f && height > 0f && depth > 0f;
        public static CardDimensions Standard => new(CardView3D.CW, CardView3D.CH, CardView3D.CD);
    }

    [Serializable]
    public sealed class CardPrefabDefinition
    {
        [SerializeField] private CardView3D prefab;
        [SerializeField] private CardDimensions dimensions = new(CardView3D.CW, CardView3D.CH, CardView3D.CD);

        public CardPrefabDefinition() { }

        public CardPrefabDefinition(CardView3D prefab, CardDimensions dimensions)
        {
            this.prefab = prefab;
            this.dimensions = dimensions;
        }

        public CardView3D Prefab => prefab;
        public Type ViewType => prefab != null ? prefab.GetType() : null;
        public CardDimensions Dimensions => dimensions.IsValid ? dimensions : CardDimensions.Standard;
    }

    /// <summary>按具体卡牌组件类型统一保存 Prefab 与物理尺寸；未配置类型继续使用程序化回退。</summary>
    [CreateAssetMenu(fileName = "CardPrefabCatalog", menuName = "Hunting in Darkness/Card Prefab Catalog")]
    public sealed class CardPrefabCatalog : ScriptableObject
    {
        [SerializeField] private List<CardPrefabDefinition> cards = new();

        public bool TryGetPrefab<T>(out T prefab) where T : CardView3D
        {
            if (TryGetDefinition(typeof(T), out CardPrefabDefinition definition))
            {
                prefab = (T)definition.Prefab;
                return prefab != null;
            }
            prefab = null;
            return false;
        }

        public CardDimensions GetDimensions(Type viewType, CardDimensions fallback)
        {
            if (TryGetDimensions(viewType, out CardDimensions dimensions)) return dimensions;
            return fallback.IsValid ? fallback : CardDimensions.Standard;
        }

        public bool TryGetDimensions(Type viewType, out CardDimensions dimensions)
        {
            if (viewType != null && TryGetDefinition(viewType, out CardPrefabDefinition definition))
            {
                dimensions = definition.Dimensions;
                return true;
            }
            dimensions = default;
            return false;
        }

        private bool TryGetDefinition(Type viewType, out CardPrefabDefinition definition)
        {
            foreach (CardPrefabDefinition candidate in cards)
            {
                if (candidate?.ViewType != viewType) continue;
                definition = candidate;
                return true;
            }
            definition = null;
            return false;
        }

        private void OnValidate()
        {
            var types = new HashSet<Type>();
            foreach (CardPrefabDefinition definition in cards)
            {
                Type type = definition?.ViewType;
                if (type == null) continue;
                if (!types.Add(type))
                    Debug.LogWarning($"[CardPrefabCatalog] 卡牌类型 {type.Name} 配置了多个 Prefab，将使用列表中的第一个。", this);
            }
        }
    }
}
