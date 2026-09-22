using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace HuntingInDarkness.ActionFlow.Presentation
{
    /// <summary>需要玩家在桌面上完成并等待结果的随机交互形式。</summary>
    public enum TabletopRandomInteractionKind
    {
        PhysicalDice,
        DrawCards,
        FlipCards,
        OldMaid,
        DeathDeck
    }

    /// <summary>数据层发给表现层的随机交互请求，不持有场景 Transform。</summary>
    public readonly struct TabletopRandomInteractionRequest
    {
        public TabletopRandomInteractionRequest(string interactionId, TabletopRandomInteractionKind kind, string actorId, string targetId, int count = 1, int sides = 6, string deckId = null, string instruction = null, IReadOnlyList<string> cardFaceLabels = null)
        {
            InteractionId = string.IsNullOrWhiteSpace(interactionId) ? Guid.NewGuid().ToString("N") : interactionId;
            Kind = kind;
            ActorId = actorId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Count = Math.Max(1, count);
            Sides = kind == TabletopRandomInteractionKind.DeathDeck ? Math.Max(1, sides) : Math.Max(2, sides);
            DeckId = deckId ?? string.Empty;
            Instruction = instruction ?? string.Empty;
            if (cardFaceLabels == null || cardFaceLabels.Count == 0)
                CardFaceLabels = Array.Empty<string>();
            else
            {
                var labels = new string[cardFaceLabels.Count];
                for (int index = 0; index < labels.Length; index++)
                    labels[index] = cardFaceLabels[index] ?? string.Empty;
                CardFaceLabels = Array.AsReadOnly(labels);
            }
        }

        public string InteractionId { get; }
        public TabletopRandomInteractionKind Kind { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public int Count { get; }
        public int Sides { get; }
        public string DeckId { get; }
        public string Instruction { get; }
        public IReadOnlyList<string> CardFaceLabels { get; }
    }

    /// <summary>物理骰子稳定或卡牌操作完成后回传给 ActionQueue 的权威结果。</summary>
    public readonly struct TabletopRandomInteractionResult
    {
        public TabletopRandomInteractionResult(string interactionId, IReadOnlyList<int> values, IReadOnlyList<string> cardIds, bool cancelled = false)
        {
            InteractionId = interactionId ?? string.Empty;
            Values = values ?? Array.Empty<int>();
            CardIds = cardIds ?? Array.Empty<string>();
            Cancelled = cancelled;
        }

        public string InteractionId { get; }
        public IReadOnlyList<int> Values { get; }
        public IReadOnlyList<string> CardIds { get; }
        public bool Cancelled { get; }
    }

    /// <summary>Action 执行环境等待的桌面随机表现端口；骰子、抽牌、翻牌与抽鬼牌共享生命周期。</summary>
    public interface ITabletopRandomInteractionPresenter
    {
        UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken);
    }

    public static class TabletopRandomInteractionResultValidator
    {
        public static bool TryGetSelectedPosition(TabletopRandomInteractionRequest request, TabletopRandomInteractionResult result, out int position)
        {
            position = -1;
            if (request.Kind != TabletopRandomInteractionKind.DeathDeck || result.Cancelled || !string.Equals(request.InteractionId, result.InteractionId, StringComparison.Ordinal) || result.CardIds == null || result.CardIds.Count != 1) return false;
            string prefix = $"{request.DeckId.Trim()}:position-";
            string cardId = result.CardIds[0];
            if (string.IsNullOrWhiteSpace(request.DeckId) || string.IsNullOrWhiteSpace(cardId) || !cardId.StartsWith(prefix, StringComparison.Ordinal)) return false;
            if (!int.TryParse(cardId.Substring(prefix.Length), out position) || position < 0 || position >= request.Sides) return false;
            return true;
        }

        public static bool TryGetCheckTotal(TabletopRandomInteractionRequest request, TabletopRandomInteractionResult result, out int total)
        {
            if (request.Kind == TabletopRandomInteractionKind.PhysicalDice)
                return TryGetDiceTotal(request, result, out total);

            total = 0;
            if (result.Cancelled || !string.Equals(request.InteractionId, result.InteractionId, StringComparison.Ordinal)) return false;
            if (string.IsNullOrWhiteSpace(request.DeckId)) return false;
            if (result.Values == null || result.CardIds == null || result.Values.Count != request.Count || result.CardIds.Count != request.Count) return false;
            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            string cardIdPrefix = $"{request.DeckId.Trim()}:";
            long resolvedTotal = 0;
            for (int index = 0; index < result.Values.Count; index++)
            {
                int value = result.Values[index];
                string cardId = result.CardIds[index];
                if (value < 1 || value > request.Sides || string.IsNullOrWhiteSpace(cardId) || !cardId.StartsWith(cardIdPrefix, StringComparison.Ordinal) || !cardIds.Add(cardId)) return false;
                resolvedTotal += value;
                if (resolvedTotal > int.MaxValue) return false;
            }
            total = (int)resolvedTotal;
            return true;
        }

        public static bool TryGetDiceTotal(TabletopRandomInteractionRequest request, TabletopRandomInteractionResult result, out int total)
        {
            total = 0;
            if (request.Kind != TabletopRandomInteractionKind.PhysicalDice || result.Cancelled) return false;
            if (!string.Equals(request.InteractionId, result.InteractionId, StringComparison.Ordinal)) return false;
            if (result.Values == null || result.Values.Count != request.Count) return false;
            long resolvedTotal = 0;
            foreach (int value in result.Values)
            {
                if (value < 1 || value > request.Sides) return false;
                resolvedTotal += value;
                if (resolvedTotal > int.MaxValue) return false;
            }
            total = (int)resolvedTotal;
            return true;
        }
    }

    /// <summary>按交互种类分发到专用 3D presenter，并阻止跨阶段随机表现重叠。</summary>
    public sealed class TabletopRandomInteractionRouter : ITabletopRandomInteractionPresenter
    {
        private readonly ITabletopRandomInteractionPresenter dicePresenter;
        private readonly ITabletopRandomInteractionPresenter cardPresenter;
        private bool isPresenting;

        public TabletopRandomInteractionRouter(ITabletopRandomInteractionPresenter dicePresenter, ITabletopRandomInteractionPresenter cardPresenter)
        {
            this.dicePresenter = dicePresenter;
            this.cardPresenter = cardPresenter;
        }

        public async UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken)
        {
            while (isPresenting)
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            ITabletopRandomInteractionPresenter presenter = request.Kind == TabletopRandomInteractionKind.PhysicalDice ? dicePresenter : cardPresenter;
            if (presenter == null) throw new NotSupportedException($"当前没有可处理 {request.Kind} 的桌面表现器。");

            isPresenting = true;
            try
            {
                return await presenter.PresentAsync(request, cancellationToken);
            }
            finally
            {
                isPresenting = false;
            }
        }
    }
}
