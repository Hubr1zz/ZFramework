using System.Text;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>工坊配方 3D 卡。展示输入与输出，只把点击转换为营地 ActionQueue 制作命令。</summary>
    public class WorkshopRecipeCard3D : CardView3D
    {
        public const float RW = 1.25f;
        public const float RH = 1.80f;

        private static readonly Color BodyColor = new(0.26f, 0.22f, 0.18f);
        private static readonly Color HoverColor = new(0.36f, 0.30f, 0.24f);
        private static readonly Color SuccessColor = new(0.20f, 0.40f, 0.22f);
        private static readonly Color FailureColor = new(0.44f, 0.18f, 0.16f);

        private CraftRecipe recipe;
        private System.Func<CraftRecipe, UniTask<SettlementCraftCommandResult>> craftCommand;
        private System.Func<RecipeIngredient, int> ingredientAmount;
        private TextMeshPro nameText;
        private TextMeshPro requirementText;
        private TextMeshPro hintText;
        private bool isCrafting;
        private bool lastCraftSucceeded;
        private bool lastCraftFailed;
        private string statusMessage = string.Empty;

        public CraftRecipe Recipe => recipe;
        public System.Action<WorkshopRecipeCard3D> OnCrafted;
        public override string DisplayName => recipe?.recipeName ?? base.DisplayName;
        public override float Width => RW;
        public override float Height => RH;

        protected override CardCategory GetDefaultCategory() => CardCategory.WorkshopRecipe;

        public static WorkshopRecipeCard3D Create(CraftRecipe recipe, System.Func<CraftRecipe, UniTask<SettlementCraftCommandResult>> craftCommand, Transform parent, Vector3 localPos = default, System.Func<RecipeIngredient, int> getIngredientAmount = null)
        {
            var go = new GameObject($"Recipe_{recipe?.recipeName}");
            go.transform.SetParent(parent, false);
            var card = go.AddComponent<WorkshopRecipeCard3D>();
            card.recipe = recipe;
            card.craftCommand = craftCommand;
            card.ingredientAmount = getIngredientAmount;
            card.InitView(localPos);
            card.transform.localPosition = localPos;
            return card;
        }

        protected override void BuildTextFields()
        {
            float textHeight = CD * 0.5f + 0.003f;
            nameText = MakeText("Name", new Vector3(0f, textHeight, RH * 0.38f), 0.13f, TextAlignmentOptions.Center, new Vector2(RW - 0.1f, 0.30f));
            requirementText = MakeText("Requirements", new Vector3(0f, textHeight, RH * 0.05f), 0.085f, TextAlignmentOptions.Center, new Vector2(RW - 0.12f, 0.72f));
            hintText = MakeText("Hint", new Vector3(0f, textHeight, -RH * 0.40f), 0.075f, TextAlignmentOptions.Center, new Vector2(RW - 0.1f, 0.30f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null || recipe == null) return;
            _bodyRenderer.material.color = lastCraftSucceeded ? SuccessColor : lastCraftFailed ? FailureColor : IsHovered ? HoverColor : BodyColor;
            if (nameText == null) return;

            string outputName = recipe.outputItem != null ? recipe.outputItem.itemName : "?";
            nameText.text = $"{recipe.recipeName}\n→ {outputName} ×{recipe.outputCount}";
            nameText.color = new Color(0.92f, 0.88f, 0.78f);
            requirementText.text = "需要:\n" + BuildRequirementText();
            requirementText.color = new Color(0.78f, 0.74f, 0.66f);
            if (isCrafting)
                hintText.text = "制作中…";
            else if (lastCraftSucceeded)
                hintText.text = "制作完成 · 可再次点击";
            else if (lastCraftFailed)
                hintText.text = string.IsNullOrWhiteSpace(statusMessage) ? "条件不足 · 点击重试" : statusMessage;
            else
                hintText.text = "点击卡牌制作";
            hintText.color = new Color(0.72f, 0.78f, 0.84f);
        }

        protected override void OnMouseDown()
        {
            if (isCrafting) return;
            CraftAsync().Forget();
        }

        private async UniTaskVoid CraftAsync()
        {
            if (craftCommand == null)
            {
                lastCraftFailed = true;
                ApplyVisuals();
                return;
            }

            isCrafting = true;
            lastCraftSucceeded = false;
            lastCraftFailed = false;
            ApplyVisuals();
            try
            {
                SettlementCraftCommandResult result = await craftCommand(recipe);
                lastCraftSucceeded = result.Succeeded;
                lastCraftFailed = !result.Succeeded;
                statusMessage = result.Reason;
                if (result.Succeeded)
                    OnCrafted?.Invoke(this);
            }
            catch (System.OperationCanceledException)
            {
                lastCraftFailed = true;
                statusMessage = "制作已取消";
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                lastCraftFailed = true;
                statusMessage = "制作中断";
            }
            finally
            {
                isCrafting = false;
                ApplyVisuals();
            }
        }

        private string BuildRequirementText()
        {
            if (recipe.ingredients == null || recipe.ingredients.Count == 0) return "（无）";
            var text = new StringBuilder();
            foreach (RecipeIngredient ingredient in recipe.ingredients)
                if (ingredient?.item != null)
                {
                    string source = ingredient.item.itemType == ItemType.Resource ? "资源" : "仓库";
                    int available = ingredientAmount?.Invoke(ingredient) ?? -1;
                    text.Append(source).Append(" · ").Append(ingredient.item.itemName).Append(' ');
                    if (available >= 0)
                        text.Append(available).Append('/').Append(ingredient.count);
                    else
                        text.Append('×').Append(ingredient.count);
                    text.AppendLine();
                }
            return text.ToString().TrimEnd();
        }

    }
}
