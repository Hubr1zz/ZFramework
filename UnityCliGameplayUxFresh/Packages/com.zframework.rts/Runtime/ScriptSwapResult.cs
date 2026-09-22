namespace ZFramework.RTS
{
    public sealed class ScriptSwapResult
    {
        private ScriptSwapResult(bool succeeded, int replacedCount, string error)
        {
            Succeeded = succeeded;
            ReplacedCount = replacedCount;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }
        public int ReplacedCount { get; }
        public string Error { get; }
        public static ScriptSwapResult Success(int count) => new ScriptSwapResult(true, count, string.Empty);
        public static ScriptSwapResult Failure(string error) => new ScriptSwapResult(false, 0, error);
    }
}
