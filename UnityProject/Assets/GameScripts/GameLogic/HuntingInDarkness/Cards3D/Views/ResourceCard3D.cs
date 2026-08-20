using Cards3D;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>
    /// 营地资源卡。显示资源名称和当前数量。
    /// 右键翻面（正面↔背面）；支持拖拽吸附到 CardSlot。
    /// </summary>
    public class ResourceCard3D : SlotDraggableCardView3D
    {
        // ─── 颜色 ─────────────────────────────────────────────────────────
        static readonly Color ColFaceDown = new(0.16f, 0.20f, 0.18f);

        static Color GetResourceColor(string name) => name?.ToLowerInvariant() switch
        {
            "bone"                    => new Color(0.78f, 0.72f, 0.58f),
            "hide"                    => new Color(0.55f, 0.40f, 0.28f),
            "stone"                   => new Color(0.50f, 0.50f, 0.52f),
            "wood"   or "lumber"      => new Color(0.52f, 0.38f, 0.22f),
            "herb"   or "plant"       => new Color(0.28f, 0.50f, 0.28f),
            "ore"    or "iron"
                     or "mineral"     => new Color(0.40f, 0.42f, 0.50f),
            "organ"  or "flesh"       => new Color(0.55f, 0.28f, 0.28f),
            _                         => new Color(0.60f, 0.52f, 0.40f),
        };
        static Color GetHoverColor(string name)
        {
            var c = GetResourceColor(name);
            return new Color(Mathf.Min(1f, c.r + 0.18f),
                             Mathf.Min(1f, c.g + 0.18f),
                             Mathf.Min(1f, c.b + 0.18f));
        }

        // ─── 状态 ─────────────────────────────────────────────────────────
        string _resourceName;
        int    _count;
        bool   _isFaceUp  = true;
        bool   _isFlipping;

        [SerializeField] TextMeshPro _nameText;
        [SerializeField] TextMeshPro _countText;
        [SerializeField] TextMeshPro _labelText;

        public string ResourceName => _resourceName;
        public bool   IsFaceUp     => _isFaceUp;

        public override string DisplayName => $"{_resourceName} ×{_count}";

        // 同名资源可互相合并成动态卡堆
        public override string StackKey => _resourceName;

        protected override CardCategory GetDefaultCategory() => CardCategory.Resource;
        protected override bool         CanHover()           => !_isFlipping;

        // ─── 初始化 ────────────────────────────────────────────────────────

        public void Init(string resourceName, int count, Vector3 localPos = default)
        {
            _resourceName   = resourceName;
            _count          = count;
            gameObject.name = $"Res_{resourceName}";
            InitView(localPos);
        }

        // ─── 工厂（程序化备用）────────────────────────────────────────────

        public static ResourceCard3D Create(
            string resourceName, int count, Transform parent, Vector3 localPos = default)
        {
            var go   = new GameObject($"Res_{resourceName}");
            go.transform.SetParent(parent, false);
            var card = go.AddComponent<ResourceCard3D>();
            card.Init(resourceName, count, localPos);
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
                new Vector2(CW - 0.06f, 0.18f));

            _countText = MakeText("Count",
                new Vector3(0f, ty, CH * 0.00f), 0.16f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.08f, 0.30f));

            _labelText = MakeText("Label",
                new Vector3(0f, ty, -CH * 0.35f), 0.07f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.06f, 0.12f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null) return;

            if (_isFaceUp)
            {
                var col = GetResourceColor(_resourceName);
                _bodyRenderer.material.color = IsHovered ? GetHoverColor(_resourceName) : col;

                if (_nameText == null) return;
                _nameText.color  = new Color(0.10f, 0.08f, 0.06f);
                _countText.color = new Color(0.08f, 0.08f, 0.08f);
                _labelText.color = new Color(0.30f, 0.28f, 0.24f);
                _nameText.text   = _resourceName ?? "";
                _countText.text  = $"×{_count}";
                _labelText.text  = "存量";
            }
            else
            {
                _bodyRenderer.material.color = ColFaceDown;

                if (_nameText == null) return;
                _nameText.text  = "";
                _countText.text = "";
                _labelText.text = "";
            }
        }

        // ─── 翻面（右键触发）──────────────────────────────────────────────

        private void OnMouseOver()
        {
            if (Input.GetMouseButtonDown(1) && !IsDraggingCard)
                Flip();
        }

        public void Flip()
        {
            if (_isFlipping || IsDraggingCard) return;
            FlipAsync().Forget();
        }

        private async UniTaskVoid FlipAsync()
        {
            _isFlipping = true;
            try
            {
                const float half = 0.15f;
                var cancellationToken = this.GetCancellationTokenOnDestroy();

                for (float t = 0; t < half; t += Time.deltaTime)
                {
                    transform.localEulerAngles = new Vector3(Mathf.Lerp(0f, 90f, t / half), 0f, 0f);
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
                _isFaceUp = !_isFaceUp;
                ApplyVisuals();

                for (float t = 0; t < half; t += Time.deltaTime)
                {
                    transform.localEulerAngles = new Vector3(Mathf.Lerp(-90f, 0f, t / half), 0f, 0f);
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
                transform.localEulerAngles = Vector3.zero;
            }
            finally
            {
                _isFlipping = false;
            }
        }

        // ─── 未入槽落点（同种资源合并）─────────────────────────────────

        protected override bool TryHandleUnslottedDrop()
        {
            var mate = FindMergeTarget();
            if (mate == null) return false;

            if (mate.CurrentSlot != null) mate.CurrentSlot.ClearCard();
            CardSlot slot = CardSlot.CreateDynamicStack(mate);
            slot.PushCard(this);
            return true;
        }

        /// <summary>
        /// 查找附近可合并的同种卡：松散卡，或处在"非堆叠槽"里的卡（可被取出合并）。
        /// 已在堆叠卡堆里的卡不作为合并目标（应直接拖到该卡堆上由槽接收）。
        /// </summary>
        private CardView3D FindMergeTarget()
        {
            if (StackKey == null) return null;
            CardView3D best = null;
            float bestDist = 0.6f; // 合并判定半径（XZ 世界单位）
            foreach (var c in AllCards)
            {
                if (c == this || c == null) continue;
                if (c.StackKey != StackKey) continue;                 // 同种
                if (c.CurrentSlot != null && c.CurrentSlot.Stackable) continue; // 已在卡堆里
                float d = new Vector2(
                    transform.position.x - c.transform.position.x,
                    transform.position.z - c.transform.position.z).magnitude;
                if (d < bestDist) { best = c; bestDist = d; }
            }
            return best;
        }

        // ─── 公共 API ─────────────────────────────────────────────────────

        public void UpdateCount(int newCount)
        {
            _count = newCount;
            if (_countText != null && _isFaceUp) _countText.text = $"×{_count}";
        }
    }
}
