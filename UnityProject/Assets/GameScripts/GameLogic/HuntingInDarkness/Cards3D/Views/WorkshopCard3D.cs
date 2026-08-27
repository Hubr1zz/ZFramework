using System.Collections.Generic;
using Cards3D;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using TMPro;
using UnityEngine;

using UI;

namespace Cards3D
{
    /// <summary>
    /// 工坊卡。展示工坊名称、图标和描述。
    /// 点击后弹出 3D 制作面板（WorkshopCraftPanel），列出已解锁配方。
    /// 持有可生产配方列表，制作状态由 Settlement ActionQueue 统一提交。
    /// </summary>
    public class WorkshopCard3D : CardView3D
    {
        static readonly Color ColBody  = new(0.22f, 0.28f, 0.36f);
        static readonly Color ColHover = new(0.32f, 0.42f, 0.54f);

        string _workshopName;
        string _description;
        Sprite _icon;

        [SerializeField] TextMeshPro    _nameText;
        [SerializeField] TextMeshPro    _descText;
        [SerializeField] SpriteRenderer _imageRenderer;

        // ─── 配方 ──────────────────────────────────────────────────────────
        [Header("工坊数据")]
        [SerializeField] List<CraftRecipe> _recipes = new(); // 已解锁的可生产配方
        private System.Func<CraftRecipe, UniTask<SettlementCraftCommandResult>> craftCommand;
        private System.Func<RecipeIngredient, int> ingredientAmount;

        public WorkshopCraftPanel _panel;

        /// <summary>已解锁配方列表。</summary>
        public IReadOnlyList<CraftRecipe> Recipes => _recipes;

        public System.Func<CraftRecipe, UniTask<SettlementCraftCommandResult>> CraftCommand => craftCommand;
        public System.Func<RecipeIngredient, int> IngredientAmount => ingredientAmount;

        /// <summary>点击工坊卡时触发（外部可附加逻辑；面板由本卡自行开关）。</summary>
        public System.Action<WorkshopCard3D> OnCraftMenuRequested;

        public override string DisplayName => _workshopName ?? base.DisplayName;

        protected override CardCategory GetDefaultCategory() => CardCategory.Workshop;

        // ─── 配置 ──────────────────────────────────────────────────────────

        public void Configure(List<CraftRecipe> recipes, System.Func<CraftRecipe, UniTask<SettlementCraftCommandResult>> craftCommand = null, System.Func<RecipeIngredient, int> getIngredientAmount = null)
        {
            if (recipes != null) _recipes = recipes;
            if (craftCommand != null) this.craftCommand = craftCommand;
            if (getIngredientAmount != null) ingredientAmount = getIngredientAmount;
        }

        // ─── 初始化 ────────────────────────────────────────────────────────

        public void Init(string workshopName, string description, Sprite icon = null, Vector3 localPos = default)
        {
            _workshopName   = workshopName;
            _description    = description;
            _icon           = icon;
            gameObject.name = $"Workshop_{workshopName}";
            InitView(localPos);
        }

        // ─── 工厂 ─────────────────────────────────────────────────────────

        public static WorkshopCard3D Create(
            string workshopName, string description, Transform parent,
            Sprite icon = null, Vector3 localPos = default)
        {
            var go   = new GameObject($"Workshop_{workshopName}");
            go.transform.SetParent(parent, false);
            var card = go.AddComponent<WorkshopCard3D>();
            card.Init(workshopName, description, icon, localPos);
            return card;
        }

        // ─── CardView3D ────────────────────────────────────────────────────

        protected override void BuildTextFields()
        {
            if (_nameText != null) return; // prefab 已配置，跳过

            float ty = CD * 0.5f + 0.003f;

            _nameText = MakeText("Name",
                new Vector3(0f, ty, CH * 0.38f), 0.10f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.06f, 0.20f));

            _descText = MakeText("Desc",
                new Vector3(0f, ty, CH * 0.02f), 0.068f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.06f, 0.44f));

            if (_imageRenderer == null)
            {
                var imgGo = new GameObject("Image");
                imgGo.transform.SetParent(transform, false);
                imgGo.transform.localPosition = new Vector3(0f, ty + 0.001f, CH * 0.20f);
                imgGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                imgGo.transform.localScale    = new Vector3(0.52f, 0.52f, 1f);
                _imageRenderer = imgGo.AddComponent<SpriteRenderer>();
            }
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null) return;

            _bodyRenderer.material.color = IsHovered ? ColHover : ColBody;

            if (_imageRenderer != null) _imageRenderer.sprite = _icon;

            if (_nameText == null) return;

            _nameText.text  = _workshopName ?? "";
            _nameText.color = new Color(0.88f, 0.92f, 0.96f);

            _descText.text  = _description ?? "";
            _descText.color = new Color(0.65f, 0.70f, 0.78f);
        }

        protected override void OnMouseDown()
        {
            OnCraftMenuRequested?.Invoke(this);
            TogglePanel();
        }

        // ─── 制作面板开关 ───────────────────────────────────────────────────

        private void TogglePanel()
        {
            if (_panel != null && _panel.IsOpen) { _panel.Close(); return; }

            if (_panel == null)
                _panel = WorkshopCraftPanel.Create(transform.root);

            // 面板平铺在工坊卡前方（俯视下方）
            Vector3 pos = transform.position + new Vector3(0f, 0.04f, -1.8f);
            _panel.Open(this, pos);
        }

        public void Refresh() => ApplyVisuals();
    }
}
