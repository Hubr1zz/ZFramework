using Cards3D;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 没有美术 Prefab 时创建可交互的角色占位实体，保证战斗流程仍可完整操作。
    /// </summary>
    public static class PlayableCharacterEntityFactory
    {
        public static CharacterEntity Create(Transform parent)
        {
            var root = new GameObject("CharacterEntity (Fallback)");
            root.transform.SetParent(parent, false);
            var entity = root.AddComponent<CharacterEntity>();
            var clickCollider = root.AddComponent<CapsuleCollider>();
            clickCollider.center = new Vector3(0f, 0.5f, 0f);
            clickCollider.height = 1f;
            clickCollider.radius = 0.25f;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            Object.Destroy(body.GetComponent<Collider>());
            var renderer = body.GetComponent<Renderer>();
            renderer.material.color = new Color(0.25f, 0.45f, 0.95f);

            var head = new GameObject("Head Anchor").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 1.1f, 0f);

            var panel = new GameObject("Action Panel");
            panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0.02f, -1.1f);
            var actionGrid = SlotGrid.Create(panel.transform, Vector3.zero, 3, 1, 0.55f, 0.77f, 0.08f, true, CardCategory.HunterAction);
            entity.BindReferences(head, null, panel, null, actionGrid, null, clickCollider);
            return entity;
        }
    }
}
