using System;
using GameplayBase;
using HuntingInDarkness.Data;
using TMPro;
using UnityEngine;

using Cards3D;

namespace UI
{
    /// <summary>
    /// 卡牌 / 实体工厂。持有各类卡牌的 Prefab 引用，通过静态 CreateXxx 方法实例化并初始化。
    ///
    /// 用法：场景里放一个挂此脚本的物体并在 Inspector 指定各 Prefab；它在 Awake 注册为
    /// <see cref="Instance"/>，之后任何类直接 <c>EntityCreator.CreateXxx(...)</c> 调用即可，
    /// 无需持有引用。未放置（Instance 为空）或某 Prefab 未指定时，自动回退到程序化创建。
    ///
    /// Prefab 制作说明：
    ///   - Prefab 上挂卡牌脚本（如 ResourceCard3D）
    ///   - 添加 Body 子物体（Cube）并将其 Renderer 拖入脚本的 Body Renderer 字段
    ///   - 添加 TMP 子物体并将其拖入对应文字字段，在此处调整字体大小和样式
    ///   - BoxCollider 可预先配置，或留空由脚本在 InitView 时自动创建
    /// </summary>
    public class EntityCreator : MonoBehaviour
    {
        public const float DefaultCardThickness = 0.025f;
        private static EntityCreator _instance;

        /// <summary>
        /// 全局唯一实例（场景中挂此脚本的物体在 Awake 注册）。
        /// 首次访问时若尚未注册（如 Awake 时序早于本组件），自动 FindObjectOfType 兜底，
        /// 避免依赖 Awake 执行顺序。仍可能为 null（场景无此组件）→ 全部走程序化回退。
        /// </summary>
        public static EntityCreator Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<EntityCreator>(includeInactive: true);
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("卡牌 Prefab（为空时回退到程序化创建）")]
        [SerializeField] ResourceCard3D  _resourceCardPrefab;
        [SerializeField] HunterCard3D    _hunterCardPrefab;
        [SerializeField] InventionCard3D _inventionCardPrefab;
        [SerializeField] WorkshopCard3D  _workshopCardPrefab;

        [Header("战斗实体 Prefab（为空时回退到程序化胶囊）")]
        [SerializeField] CharacterEntity _characterEntityPrefab;

        private void Awake()
        {
            // 直接用后备字段，避免 Awake 期触发 getter 的 FindObjectOfType。
            // 保留先注册的（通常是 Inspector 配好 Prefab 的那个）；忽略重复实例。
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[EntityCreator] 场景中存在多个实例，保留已注册的 '{_instance.name}'，忽略 '{name}'。");
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ─── 角色实体 ─────────────────────────────────────────────────────
        // 注：本期只迁移角色；Boss / 组件 / 棋格仍由 EntityVisualizer 生成，
        //     后续可在此按同样模式新增 CreateBossEntity 等扩展位。

        public static CharacterEntity CreateCharacterEntity(
            int id, Vector3 worldPos, IGameContext ctx,
            Func<int, int> getCurrentTP, Func<int, int> getTPLimit,
            Action<int> onClicked, Action<int> onPlayCard, Transform parent)
        {
            var prefab = Instance != null ? Instance._characterEntityPrefab : null;
            if (prefab == null) Debug.LogError("CharacterEntity Prefab is null");

            CharacterEntity entity = prefab != null
                ? Instantiate(prefab, parent)
                : new();

            entity.transform.position = worldPos;
            entity.OnClicked          = onClicked;
            entity.OnCardPlayRequested = onPlayCard;
            entity.Init(id, ctx, getCurrentTP, getTPLimit);
            return entity;
        }

        // ─── 资源卡 ───────────────────────────────────────────────────────
        //todo 后期替换为用id查表拿数据
        public static ResourceCard3D CreateResourceCard(string resourceName, int count, Transform parent)
        {
            var prefab = Instance != null ? Instance._resourceCardPrefab : null;
            if (prefab != null)
            {
                var card = Instantiate(prefab, parent);
                card.Init(resourceName, count);
                return card;
            }
            return ResourceCard3D.Create(resourceName, count, parent);
        }

        // ─── 猎人卡 ───────────────────────────────────────────────────────

        public static HunterCard3D CreateHunterCard(HunterInstance hunter, Transform parent)
        {
            var prefab = Instance != null ? Instance._hunterCardPrefab : null;
            if (prefab != null)
            {
                var card = Instantiate(prefab, parent);
                card.Init(hunter);
                return card;
            }
            return HunterCard3D.Create(hunter, parent);
        }

        // ─── 发明卡 ───────────────────────────────────────────────────────

        public static InventionCard3D CreateInventionCard(InventionData data, Transform parent)
        {
            var prefab = Instance != null ? Instance._inventionCardPrefab : null;
            if (prefab != null)
            {
                var card = Instantiate(prefab, parent);
                card.Init(data);
                return card;
            }
            return InventionCard3D.Create(data, parent);
        }

        // ─── 工坊卡 ───────────────────────────────────────────────────────

        public static WorkshopCard3D CreateWorkshopCard(string workshopName, string description, Transform parent, Sprite icon = null)
        {
            var prefab = Instance != null ? Instance._workshopCardPrefab : null;
            if (prefab != null)
            {
                var card = Instantiate(prefab, parent);
                card.Init(workshopName, description, icon);
                return card;
            }
            return WorkshopCard3D.Create(workshopName, description, parent, icon);
        }
    }
}
