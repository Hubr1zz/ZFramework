namespace ZFramework
{
    internal class ResourceLogger : YooAsset.ILogger
    {
        public void Log(string message)
        {
            ZFramework.Log.Info(message);
        }

        public void Warning(string message)
        {
            ZFramework.Log.Warning(message);
        }

        public void Error(string message)
        {
            ZFramework.Log.Error(message);
        }

        public void Exception(System.Exception exception)
        {
            ZFramework.Log.Fatal(exception.Message);
        }
    }
}