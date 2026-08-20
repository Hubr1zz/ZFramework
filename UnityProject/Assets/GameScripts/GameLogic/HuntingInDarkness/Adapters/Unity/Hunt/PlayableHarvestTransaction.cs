using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunt;

namespace HuntingInDarkness.Hunt
{
    public readonly struct PlayableHarvestStepResult
    {
        private PlayableHarvestStepResult(bool succeeded, string reason, bool hasRevealedCard, HarvestCardResult revealedCard, bool isCompleted, IReadOnlyList<ItemInstance> obtained)
        {
            Succeeded = succeeded;
            Reason = reason;
            HasRevealedCard = hasRevealedCard;
            RevealedCard = revealedCard;
            IsCompleted = isCompleted;
            Obtained = obtained ?? Array.Empty<ItemInstance>();
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public bool HasRevealedCard { get; }
        public HarvestCardResult RevealedCard { get; }
        public bool IsCompleted { get; }
        public IReadOnlyList<ItemInstance> Obtained { get; }
        public static PlayableHarvestStepResult Revealed(HarvestCardResult card) => new(true, string.Empty, true, card, false, null);
        public static PlayableHarvestStepResult Completed(HarvestCardResult? card, IReadOnlyList<ItemInstance> obtained) => new(true, string.Empty, card.HasValue, card.GetValueOrDefault(), true, obtained);
        public static PlayableHarvestStepResult Failed(string reason, HarvestCardResult? card = null) => new(false, reason, card.HasValue, card.GetValueOrDefault(), false, null);
    }

    /// <summary>把逐卡揭示与最终资源写入绑定为一次不可重复的采集事务。</summary>
    public sealed class PlayableHarvestTransaction
    {
        private readonly ResourceSystem owner;
        private readonly ResourcePointInstance point;
        private readonly HunterInstance hunter;
        private readonly HarvestDrawPlan plan;
        private readonly Action release;
        private readonly List<ItemInstance> obtained = new();
        private readonly IReadOnlyList<ItemInstance> visibleObtained;
        private int revealedCount;
        private bool isReleased;

        public int CardCount => plan.CardCount;
        public double HitChance => plan.HitChance;
        public int RevealedCount => revealedCount;
        public int RevealedHitCount { get; private set; }
        public ResourcePointInstance Point => point;
        public string ResourceName => point.ResourceName;
        public int HunterId => hunter?.InstanceId ?? -1;
        public string HunterName => hunter?.Name ?? "?";
        public bool CanReveal => !IsCommitted && !IsCancelled && revealedCount < plan.CardCount;
        public bool IsComplete => revealedCount >= plan.CardCount;
        public bool IsCommitted { get; private set; }
        public bool IsCancelled { get; private set; }
        public IReadOnlyList<ItemInstance> Obtained => visibleObtained;

        internal PlayableHarvestTransaction(ResourceSystem owner, ResourcePointInstance point, HunterInstance hunter, HarvestDrawPlan plan, Action release = null)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.point = point ?? throw new ArgumentNullException(nameof(point));
            this.hunter = hunter;
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.release = release;
            visibleObtained = obtained.AsReadOnly();
        }

        public HarvestCardResult RevealNext()
        {
            if (!CanReveal)
                throw new InvalidOperationException("No harvest card remains to reveal.");

            HarvestCardResult result = plan.Cards[revealedCount++];
            if (result.IsHit)
                RevealedHitCount++;
            return result;
        }

        internal IReadOnlyList<ItemInstance> Commit()
        {
            if (IsCommitted)
                return visibleObtained;
            if (IsCancelled)
                throw new InvalidOperationException("A cancelled harvest transaction cannot be committed.");
            if (!IsComplete)
                throw new InvalidOperationException("All harvest cards must be revealed before commit.");
            if (point.IsExhausted)
                throw new InvalidOperationException("The resource point was exhausted by another harvest.");

            for (int i = 0; i < plan.HitCount; i++)
            {
                if (point.Resource == null)
                    break;
                var item = new ItemInstance(point.Resource);
                obtained.Add(item);
                hunter?.Collectibles?.Add(item);
            }
            point.IsExhausted = true;
            IsCommitted = true;
            Release();
            return visibleObtained;
        }

        public bool Cancel()
        {
            if (IsCancelled) return true;
            if (IsCommitted || revealedCount > 0)
                return false;
            IsCancelled = true;
            Release();
            return true;
        }

        internal bool IsOwnedBy(ResourceSystem resourceSystem) => ReferenceEquals(owner, resourceSystem);

        internal void Abandon()
        {
            if (IsCommitted) return;
            IsCancelled = true;
            Release();
        }

        private void Release()
        {
            if (isReleased) return;
            isReleased = true;
            release?.Invoke();
        }
    }
}
