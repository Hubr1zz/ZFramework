using HuntingInDarkness.Data;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>
    /// 猎人卡。显示猎人姓名、意志点和关键属性。
    /// 活着：蓝灰色；死亡：暗红。点击调用 OnHunterClicked（由 SettlementTable3D 注入）。
    /// </summary>
    public class HunterCard3D : CardView3D
    {
        static readonly Color ColAlive      = new(0.22f, 0.30f, 0.42f);
        static readonly Color ColDead       = new(0.28f, 0.10f, 0.10f);
        static readonly Color ColHoverAlive = new(0.32f, 0.44f, 0.58f);

        HunterInstance _hunter;

        [SerializeField] TextMeshPro _nameText;
        [SerializeField] TextMeshPro _wpText;
        [SerializeField] TextMeshPro _statsText;
        [SerializeField] TextMeshPro _stateText;

        public HunterInstance Hunter => _hunter;
        public System.Action<HunterCard3D> OnHunterClicked;

        public override string DisplayName => _hunter?.Name ?? base.DisplayName;

        protected override CardCategory GetDefaultCategory() => CardCategory.HunterProfile;

        // ─── 初始化 ────────────────────────────────────────────────────────

        public void Init(HunterInstance hunter, Vector3 localPos = default)
        {
            _hunter         = hunter;
            gameObject.name = $"Hunter_{hunter.Name}";
            InitView(localPos);
        }

        // ─── 工厂（程序化备用）────────────────────────────────────────────

        public static HunterCard3D Create(HunterInstance hunter, Transform parent, Vector3 localPos = default)
        {
            var go   = new GameObject($"Hunter_{hunter.Name}");
            go.transform.SetParent(parent, false);
            var card = go.AddComponent<HunterCard3D>();
            card.Init(hunter, localPos);
            return card;
        }

        // ─── CardView3D ────────────────────────────────────────────────────

        protected override void BuildTextFields()
        {
            if (_nameText != null) return; // prefab 已配置，跳过

            float ty = CD * 0.5f + 0.003f;

            _nameText = MakeText("Name",
                new Vector3(0f, ty, CH * 0.36f), 0.10f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.06f, 0.20f));

            _wpText = MakeText("WP",
                new Vector3(0f, ty, CH * 0.12f), 0.090f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.06f, 0.16f));

            _statsText = MakeText("Stats",
                new Vector3(0f, ty, -CH * 0.10f), 0.070f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.06f, 0.36f));

            _stateText = MakeText("State",
                new Vector3(0f, ty, -CH * 0.38f), 0.075f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.06f, 0.14f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null || _hunter == null) return;

            bool alive = _hunter.IsAlive;
            _bodyRenderer.material.color = !alive ? ColDead
                : IsHovered ? ColHoverAlive : ColAlive;

            if (_nameText == null) return;

            _nameText.text  = _hunter.Name;
            _nameText.color = alive ? new Color(0.85f, 0.90f, 0.95f) : new Color(0.65f, 0.45f, 0.45f);

            if (alive)
            {
                _wpText.text    = $"意志  {_hunter.Willpower}/{_hunter.WillpowerMax}";
                _wpText.color   = _hunter.Willpower > 0
                    ? new Color(0.70f, 0.82f, 0.95f)
                    : new Color(0.85f, 0.40f, 0.35f);

                var s = _hunter.Stats;
                _statsText.text  = $"力{s.strength}  敏{s.evasion}  速{s.speed}";
                _statsText.color = new Color(0.60f, 0.68f, 0.78f);

                _stateText.text  = string.IsNullOrWhiteSpace(_hunter.BloodlineName) ? "点击查看" : $"{_hunter.BloodlineName} · 查看";
                _stateText.color = new Color(0.50f, 0.60f, 0.72f);
            }
            else
            {
                _wpText.text    = "† 已阵亡";
                _wpText.color   = new Color(0.65f, 0.30f, 0.30f);
                _statsText.text = "";
                _stateText.text = "";
            }
        }

        protected override void OnMouseDown()
        {
            OnHunterClicked?.Invoke(this);
            base.OnMouseDown();
        }

        public void Refresh(HunterInstance hunter)
        {
            _hunter = hunter;
            ApplyVisuals();
        }
    }
}
