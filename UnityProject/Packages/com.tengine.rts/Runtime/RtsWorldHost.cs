using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine.RTS
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("TEngine/RTS/World Host")]
    public class RtsWorldHost : MonoBehaviour, IRtsWorldServiceV1
    {
        private sealed class Entity
        {
            public int Handle;
            public string OwnerKey;
            public string StableKey;
            public string Prototype;
            public GameObject GameObject;
            public Material Material;
            public RtsWorldEntitySpec Spec;
        }

        private sealed class Reconcile : IRtsWorldReconcileV1
        {
            private readonly RtsWorldHost _host;
            private readonly string _ownerKey;
            private readonly Dictionary<string, Entity> _desired = new Dictionary<string, Entity>(StringComparer.Ordinal);
            private bool _finished;

            public Reconcile(RtsWorldHost host, string ownerKey) { _host = host; _ownerKey = ownerKey; }

            public int Upsert(string stableKey, in RtsWorldEntitySpec spec)
            {
                if (_finished) throw new ObjectDisposedException(nameof(Reconcile));
                if (string.IsNullOrWhiteSpace(stableKey)) throw new ArgumentException("Stable key is required.", nameof(stableKey));
                int handle = _host.ReserveHandle(_ownerKey, stableKey);
                _desired[stableKey] = new Entity
                {
                    Handle = handle,
                    OwnerKey = _ownerKey,
                    StableKey = stableKey,
                    Prototype = spec.Prototype,
                    Spec = spec
                };
                return handle;
            }

            public void Commit()
            {
                if (_finished) throw new ObjectDisposedException(nameof(Reconcile));
                _host.Commit(_ownerKey, _desired);
                _finished = true;
            }

            public void Dispose()
            {
                if (_finished) return;
                _host.Cancel(_ownerKey);
                _finished = true;
            }
        }

        [SerializeField] private bool suppressPersistentOverlays = true;
        private readonly Dictionary<int, Entity> _byHandle = new Dictionary<int, Entity>();
        private readonly Dictionary<string, Dictionary<string, Entity>> _byOwner =
            new Dictionary<string, Dictionary<string, Entity>>(StringComparer.Ordinal);
        private readonly HashSet<string> _openTransactions = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<Canvas> _suppressedCanvases = new List<Canvas>();
        private readonly List<global::TEngine.Debugger> _suppressedDebuggers = new List<global::TEngine.Debugger>();
        private int _nextHandle = 1;

        public int EntityCount => _byHandle.Count;

        private void OnEnable()
        {
            RtsServiceRegistry.Register<IRtsWorldServiceV1>(this);
            if (suppressPersistentOverlays) SuppressPersistentOverlays();
        }

        private void OnDisable()
        {
            if (RtsServiceRegistry.TryGet(out IRtsWorldServiceV1 registered) && ReferenceEquals(registered, this))
                RtsServiceRegistry.Unregister<IRtsWorldServiceV1>();
            ClearAll();
            RestorePersistentOverlays();
        }

        public IRtsWorldReconcileV1 BeginReconcile(string ownerKey)
        {
            if (string.IsNullOrWhiteSpace(ownerKey)) throw new ArgumentException("Owner key is required.", nameof(ownerKey));
            if (!_openTransactions.Add(ownerKey)) throw new InvalidOperationException($"Owner '{ownerKey}' already has an open reconcile.");
            return new Reconcile(this, ownerKey);
        }

        public bool Exists(int handle) => _byHandle.ContainsKey(handle);

        public int Spawn(string ownerKey, string stableKey, in RtsWorldEntitySpec spec)
        {
            if (string.IsNullOrWhiteSpace(ownerKey)) throw new ArgumentException("Owner key is required.", nameof(ownerKey));
            if (string.IsNullOrWhiteSpace(stableKey)) throw new ArgumentException("Stable key is required.", nameof(stableKey));
            Validate(in spec);
            if (!_byOwner.TryGetValue(ownerKey, out Dictionary<string, Entity> owner))
            {
                owner = new Dictionary<string, Entity>(StringComparer.Ordinal);
                _byOwner.Add(ownerKey, owner);
            }
            if (owner.TryGetValue(stableKey, out Entity existing))
            {
                if (!string.Equals(existing.Prototype, spec.Prototype, StringComparison.OrdinalIgnoreCase))
                {
                    int handle = existing.Handle;
                    Remove(existing);
                    existing = Create(new Entity { Handle = handle, OwnerKey = ownerKey, StableKey = stableKey, Prototype = spec.Prototype, Spec = spec });
                }
                Apply(existing, in spec);
                owner[stableKey] = existing;
                _byHandle[existing.Handle] = existing;
                return existing.Handle;
            }
            Entity created = Create(new Entity
            {
                Handle = _nextHandle++, OwnerKey = ownerKey, StableKey = stableKey,
                Prototype = spec.Prototype, Spec = spec
            });
            Apply(created, in spec);
            owner[stableKey] = created;
            _byHandle[created.Handle] = created;
            return created.Handle;
        }

        public void Despawn(int handle)
        {
            if (_byHandle.TryGetValue(handle, out Entity entity)) Remove(entity);
        }

        public void SetTransform(int handle, RtsVector3 position, RtsVector3 rotation, RtsVector3 scale)
        {
            Entity entity = Require(handle);
            RtsWorldEntitySpec previous = entity.Spec;
            var next = new RtsWorldEntitySpec(previous.Prototype, position, rotation, scale, previous.Color, previous.Text);
            Apply(entity, in next);
        }

        public void SetColor(int handle, RtsColor color)
        {
            Entity entity = Require(handle);
            RtsWorldEntitySpec previous = entity.Spec;
            var next = new RtsWorldEntitySpec(previous.Prototype, previous.Position, previous.Rotation, previous.Scale, color, previous.Text);
            Apply(entity, in next);
        }

        public void SetText(int handle, string text)
        {
            Entity entity = Require(handle);
            RtsWorldEntitySpec previous = entity.Spec;
            var next = new RtsWorldEntitySpec(previous.Prototype, previous.Position, previous.Rotation, previous.Scale, previous.Color, text);
            Apply(entity, in next);
        }

        public bool TryGetHandle(string ownerKey, string stableKey, out int handle)
        {
            handle = 0;
            if (!_byOwner.TryGetValue(ownerKey ?? string.Empty, out Dictionary<string, Entity> owner) ||
                !owner.TryGetValue(stableKey ?? string.Empty, out Entity entity)) return false;
            handle = entity.Handle;
            return true;
        }

        private int ReserveHandle(string ownerKey, string stableKey)
        {
            if (_byOwner.TryGetValue(ownerKey, out Dictionary<string, Entity> owner) &&
                owner.TryGetValue(stableKey, out Entity existing)) return existing.Handle;
            return _nextHandle++;
        }

        private Entity Require(int handle)
        {
            if (!_byHandle.TryGetValue(handle, out Entity entity))
                throw new KeyNotFoundException($"Unknown RTS world handle {handle}.");
            return entity;
        }

        private void Commit(string ownerKey, Dictionary<string, Entity> desired)
        {
            try
            {
                foreach (Entity entity in desired.Values) Validate(entity.Spec);
                if (!_byOwner.TryGetValue(ownerKey, out Dictionary<string, Entity> current))
                {
                    current = new Dictionary<string, Entity>(StringComparer.Ordinal);
                    _byOwner.Add(ownerKey, current);
                }

                var removals = new List<string>();
                foreach (string stableKey in current.Keys)
                    if (!desired.ContainsKey(stableKey)) removals.Add(stableKey);
                for (int i = 0; i < removals.Count; i++) Remove(current[removals[i]]);

                foreach (KeyValuePair<string, Entity> pair in desired)
                {
                    if (current.TryGetValue(pair.Key, out Entity entity))
                    {
                        if (!string.Equals(entity.Prototype, pair.Value.Prototype, StringComparison.OrdinalIgnoreCase))
                        {
                            Remove(entity);
                            entity = Create(pair.Value);
                        }
                    }
                    else entity = Create(pair.Value);
                    Apply(entity, pair.Value.Spec);
                    current[pair.Key] = entity;
                    _byHandle[entity.Handle] = entity;
                }
            }
            finally { _openTransactions.Remove(ownerKey); }
        }

        private void Cancel(string ownerKey) => _openTransactions.Remove(ownerKey);

        private static void Validate(in RtsWorldEntitySpec spec)
        {
            if (string.Equals(spec.Prototype, "hud", StringComparison.OrdinalIgnoreCase)) return;
            ResolvePrimitive(spec.Prototype);
        }

        private Entity Create(Entity desired)
        {
            var entity = new Entity
            {
                Handle = desired.Handle,
                OwnerKey = desired.OwnerKey,
                StableKey = desired.StableKey,
                Prototype = desired.Prototype
            };
            if (!string.Equals(entity.Prototype, "hud", StringComparison.OrdinalIgnoreCase))
            {
                PrimitiveType type = ResolvePrimitive(entity.Prototype);
                entity.GameObject = GameObject.CreatePrimitive(type);
                entity.GameObject.name = $"RTS/{entity.OwnerKey}/{entity.StableKey}";
                entity.GameObject.transform.SetParent(transform, false);
                Collider collider = entity.GameObject.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                Renderer renderer = entity.GameObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Shader shader = Shader.Find("Standard");
                    if (shader != null)
                    {
                        entity.Material = new Material(shader);
                        renderer.sharedMaterial = entity.Material;
                    }
                }
            }
            return entity;
        }

        private static PrimitiveType ResolvePrimitive(string prototype)
        {
            switch ((prototype ?? string.Empty).ToLowerInvariant())
            {
                case "plane": return PrimitiveType.Plane;
                case "sphere": return PrimitiveType.Sphere;
                case "capsule": return PrimitiveType.Capsule;
                case "cylinder": return PrimitiveType.Cylinder;
                case "cube": return PrimitiveType.Cube;
                default: throw new ArgumentException($"Unknown RTS prototype '{prototype}'.");
            }
        }

        private static void Apply(Entity entity, in RtsWorldEntitySpec spec)
        {
            entity.Spec = spec;
            if (entity.GameObject == null) return;
            Transform value = entity.GameObject.transform;
            value.position = new Vector3(spec.Position.X, spec.Position.Y, spec.Position.Z);
            value.eulerAngles = new Vector3(spec.Rotation.X, spec.Rotation.Y, spec.Rotation.Z);
            value.localScale = new Vector3(spec.Scale.X, spec.Scale.Y, spec.Scale.Z);
            if (entity.Material != null)
                entity.Material.color = new Color(spec.Color.R, spec.Color.G, spec.Color.B, spec.Color.A);
        }

        private void Remove(Entity entity)
        {
            if (entity == null) return;
            _byHandle.Remove(entity.Handle);
            if (_byOwner.TryGetValue(entity.OwnerKey, out Dictionary<string, Entity> owner))
                owner.Remove(entity.StableKey);
            if (entity.GameObject != null) Destroy(entity.GameObject);
            if (entity.Material != null) Destroy(entity.Material);
        }

        private void ClearAll()
        {
            var snapshot = new List<Entity>(_byHandle.Values);
            for (int i = 0; i < snapshot.Count; i++) Remove(snapshot[i]);
            _byHandle.Clear();
            _byOwner.Clear();
            _openTransactions.Clear();
        }

        private void OnGUI()
        {
            foreach (Entity entity in _byHandle.Values)
            {
                if (!string.Equals(entity.Prototype, "hud", StringComparison.OrdinalIgnoreCase)) continue;
                RtsWorldEntitySpec spec = entity.Spec;
                Color previous = GUI.color;
                GUI.color = new Color(spec.Color.R, spec.Color.G, spec.Color.B, spec.Color.A);
                var rect = new Rect(spec.Position.X, spec.Position.Y,
                    Mathf.Max(1f, spec.Scale.X), Mathf.Max(1f, spec.Scale.Y));
                GUI.Box(rect, spec.Text ?? string.Empty);
                GUI.color = previous;
            }
        }

        private void SuppressPersistentOverlays()
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (!canvas.enabled || canvas.gameObject.scene == gameObject.scene) continue;
                canvas.enabled = false;
                _suppressedCanvases.Add(canvas);
            }
            global::TEngine.Debugger[] debuggers = FindObjectsOfType<global::TEngine.Debugger>(true);
            for (int i = 0; i < debuggers.Length; i++)
            {
                if (!debuggers[i].enabled) continue;
                debuggers[i].enabled = false;
                _suppressedDebuggers.Add(debuggers[i]);
            }
        }

        private void RestorePersistentOverlays()
        {
            for (int i = 0; i < _suppressedCanvases.Count; i++)
                if (_suppressedCanvases[i] != null) _suppressedCanvases[i].enabled = true;
            _suppressedCanvases.Clear();
            for (int i = 0; i < _suppressedDebuggers.Count; i++)
                if (_suppressedDebuggers[i] != null) _suppressedDebuggers[i].enabled = true;
            _suppressedDebuggers.Clear();
        }
    }
}
