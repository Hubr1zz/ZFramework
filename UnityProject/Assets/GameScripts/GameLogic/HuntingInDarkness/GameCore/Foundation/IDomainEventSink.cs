namespace HuntingInDarkness.GameCore.Foundation
{
    /// <summary>
    /// Output port for domain facts. Engine adapters decide how facts are transported.
    /// </summary>
    public interface IDomainEventSink
    {
        void Publish<TEvent>(TEvent domainEvent) where TEvent : struct;
    }

    public sealed class NullDomainEventSink : IDomainEventSink
    {
        public static readonly NullDomainEventSink Instance = new NullDomainEventSink();

        private NullDomainEventSink() { }

        public void Publish<TEvent>(TEvent domainEvent) where TEvent : struct { }
    }
}
