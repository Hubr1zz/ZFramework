using System;
using System.Collections.Generic;
using System.Threading;
using Cards3D;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Presentation;
using TMPro;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>把抽牌、翻牌与简化抽鬼牌请求表现为可点击的 3D 桌面牌组。</summary>
    public sealed class TabletopCardInteractionPresenter : MonoBehaviour, ITabletopRandomInteractionPresenter
    {
        private sealed class CardOption
        {
            public string Id;
            public int Value;
            public bool IsOldMaid;
            public GameObject GameObject;
            public Collider Collider;
            public Renderer Renderer;
            public TextMeshPro Label;
        }

        [SerializeField, Min(0.25f)] private float cardWidth = 0.62f;
        [SerializeField, Min(0.35f)] private float cardHeight = 0.92f;
        [SerializeField, Min(0.01f)] private float cardThickness = 0.045f;
        [SerializeField, Min(2)] private int maxDeckSize = 20;
        [SerializeField, Min(1)] private int maxSelectionCount = 12;
        [SerializeField, Min(0f)] private float revealDuration = 0.35f;
        [SerializeField, Min(0f)] private float resultDisplayDuration = 1.0f;
        [SerializeField] private Material cardBackMaterialTemplate;
        [SerializeField] private Material cardFrontMaterialTemplate;
        [SerializeField] private Material tableMaterialTemplate;
        [SerializeField] private TMP_FontAsset cardFont;

        private UniTaskCompletionSource<int> selectionSource;
        private IReadOnlyList<CardOption> activeCards = Array.Empty<CardOption>();
        private bool isPresenting;

        public Func<TabletopRandomInteractionRequest, Vector3> AnchorResolver { private get; set; }
        public bool IsPresenting => isPresenting;
        public TabletopRandomInteractionResult LastCompletedResult { get; private set; }

        public async UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken)
        {
            ValidateRequest(request);
            while (isPresenting)
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

            isPresenting = true;
            GameObject interactionRoot = null;
            Material cardBackMaterial = null;
            Material cardFrontMaterial = null;
            Material tableMaterial = null;
            TabletopBackgroundInputBlocker backgroundInputBlocker = null;
            try
            {
                Vector3 anchor = AnchorResolver != null ? AnchorResolver.Invoke(request) : transform.position;
                backgroundInputBlocker = TabletopBackgroundInputBlocker.Capture();
                interactionRoot = new GameObject($"TabletopCards_{request.InteractionId}");
                interactionRoot.transform.position = anchor + Vector3.up * 0.08f;
                cardBackMaterial = cardBackMaterialTemplate != null ? cardBackMaterialTemplate : CreateMaterial(new Color(0.16f, 0.08f, 0.12f));
                cardFrontMaterial = cardFrontMaterialTemplate != null ? cardFrontMaterialTemplate : CreateMaterial(new Color(0.72f, 0.62f, 0.42f));
                tableMaterial = tableMaterialTemplate != null ? tableMaterialTemplate : CreateMaterial(new Color(0.10f, 0.07f, 0.055f));
                BuildTable(interactionRoot.transform, tableMaterial);
                BuildInstruction(interactionRoot.transform, request.Instruction);
                List<CardOption> cards = BuildCards(request, interactionRoot.transform, cardBackMaterial);
                activeCards = cards;

                var selected = new HashSet<int>();
                var values = new List<int>(request.Count);
                var cardIds = new List<string>(request.Count);
                for (int selectionIndex = 0; selectionIndex < request.Count; selectionIndex++)
                {
                    ConfigureSelectableCards(request.Kind, cards, selected);
                    selectionSource = new UniTaskCompletionSource<int>();
                    int cardIndex = await selectionSource.Task.AttachExternalCancellation(cancellationToken);
                    selectionSource = null;
                    selected.Add(cardIndex);
                    CardOption card = cards[cardIndex];
                    values.Add(card.Value);
                    cardIds.Add(card.Id);
                    RevealCard(card, cardFrontMaterial, request.Kind, selectionIndex, request.Count);
                    if (revealDuration > 0f)
                        await UniTask.Delay(TimeSpan.FromSeconds(revealDuration), cancellationToken: cancellationToken);
                }

                DisableAll(cards);
                BuildResultLabel(interactionRoot.transform, request.Kind, values, cardIds);
                if (resultDisplayDuration > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(resultDisplayDuration), cancellationToken: cancellationToken);
                LastCompletedResult = new TabletopRandomInteractionResult(request.InteractionId, values, cardIds);
                return LastCompletedResult;
            }
            finally
            {
                selectionSource = null;
                activeCards = Array.Empty<CardOption>();
                if (interactionRoot != null) Destroy(interactionRoot);
                if (cardBackMaterial != null && cardBackMaterial != cardBackMaterialTemplate) Destroy(cardBackMaterial);
                if (cardFrontMaterial != null && cardFrontMaterial != cardFrontMaterialTemplate) Destroy(cardFrontMaterial);
                if (tableMaterial != null && tableMaterial != tableMaterialTemplate) Destroy(tableMaterial);
                backgroundInputBlocker?.Dispose();
                isPresenting = false;
            }
        }

        private void ValidateRequest(TabletopRandomInteractionRequest request)
        {
            if (request.Kind == TabletopRandomInteractionKind.PhysicalDice) throw new NotSupportedException("卡牌表现器不能处理物理骰子请求。");
            if (request.Sides > maxDeckSize) throw new InvalidOperationException($"桌面牌组不能超过 {maxDeckSize} 张，收到 {request.Sides}。");
            if (request.Count > maxSelectionCount || request.Count > request.Sides) throw new InvalidOperationException($"本次需要选择的卡牌数量无效：{request.Count}。");
            if (request.Kind == TabletopRandomInteractionKind.OldMaid && request.Count != 1) throw new InvalidOperationException("抽鬼牌交互当前每次只允许抽取一张牌。");
            if (string.IsNullOrWhiteSpace(request.DeckId)) throw new InvalidOperationException("卡牌随机交互缺少稳定牌组 ID。");
        }

        private List<CardOption> BuildCards(TabletopRandomInteractionRequest request, Transform parent, Material backMaterial)
        {
            List<CardOption> cards = CreateDeck(request);
            Shuffle(cards);
            for (int index = 0; index < cards.Count; index++)
            {
                int cardIndex = index;
                CardOption card = cards[index];
                GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gameObject.name = $"Card_{card.Id}";
                gameObject.transform.SetParent(parent, false);
                gameObject.transform.localScale = new Vector3(cardWidth, cardThickness, cardHeight);
                gameObject.transform.localPosition = ResolveCardPosition(request.Kind, index, cards.Count);
                card.GameObject = gameObject;
                card.Collider = gameObject.GetComponent<Collider>();
                card.Renderer = gameObject.GetComponent<Renderer>();
                card.Renderer.sharedMaterial = backMaterial;
                gameObject.AddComponent<ClickProxy>().OnClick = () => SelectCard(cardIndex);
                card.Label = BuildCardLabel(gameObject.transform);
            }
            return cards;
        }

        private static List<CardOption> CreateDeck(TabletopRandomInteractionRequest request)
        {
            var cards = new List<CardOption>(request.Sides);
            string deckId = request.DeckId.Trim();
            if (request.Kind == TabletopRandomInteractionKind.OldMaid)
            {
                cards.Add(new CardOption { Id = $"{deckId}:old-maid", Value = 1, IsOldMaid = true });
                for (int index = 1; index < request.Sides; index++)
                    cards.Add(new CardOption { Id = $"{deckId}:safe-{index}", Value = request.Sides });
                return cards;
            }

            for (int value = 1; value <= request.Sides; value++)
                cards.Add(new CardOption { Id = $"{deckId}:card-{value}", Value = value });
            return cards;
        }

        private static void Shuffle(IList<CardOption> cards)
        {
            for (int index = cards.Count - 1; index > 0; index--)
            {
                int swapIndex = UnityEngine.Random.Range(0, index + 1);
                (cards[index], cards[swapIndex]) = (cards[swapIndex], cards[index]);
            }
        }

        private Vector3 ResolveCardPosition(TabletopRandomInteractionKind kind, int index, int count)
        {
            if (kind == TabletopRandomInteractionKind.DrawCards)
                return new Vector3(0f, index * cardThickness * 0.55f, 0.05f - index * 0.002f);
            int columns = Mathf.Min(count, 10);
            int row = index / columns;
            int column = index % columns;
            float spacing = cardWidth * 0.82f;
            float rowOffset = -(Mathf.Min(columns, count - row * columns) - 1) * spacing * 0.5f;
            return new Vector3(rowOffset + column * spacing, row * cardThickness * 0.25f, 0.15f - row * cardHeight * 0.78f);
        }

        private TextMeshPro BuildCardLabel(Transform parent)
        {
            var labelObject = new GameObject("FaceLabel");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = new Vector3(1f / cardWidth, 1f / cardHeight, 1f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = "?";
            if (cardFont != null) label.font = cardFont;
            label.fontSize = 0.18f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.92f, 0.82f, 0.62f);
            label.rectTransform.sizeDelta = new Vector2(cardWidth * 0.72f, cardHeight * 0.62f);
            return label;
        }

        private void ConfigureSelectableCards(TabletopRandomInteractionKind kind, IReadOnlyList<CardOption> cards, ISet<int> selected)
        {
            DisableAll(cards);
            if (kind == TabletopRandomInteractionKind.DrawCards)
            {
                for (int index = cards.Count - 1; index >= 0; index--)
                    if (!selected.Contains(index))
                    {
                        cards[index].Collider.enabled = true;
                        return;
                    }
                return;
            }
            for (int index = 0; index < cards.Count; index++)
                cards[index].Collider.enabled = !selected.Contains(index);
        }

        private void SelectCard(int index)
        {
            if (selectionSource == null || index < 0 || index >= activeCards.Count || !activeCards[index].Collider.enabled) return;
            selectionSource.TrySetResult(index);
        }

        private void RevealCard(CardOption card, Material frontMaterial, TabletopRandomInteractionKind kind, int selectionIndex, int selectionCount)
        {
            card.Collider.enabled = false;
            card.Renderer.sharedMaterial = frontMaterial;
            card.Label.text = kind == TabletopRandomInteractionKind.OldMaid ? card.IsOldMaid ? "鬼牌" : "安全" : card.Value.ToString();
            float spacing = cardWidth * 1.05f;
            card.GameObject.transform.localPosition = new Vector3(-(selectionCount - 1) * spacing * 0.5f + selectionIndex * spacing, 0.10f, -1.05f);
        }

        private static void DisableAll(IReadOnlyList<CardOption> cards)
        {
            foreach (CardOption card in cards)
                card.Collider.enabled = false;
        }

        private void BuildTable(Transform parent, Material material)
        {
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "CardInteractionMat";
            table.transform.SetParent(parent, false);
            table.transform.localPosition = new Vector3(0f, -0.08f, -0.28f);
            table.transform.localScale = new Vector3(Mathf.Max(3.0f, maxDeckSize * cardWidth * 0.44f), 0.10f, 2.7f);
            table.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(table.GetComponent<Collider>());
        }

        private void BuildInstruction(Transform parent, string instruction)
        {
            var labelObject = new GameObject("Instruction");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.04f, 1.25f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = string.IsNullOrWhiteSpace(instruction) ? "选择一张牌" : instruction;
            if (cardFont != null) label.font = cardFont;
            label.fontSize = 0.16f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.96f, 0.84f, 0.48f);
            label.rectTransform.sizeDelta = new Vector2(4.2f, 0.30f);
        }

        private void BuildResultLabel(Transform parent, TabletopRandomInteractionKind kind, IReadOnlyList<int> values, IReadOnlyList<string> cardIds)
        {
            int total = 0;
            foreach (int value in values)
                total += value;
            bool drewOldMaid = false;
            foreach (string cardId in cardIds)
                if (cardId.EndsWith(":old-maid", StringComparison.Ordinal))
                {
                    drewOldMaid = true;
                    break;
                }
            var labelObject = new GameObject("CardResult");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.05f, -1.52f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = kind == TabletopRandomInteractionKind.OldMaid ? drewOldMaid ? "抽中了鬼牌" : "避开了鬼牌" : values.Count == 1 ? $"牌面  {total}" : $"牌面  {string.Join(" + ", values)} = {total}";
            if (cardFont != null) label.font = cardFont;
            label.fontSize = 0.15f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.98f, 0.86f, 0.42f);
            label.rectTransform.sizeDelta = new Vector2(3.4f, 0.30f);
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("当前渲染管线未提供 Standard Shader，请为桌面卡牌配置材质模板。");
            var material = new Material(shader) { color = color };
            material.SetFloat("_Glossiness", 0.12f);
            return material;
        }
    }
}
