using System;
using UnityEngine;

namespace Cards3D
{
    /// <summary>卡牌视觉组合入口。优先实例化 Catalog Prefab，缺省时保留程序化回退。</summary>
    public static class CardPrefabRegistry
    {
        private static CardPrefabCatalog catalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState() => catalog = null;

        public static void Configure(CardPrefabCatalog prefabCatalog) => catalog = prefabCatalog;

        public static CardDimensions GetDimensions(Type viewType, CardDimensions fallback)
        {
            if (catalog != null) return catalog.GetDimensions(viewType, fallback);
            return fallback.IsValid ? fallback : CardDimensions.Standard;
        }

        public static T Create<T>(Transform parent, string objectName) where T : CardView3D
        {
            T card;
            if (catalog != null && catalog.TryGetPrefab(out T prefab))
            {
                card = UnityEngine.Object.Instantiate(prefab, parent, false);
                card.gameObject.name = objectName;
                return card;
            }

            var gameObject = new GameObject(objectName);
            gameObject.transform.SetParent(parent, false);
            return gameObject.AddComponent<T>();
        }
    }
}
