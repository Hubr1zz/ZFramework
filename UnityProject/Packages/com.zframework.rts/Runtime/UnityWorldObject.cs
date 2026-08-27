using UnityEngine;

namespace ZFramework.RTS
{
    internal sealed class UnityWorldObject : IWorldObject, ITransformAccess
    {
        private readonly ScriptAnchor _anchor;
        public UnityWorldObject(ScriptAnchor anchor) { _anchor = anchor; }
        public ulong InstanceId
        {
            get
            {
#if UNITY_6000_5_OR_NEWER
                return UnityEngine.EntityId.ToULong(_anchor.GetEntityId());
#else
                return unchecked((uint)_anchor.GetInstanceID());
#endif
            }
        }
        public string Name => _anchor.name;

        public float PositionX { get => _anchor.transform.position.x; set => SetPosition(value, PositionY, PositionZ); }
        public float PositionY { get => _anchor.transform.position.y; set => SetPosition(PositionX, value, PositionZ); }
        public float PositionZ { get => _anchor.transform.position.z; set => SetPosition(PositionX, PositionY, value); }

        public bool TryGetCapability<T>(out T capability) where T : class
        {
            capability = this as T;
            return capability != null;
        }

        public void Translate(float x, float y, float z) => _anchor.transform.Translate(x, y, z, Space.World);
        private void SetPosition(float x, float y, float z) => _anchor.transform.position = new Vector3(x, y, z);
    }
}
