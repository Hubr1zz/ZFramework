namespace TEngine.RTS
{
    public enum RtsParameterType { String, Integer, Float, Boolean }

    public interface IScriptServiceResolver
    {
        bool TryGetService<T>(out T service) where T : class;
    }

    public enum ScriptStateMigrationPolicy
    {
        PreserveWhenCompatible,
        Reset,
        RequireCompatibleSchema
    }

    public interface IScriptStateSchema
    {
        int StateSchemaVersion { get; }
    }

    public interface IScriptContext : IScriptLog
    {
        IScriptScope Scope { get; }
        bool TryGetService<T>(out T service) where T : class;
    }
}
