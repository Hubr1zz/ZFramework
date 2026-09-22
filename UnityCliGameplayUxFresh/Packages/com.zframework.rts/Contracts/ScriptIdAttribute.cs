using System;

namespace ZFramework.RTS
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ScriptIdAttribute : Attribute
    {
        public ScriptIdAttribute(string id) { Id = id ?? throw new ArgumentNullException(nameof(id)); }
        public string Id { get; }
    }
}
