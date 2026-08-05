namespace TEngine.RTS
{
    public interface IScriptProvider
    {
        string GenerationName { get; }
        bool TryCreate(string scriptId, out IScript script);
    }
}
