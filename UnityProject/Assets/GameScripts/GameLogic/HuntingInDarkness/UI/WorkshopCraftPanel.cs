using System.Collections.Generic;
using Cards3D;
using HuntingInDarkness.Data;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 工坊制作面板。点击 WorkshopCard3D 弹出，平铺在桌面上（继承 WorldSpaceViewPanel）。
    /// 把已解锁的配方逐张铺成一排（WorkshopRecipeCard3D），每张可拖入素材制作。
    /// 关闭时把各配方卡素材槽里暂存的素材退回工坊产出槽。
    /// </summary>
    public class WorkshopCraftPanel : WorldSpaceViewPanel
    {
        const float RecipeGap   = 0.35f;          // 配方卡间距
        const float SidePad     = 0.5f;
        const float RecipeW     = WorkshopRecipeCard3D.RW;
        const float RecipeH     = WorkshopRecipeCard3D.RH;

        readonly List<WorkshopRecipeCard3D> _recipeCards = new();
        WorkshopCard3D _workshop;
        ClickProxy     _closeBtn;

        public bool IsOpen { get; private set; }

        // ─── 工厂 ─────────────────────────────────────────────────────────

        public static WorkshopCraftPanel Create(Transform parent)
        {
            var go = new GameObject("WorkshopCraftPanel");
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<WorkshopCraftPanel>();
            panel.BuildBase();
            panel.BuildCloseButton();
            panel.gameObject.SetActive(false);
            return panel;
        }

        // ─── 打开 / 关闭 ──────────────────────────────────────────────────

        public void Open(WorkshopCard3D workshop, Vector3 worldPos)
        {
            _workshop = workshop;
            IsOpen    = true;

            ClearRecipeCards();

            var recipes = workshop.Recipes;
            int n = recipes?.Count ?? 0;

            Title.text = $"{workshop.DisplayName} · 制作";

            float rowStep = RecipeW + RecipeGap;
            float startX  = -(n - 1) * 0.5f * rowStep;

            for (int i = 0; i < n; i++)
            {
                var card = WorkshopRecipeCard3D.Create(
                    recipes[i], workshop.CraftCommand, ContentRoot,
                    new Vector3(startX + i * rowStep, 0f, 0f), workshop.IngredientAmount);
                card.OnCrafted += OnRecipeCrafted;
                _recipeCards.Add(card);
            }

            float w = Mathf.Max(RecipeW + SidePad, n * rowStep + SidePad);
            float h = RecipeH + 0.9f;
            SetSize(w, h);
            PlaceCloseButton(w, h);

            ShowAt(worldPos);
        }

        public void Close()
        {
            ClearRecipeCards();
            IsOpen = false;
            Hide();
        }

        private void ClearRecipeCards()
        {
            foreach (var c in _recipeCards)
                if (c != null)
                {
                    c.OnCrafted -= OnRecipeCrafted;
                    Destroy(c.gameObject);
                }
            _recipeCards.Clear();
        }

        private void OnRecipeCrafted(WorkshopRecipeCard3D card)
        {
            if (card?.Recipe == null || _workshop == null) return;
            Title.text = $"{_workshop.DisplayName} · 已完成 {card.Recipe.recipeName}";
        }

        // ─── 关闭按钮 ─────────────────────────────────────────────────────

        private void BuildCloseButton()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CloseButton";
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.5f, 0.04f, 0.22f);
            go.GetComponent<Renderer>().material.color = new Color(0.45f, 0.16f, 0.16f);

            _closeBtn = go.AddComponent<ClickProxy>();
            _closeBtn.OnClick = Close;

            var tGo = new GameObject("CloseLabel");
            tGo.transform.SetParent(go.transform, false);
            tGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            // 反缩放，让文字不随按钮立方体压扁
            tGo.transform.localScale = new Vector3(1f / 0.5f, 1f / 0.22f, 1f);
            var tmp = tGo.AddComponent<TextMeshPro>();
            tmp.text      = "关闭";
            tmp.fontSize  = 0.10f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = new Color(0.95f, 0.85f, 0.82f);
            tmp.rectTransform.sizeDelta = new Vector2(0.45f, 0.18f);
        }

        private void PlaceCloseButton(float w, float h)
        {
            if (_closeBtn == null) return;
            _closeBtn.transform.localPosition = new Vector3(
                w * 0.5f - 0.4f, 0.03f, h * 0.5f - 0.18f);
        }
    }
}
