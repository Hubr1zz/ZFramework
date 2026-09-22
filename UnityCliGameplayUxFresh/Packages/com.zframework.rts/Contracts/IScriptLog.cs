namespace ZFramework.RTS
{
    public interface IScriptLog
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
    }
}
