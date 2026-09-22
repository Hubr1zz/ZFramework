using HuntingInDarkness.Data;

namespace HuntingInDarkness.ActionFlow.Events
{
    public readonly struct PlayableEventItemChange
    {
        public PlayableEventItemChange(string itemId, int actorId, int oldAmount, int newAmount)
        {
            ItemId = itemId ?? string.Empty;
            ActorId = actorId;
            OldAmount = oldAmount;
            NewAmount = newAmount;
        }

        public string ItemId { get; }
        public int ActorId { get; }
        public int OldAmount { get; }
        public int NewAmount { get; }
        public bool Changed => OldAmount != NewAmount;
    }

    public interface IPlayableEventItemAvailability
    {
        int GetAvailableAmount(string itemId, HunterInstance actor);
    }

    public interface IPlayableEventItemCommand : IPlayableEventItemAvailability
    {
        bool CanRemove(string itemId, int amount, HunterInstance actor, out string reason);
        bool TryAdd(string itemId, int amount, HunterInstance actor, out PlayableEventItemChange change, out string reason);
        bool TryRemove(string itemId, int amount, HunterInstance actor, out PlayableEventItemChange change, out string reason);
    }

    public static class PlayableEventAvailabilityScope
    {
        public static IPlayableEventResourceAvailability Compose(IPlayableEventResourceAvailability resourceAvailability, IPlayableEventItemAvailability itemAvailability)
        {
            if (resourceAvailability == null || itemAvailability == null) return resourceAvailability;
            if (resourceAvailability is IPlayableEventItemAvailability) return resourceAvailability;
            return new CombinedAvailability(resourceAvailability, itemAvailability);
        }

        private sealed class CombinedAvailability : IPlayableEventResourceAvailability, IPlayableEventItemAvailability
        {
            private readonly IPlayableEventResourceAvailability resourceAvailability;
            private readonly IPlayableEventItemAvailability itemAvailability;

            public CombinedAvailability(IPlayableEventResourceAvailability resourceAvailability, IPlayableEventItemAvailability itemAvailability)
            {
                this.resourceAvailability = resourceAvailability;
                this.itemAvailability = itemAvailability;
            }

            public PlayableEventResourceScope Scope => resourceAvailability.Scope;
            public int GetAvailableAmount(string resourceId) => resourceAvailability.GetAvailableAmount(resourceId);
            public int GetAvailableAmount(string itemId, HunterInstance actor) => itemAvailability.GetAvailableAmount(itemId, actor);
        }
    }

    public struct PlayableEventItemChangedEvent
    {
        public string ItemId;
        public int ActorId;
        public int OldAmount;
        public int NewAmount;
    }
}
