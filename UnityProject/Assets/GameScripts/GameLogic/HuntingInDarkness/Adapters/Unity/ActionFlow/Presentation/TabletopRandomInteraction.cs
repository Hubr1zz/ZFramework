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
        OldMaid
    }

    /// <summary>数据层发给表现层的随机交互请求，不持有场景 Transform。</summary>
    public readonly struct TabletopRandomInteractionRequest
    {
        public TabletopRandomInteractionRequest(string interactionId, TabletopRandomInteractionKind kind, string actorId, string targetId, int count = 1, int sides = 6, string deckId = null, string instruction = null)
        {
            InteractionId = string.IsNullOrWhiteSpace(interactionId) ? Guid.NewGuid().ToString("N") : interactionId;
            Kind = kind;
            ActorId = actorId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Count = Math.Max(1, count);
            Sides = Math.Max(2, sides);
            DeckId = deckId ?? string.Empty;
            Instruction = instruction ?? string.Empty;
        }

        public string InteractionId { get; }
        public TabletopRandomInteractionKind Kind { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public int Count { get; }
        public int Sides { get; }
        public string DeckId { get; }
        public string Instruction { get; }
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
}
