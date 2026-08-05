using System;
using System.Collections.Generic;

namespace TEngine.RTS
{
    public sealed class StaticScriptProvider : IScriptProvider
    {
        private readonly Dictionary<string, Func<IScript>> _factories =
            new Dictionary<string, Func<IScript>>(StringComparer.Ordinal);

        public StaticScriptProvider(string generationName = "static")
        {
            GenerationName = string.IsNullOrWhiteSpace(generationName) ? "static" : generationName;
        }

        public string GenerationName { get; }

        public StaticScriptProvider Register<T>(string scriptId) where T : IScript, new()
        {
            return Register(scriptId, () => new T());
        }

        public StaticScriptProvider Register(string scriptId, Func<IScript> factory)
        {
            if (string.IsNullOrWhiteSpace(scriptId)) throw new ArgumentException("Script id cannot be empty.", nameof(scriptId));
            _factories[scriptId] = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        public bool TryCreate(string scriptId, out IScript script)
        {
            script = null;
            return scriptId != null && _factories.TryGetValue(scriptId, out Func<IScript> factory) &&
                   (script = factory()) != null;
        }
    }
}
