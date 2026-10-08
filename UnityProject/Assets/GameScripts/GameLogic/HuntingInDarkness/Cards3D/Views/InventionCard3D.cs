using System.Collections.Generic;
using HuntingInDarkness.Data;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    public class InventionCard3D : SlotDraggableCardView3D
    {
        static readonly Color ColBody = new(0.02f, 0.04f, 0.07f);
        static readonly Color ColHover = new(0.06f, 0.11f, 0.18f);
        static readonly Color ColAvailable = new(0.08f, 0.06f, 0.015f);
        static readonly Color ColAvailableHover = new(0.13f, 0.10f, 0.025f);
        static readonly Color ColLocked = new(0.13f, 0.14f, 0.17f);

        InventionData              _data;
        List<InventionActiveEffect> _effects = new();
        bool isUnlocked;
        bool canUnlock;
        bool isReadOnlyPreview;
        string availabilityReason = string.Empty;
        CardInspectionContent inspectionContent;
        bool hasInspectionContent;

        [SerializeField] TextMeshPro    _nameText;
        [SerializeField] TextMeshPro    _descText;
        [SerializeField] TextMeshPro    _hintText;
        [SerializeField] SpriteRenderer _imageRenderer;

        public InventionData Data => _data;
        public IReadOnlyList<InventionActiveEffect> Effects => _effects;

        public override string DisplayName => _data?.inventionName ?? base.DisplayName;

        /// <summary>点击发明卡时触发（由外部注入以展示效果选择面板）</summary>
        public System.Action<InventionCard3D> OnEffectMenuRequested;
        public System.Action<InventionCard3D> OnUnlockRequested;

        protected override CardCategory GetDefaultCategory() => CardCategory.Invention;

        // ─── 初始化 ────────────────────────────────────────────────────────

        public void Init(InventionData data, Vector3 localPos = default)
        {
            _data           = data;
            _effects        = data.activeEffects ?? new List<InventionActiveEffect>();
            gameObject.name = $"Inv_{data.inventionName}";
            InitView(localPos);
        }

        // ─── 工厂 ─────────────────────────────────────────────────────────

        public static InventionCard3D Create(InventionData data, Transform parent, Vector3 localPos = default)
        {
            var go   = new GameObject($"Inv_{data.inventionName}");
            go.transform.SetParent(parent, false);
            var card = go.AddComponent<InventionCard3D>();
            card.Init(data, localPos);
            return card;
        }

        // ─── CardView3D ────────────────────────────────────────────────────

        protected override void BuildTextFields()
        {
            if (_nameText != null) return; // prefab 已配置，跳过

            float ty = CD * 0.5f + 0.003f;

            _nameText = MakeText("Name",
                new Vector3(0f, ty, 0.34f), CardPresentationConsts.CompactTitleFontSize,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.06f, 0.34f));

            _descText = MakeText("Desc",
                new Vector3(0f, ty, -0.03f), 0.068f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.06f, 0.30f));

            _hintText = MakeText("Hint",
                new Vector3(0f, ty, -0.365f), 0.065f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.06f, 0.24f));

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
            if (_bodyRenderer == null || _data == null) return;

            _bodyRenderer.material.color = ResolveBodyColor();

            if (_imageRenderer != null) _imageRenderer.sprite = _data.icon;

            if (_nameText == null) return;

            _nameText.text  = _data.inventionName;
            _nameText.color = new Color(0.88f, 0.92f, 0.96f);

            _descText.text  = _data.description ?? "";
            _descText.color = new Color(0.86f, 0.89f, 0.94f);

            bool hasEffects = _effects != null && _effects.Count > 0;
            _hintText.text = isReadOnlyPreview ? "只读 · 悬停查看" : isUnlocked ? (hasEffects ? "点击使用" : "已掌握 · 悬停查看") : (canUnlock ? "点击发明 · 悬停查看" : $"{availabilityReason} · 悬停查看");
            _hintText.color = new Color(0.88f, 0.90f, 0.94f);
        }

        protected override void OnClickReleased()
        {
            if (isReadOnlyPreview) return;
            if (!isUnlocked)
            {
                OnUnlockRequested?.Invoke(this);
                return;
            }
            if (_effects != null && _effects.Count > 0)
                OnEffectMenuRequested?.Invoke(this);
            else
                base.OnClickReleased();
        }

        public void ConfigureState(bool unlocked, bool unlockable, string reason)
        {
            isUnlocked = unlocked;
            canUnlock = unlockable;
            isReadOnlyPreview = false;
            availabilityReason = reason ?? string.Empty;
            ApplyVisuals();
        }

        public void ConfigurePreview()
        {
            isUnlocked = false;
            canUnlock = true;
            isReadOnlyPreview = true;
            availabilityReason = "只读预览";
            ApplyVisuals();
        }

        public void Refresh() => ApplyVisuals();

        public void ConfigureInspectionContent(CardInspectionContent content)
        {
            inspectionContent = content;
            hasInspectionContent = true;
        }

        public override bool TryGetInspectionContent(out CardInspectionContent content)
        {
            if (hasInspectionContent)
            {
                content = inspectionContent;
                return true;
            }
            return base.TryGetInspectionContent(out content);
        }

        private Color ResolveBodyColor()
        {
            if (isUnlocked) return IsHovered ? ColHover : ColBody;
            if (canUnlock) return IsHovered ? ColAvailableHover : ColAvailable;
            return ColLocked;
        }
    }
}
