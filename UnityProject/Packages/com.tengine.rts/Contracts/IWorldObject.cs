namespace TEngine.RTS
{
    public interface IWorldObject
    {
        ulong InstanceId { get; }
        string Name { get; }
        bool TryGetCapability<T>(out T capability) where T : class;
    }
}
