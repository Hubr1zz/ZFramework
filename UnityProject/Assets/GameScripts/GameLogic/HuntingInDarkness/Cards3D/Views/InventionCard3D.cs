using System.Collections.Generic;
using HuntingInDarkness.Data;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    public class InventionCard3D : CardView3D
    {
        static readonly Color ColBody = new(0.20f, 0.28f, 0.36f);
        static readonly Color ColHover = new(0.32f, 0.44f, 0.58f);
        static readonly Color ColAvailable = new(0.34f, 0.30f, 0.12f);
        static readonly Color ColAvailableHover = new(0.48f, 0.42f, 0.18f);
        static readonly Color ColLocked = new(0.13f, 0.14f, 0.17f);

        InventionData              _data;
        List<InventionActiveEffect> _effects = new();
        bool isUnlocked;
        bool canUnlock;
        string availabilityReason = string.Empty;

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
                new Vector3(0f, ty, CH * 0.38f), 0.095f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.06f, 0.20f));

            _descText = MakeText("Desc",
                new Vector3(0f, ty, CH * 0.00f), 0.068f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.06f, 0.44f));

            _hintText = MakeText("Hint",
                new Vector3(0f, ty, -CH * 0.40f), 0.065f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.06f, 0.12f));

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
            _descText.color = new Color(0.65f, 0.70f, 0.78f);

            bool hasEffects = _effects != null && _effects.Count > 0;
            _hintText.text = isUnlocked ? (hasEffects ? "已掌握 · 点击使用" : "已掌握") : (canUnlock ? "点击发明" : availabilityReason);
            _hintText.color = new Color(0.70f, 0.82f, 0.95f);
        }

        protected override void OnMouseDown()
        {
            if (!isUnlocked)
            {
                OnUnlockRequested?.Invoke(this);
                return;
            }
            if (_effects != null && _effects.Count > 0)
                OnEffectMenuRequested?.Invoke(this);
            else
                base.OnMouseDown();
        }

        public void ConfigureState(bool unlocked, bool unlockable, string reason)
        {
            isUnlocked = unlocked;
            canUnlock = unlockable;
            availabilityReason = reason ?? string.Empty;
            ApplyVisuals();
        }

        public void Refresh() => ApplyVisuals();

        private Color ResolveBodyColor()
        {
            if (isUnlocked) return IsHovered ? ColHover : ColBody;
            if (canUnlock) return IsHovered ? ColAvailableHover : ColAvailable;
            return ColLocked;
        }
    }
}
