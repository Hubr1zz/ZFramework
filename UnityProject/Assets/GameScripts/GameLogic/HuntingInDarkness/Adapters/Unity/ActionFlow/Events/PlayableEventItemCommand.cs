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

    public interface IPlayableEventItemCommand
    {
        bool TryAdd(string itemId, int amount, HunterInstance actor, out PlayableEventItemChange change, out string reason);
    }

    public struct PlayableEventItemChangedEvent
    {
        public string ItemId;
        public int ActorId;
        public int OldAmount;
        public int NewAmount;
    }
}
