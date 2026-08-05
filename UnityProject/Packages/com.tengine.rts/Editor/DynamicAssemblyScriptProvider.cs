using System;
using System.Collections.Generic;
using System.Reflection;

namespace TEngine.RTS.Editor
{
    internal sealed class DynamicAssemblyScriptProvider : IScriptProvider
    {
        private readonly Dictionary<string, Type> _scriptTypes = new Dictionary<string, Type>(StringComparer.Ordinal);

        public DynamicAssemblyScriptProvider(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            GenerationName = assembly.GetName().Name;
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface || !typeof(IScript).IsAssignableFrom(type)) continue;
                ScriptIdAttribute attribute = type.GetCustomAttribute<ScriptIdAttribute>();
                if (attribute == null || string.IsNullOrWhiteSpace(attribute.Id)) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null)
                    throw new InvalidOperationException($"RTS script '{type.FullName}' needs a public parameterless constructor.");
                if (_scriptTypes.ContainsKey(attribute.Id))
                    throw new InvalidOperationException($"Duplicate RTS script id '{attribute.Id}'.");
                _scriptTypes.Add(attribute.Id, type);
            }
        }

        public string GenerationName { get; }
        public bool TryCreate(string scriptId, out IScript script)
        {
            script = null;
            if (scriptId == null || !_scriptTypes.TryGetValue(scriptId, out Type type)) return false;
            script = (IScript)Activator.CreateInstance(type);
            return script != null;
        }
    }
}
