namespace ZFramework.RTS
{
    public sealed class ScriptState
    {
        public ScriptState(int schemaVersion, string payload)
        {
            SchemaVersion = schemaVersion;
            Payload = payload ?? string.Empty;
        }

        public int SchemaVersion { get; }
        public string Payload { get; }
        public static ScriptState Empty { get; } = new ScriptState(0, string.Empty);
    }
}
