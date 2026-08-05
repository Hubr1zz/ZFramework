using System;
using System.Collections.Generic;

namespace TEngine.RTS
{
    public static class RtsServiceRegistry
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();
        public static void Register<T>(T service) where T : class
        { if (service == null) throw new ArgumentNullException(nameof(service)); Services[typeof(T)] = service; }
        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out object value) && value is T typed) { service = typed; return true; }
            service = null; return false;
        }
        public static void Unregister<T>() where T : class => Services.Remove(typeof(T));
        public static void Clear() => Services.Clear();
    }

    internal sealed class UnityScriptContext : IScriptLog, IScriptServiceResolver
    {
        public void LogInfo(string message) => Log.Info("[RTS] {0}", message);
        public void LogWarning(string message) => Log.Warning("[RTS] {0}", message);
        public void LogError(string message) => Log.Error("[RTS] {0}", message);
        public bool TryGetService<T>(out T service) where T : class => RtsServiceRegistry.TryGet(out service);
    }
}
