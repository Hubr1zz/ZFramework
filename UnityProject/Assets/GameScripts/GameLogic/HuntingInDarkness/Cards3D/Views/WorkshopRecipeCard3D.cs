using System.Collections.Generic;
using System.Text;
using Cards3D;
using HuntingInDarkness.Data;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>
    /// 工坊配方卡。比标准卡更大，持有一个 CraftRecipe（目标物品 + 所需素材）。
    /// 卡上有一个「可堆叠素材槽」，玩家把素材卡拖进去暂存：
    ///   - 素材凑齐配方 → 消耗素材，在工坊产出槽生成产物
    ///   - 面板关闭 → ReturnMaterials() 把暂存素材退回工坊产出槽
    /// </summary>
    public class WorkshopRecipeCard3D : CardView3D
    {
        // ─── 尺寸（比标准卡大）──────────────────────────────────────────────
        public const float RW = 1.25f;
        public const float RH = 1.80f;

        static readonly Color ColBody  = new(0.26f, 0.22f, 0.18f);
        static readonly Color ColHover = new(0.36f, 0.30f, 0.24f);
        static readonly Color ColReady = new(0.20f, 0.40f, 0.22f);

        CraftRecipe _recipe;
        CardSlot    _outputSlot;   // 工坊产出槽（产物/退还素材落点）
        CardSlot    _materialSlot; // 本卡的素材暂存槽

        TextMeshPro _nameText;
        TextMeshPro _reqText;
        TextMeshPro _hintText;

        public CraftRecipe Recipe => _recipe;
        public System.Action<WorkshopRecipeCard3D> OnCrafted;

        public override string DisplayName => _recipe?.recipeName ?? base.DisplayName;
        protected override CardCategory GetDefaultCategory() => CardCategory.WorkshopRecipe;

        // 比标准卡更大：基类按 Width/Height 自动构建几何
        public override float Width  => RW;
        public override float Height => RH;

        // ─── 工厂 ─────────────────────────────────────────────────────────

        public static WorkshopRecipeCard3D Create(
            CraftRecipe recipe, CardSlot outputSlot, Transform parent, Vector3 localPos = default)
        {
            var go = new GameObject($"Recipe_{recipe?.recipeName}");
            go.transform.SetParent(parent, false);
            var card = go.AddComponent<WorkshopRecipeCard3D>();
            card._recipe     = recipe;
            card._outputSlot = outputSlot;
            card.InitView(localPos);                 // 基类按 Width/Height(=RW/RH) 建几何
            card.transform.localPosition = localPos;
            card.BuildMaterialSlot();
            return card;
        }

        // ─── 文字 ─────────────────────────────────────────────────────────

        protected override void BuildTextFields()
        {
            float ty = CD * 0.5f + 0.003f;

            _nameText = MakeText("Name",
                new Vector3(0f, ty, RH * 0.40f), 0.13f,
                TextAlignmentOptions.Center,
                new Vector2(RW - 0.1f, 0.26f));

            _reqText = MakeText("Req",
                new Vector3(0f, ty, RH * 0.12f), 0.085f,
                TextAlignmentOptions.Center,
                new Vector2(RW - 0.12f, 0.55f));

            _hintText = MakeText("Hint",
                new Vector3(0f, ty, -RH * 0.46f), 0.075f,
                TextAlignmentOptions.Center,
                new Vector2(RW - 0.1f, 0.16f));
        }

        // ─── 素材槽（位于卡下半部）──────────────────────────────────────────

        private void BuildMaterialSlot()
        {
            _materialSlot = CardSlot.Create(
                transform, transform.position,
                CW * 0.85f, CH * 0.55f,
                stackable: true, CardCategory.Resource);
            _materialSlot.transform.localPosition = new Vector3(0f, 0.006f, -RH * 0.22f);
            _materialSlot.StackDir   = StackDirection.StackUp;
            _materialSlot.Previewable = true;
            _materialSlot.OnCardPushed += OnMaterialAdded;
        }

        // ─── 视觉 ─────────────────────────────────────────────────────────

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null || _recipe == null) return;

            bool ready = IsRecipeSatisfied();
            _bodyRenderer.material.color = ready ? ColReady : (IsHovered ? ColHover : ColBody);

            if (_nameText == null) return;

            string outName = _recipe.outputItem != null ? _recipe.outputItem.itemName : "?";
            _nameText.text  = $"{_recipe.recipeName}\n→ {outName} ×{_recipe.outputCount}";
            _nameText.color = new Color(0.92f, 0.88f, 0.78f);

            _reqText.text  = "需要:\n" + BuildRequirementText();
            _reqText.color = new Color(0.78f, 0.74f, 0.66f);

            _hintText.text  = "拖入素材制作";
            _hintText.color = new Color(0.62f, 0.66f, 0.74f);
        }

        private string BuildRequirementText()
        {
            if (_recipe.ingredients == null || _recipe.ingredients.Count == 0) return "（无）";
            var have = CountMaterials();
            var sb = new StringBuilder();
            foreach (var ing in _recipe.ingredients)
            {
                if (ing.item == null) continue;
                int got = have.TryGetValue(ing.item.itemName, out int v) ? v : 0;
                sb.Append($"{ing.item.itemName} {got}/{ing.count}\n");
            }
            return sb.ToString().TrimEnd();
        }

        // ─── 制作逻辑 ─────────────────────────────────────────────────────

        bool _isCrafting;

        private void OnMaterialAdded(CardView3D _)
        {
            if (_isCrafting) return; // 防止退还剩余素材时的 OnCardPushed 重入
            _isCrafting = true;
            while (IsRecipeSatisfied()) CraftOnce();
            _isCrafting = false;
            ApplyVisuals();
        }

        private Dictionary<string, int> CountMaterials()
        {
            var have = new Dictionary<string, int>();
            if (_materialSlot == null) return have;
            foreach (var c in _materialSlot.StackCards)
            {
                if (c is ResourceCard3D r && r.ResourceName != null)
                    have[r.ResourceName] = (have.TryGetValue(r.ResourceName, out int v) ? v : 0) + 1;
            }
            return have;
        }

        private bool IsRecipeSatisfied()
        {
            if (_recipe?.ingredients == null) return false;
            var have = CountMaterials();
            foreach (var ing in _recipe.ingredients)
            {
                if (ing.item == null) continue;
                int got = have.TryGetValue(ing.item.itemName, out int v) ? v : 0;
                if (got < ing.count) return false;
            }
            return true;
        }

        /// <summary>消耗一份配方所需素材并产出一次（调用方保证已满足）。</summary>
        private void CraftOnce()
        {
            // 需求计数（按名）
            var need = new Dictionary<string, int>();
            foreach (var ing in _recipe.ingredients)
                if (ing.item != null)
                    need[ing.item.itemName] = (need.TryGetValue(ing.item.itemName, out int v) ? v : 0) + ing.count;

            // 取出全部素材，消耗所需，剩余退回素材槽
            var all = _materialSlot.TakeAll();
            var leftovers = new List<CardView3D>();
            foreach (var c in all)
            {
                if (c is ResourceCard3D r && need.TryGetValue(r.ResourceName, out int n) && n > 0)
                {
                    need[r.ResourceName] = n - 1;
                    Destroy(c.gameObject);
                }
                else leftovers.Add(c);
            }
            foreach (var c in leftovers) _materialSlot.PushCard(c);

            // 在工坊产出槽生成产物
            if (_outputSlot != null && _recipe.outputItem != null)
            {
                var outCard = ResourceCard3D.Create(
                    _recipe.outputItem.itemName, _recipe.outputCount, _outputSlot.transform.root);
                _outputSlot.PushCard(outCard);
            }

            OnCrafted?.Invoke(this);
        }

        // ─── 关闭面板时退还暂存素材 ─────────────────────────────────────────

        public void ReturnMaterials()
        {
            if (_materialSlot == null) return;
            var all = _materialSlot.TakeAll();
            foreach (var c in all)
            {
                if (_outputSlot != null) _outputSlot.PushCard(c);
                else Destroy(c.gameObject);
            }
        }
    }
}
