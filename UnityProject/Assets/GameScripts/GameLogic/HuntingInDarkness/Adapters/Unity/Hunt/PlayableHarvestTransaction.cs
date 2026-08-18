using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunt;

namespace HuntingInDarkness.Hunt
{
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
        public int RevealedCount => revealedCount;
        public int RevealedHitCount { get; private set; }
        public ResourcePointInstance Point => point;
        public string ResourceName => point.ResourceName;
        public string HunterName => hunter?.Name ?? "?";
        public bool CanReveal => !IsCommitted && revealedCount < plan.CardCount;
        public bool IsComplete => revealedCount >= plan.CardCount;
        public bool IsCommitted { get; private set; }
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
            if (IsCommitted || revealedCount > 0)
                return false;
            Release();
            return true;
        }

        internal bool IsOwnedBy(ResourceSystem resourceSystem) => ReferenceEquals(owner, resourceSystem);

        private void Release()
        {
            if (isReleased) return;
            isReleased = true;
            release?.Invoke();
        }
    }
}
