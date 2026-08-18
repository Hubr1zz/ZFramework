using System;
using System.Collections.Generic;

namespace GameFramework.Buffs.Formula
{
    public readonly struct StatModifierTemplate
    {
        public StatModifierTemplate(
            FormulaKey formula,
            FormulaParameterKey parameter,
            ModifierLayerKey layer,
            double valuePerStack,
            int priority = 0)
        {
            Formula = formula;
            Parameter = parameter;
            Layer = layer;
            ValuePerStack = valuePerStack;
            Priority = priority;
        }

        public FormulaKey Formula { get; }
        public FormulaParameterKey Parameter { get; }
        public ModifierLayerKey Layer { get; }
        public double ValuePerStack { get; }
        public int Priority { get; }
    }

    /// <summary>BuffKey 到数值贡献的独立映射；BuffDefinition 不依赖数值系统。</summary>
    public sealed class BuffStatModifierCatalog
    {
        private readonly Dictionary<BuffKey, IReadOnlyList<StatModifierTemplate>> _entries = new();

        public BuffStatModifierCatalog Register(
            BuffKey buff,
            params StatModifierTemplate[] modifiers)
        {
            if (modifiers == null)
                throw new ArgumentNullException(nameof(modifiers));
            _entries[buff] = modifiers;
            return this;
        }

        public bool TryGet(BuffKey buff, out IReadOnlyList<StatModifierTemplate> modifiers) =>
            _entries.TryGetValue(buff, out modifiers);
    }

    /// <summary>把 Buff 生命周期翻译为可撤销的公式 Modifier handle。</summary>
    public sealed class BuffStatBinding : IDisposable
    {
        private readonly BuffContainer _container;
        private readonly StatModifierCollection _modifiers;
        private readonly BuffStatModifierCatalog _catalog;
        private readonly Dictionary<long, List<IDisposable>> _handles = new();
        private bool _disposed;

        public BuffStatBinding(
            BuffContainer container,
            StatModifierCollection modifiers,
            BuffStatModifierCatalog catalog)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _modifiers = modifiers ?? throw new ArgumentNullException(nameof(modifiers));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _container.Changed += OnBuffChanged;

            foreach (BuffInstance instance in _container.Active)
                Rebuild(instance);
        }

        private void OnBuffChanged(object sender, BuffChangedEventArgs args)
        {
            switch (args.Kind)
            {
                case BuffChangeKind.Added:
                    Rebuild(args.Instance);
                    break;
                case BuffChangeKind.Updated when args.PreviousStacks != args.Instance.Stacks:
                    Rebuild(args.Instance);
                    break;
                case BuffChangeKind.Removed:
                case BuffChangeKind.Expired:
                    Release(args.Instance.Id);
                    break;
            }
        }

        private void Rebuild(BuffInstance instance)
        {
            Release(instance.Id);
            if (!_catalog.TryGet(instance.Definition.Key, out IReadOnlyList<StatModifierTemplate> templates))
                return;

            var handles = new List<IDisposable>(templates.Count);
            foreach (StatModifierTemplate template in templates)
            {
                handles.Add(_modifiers.Add(
                    template.Formula,
                    template.Parameter,
                    template.Layer,
                    template.ValuePerStack * instance.Stacks,
                    template.Priority,
                    instance));
            }
            _handles.Add(instance.Id, handles);
        }

        private void Release(long instanceId)
        {
            if (!_handles.TryGetValue(instanceId, out List<IDisposable> handles))
                return;
            _handles.Remove(instanceId);
            foreach (IDisposable handle in handles)
                handle.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _container.Changed -= OnBuffChanged;
            foreach (List<IDisposable> handles in _handles.Values)
            {
                foreach (IDisposable handle in handles)
                    handle.Dispose();
            }
            _handles.Clear();
        }
    }
}
