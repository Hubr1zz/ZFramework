using Cards3D;
using TMPro;
using UI;
using UnityEngine;

namespace CardTest3D
{
    /// <summary>
    /// 3DCardTest 场景引导脚本。
    /// 用法：在 3DCardTest 场景中新建空 GameObject，挂上此脚本，Play 即可。
    ///
    /// 功能演示：
    ///   - 桌面排列 4 张物资卡（木材/食物/药草/铁矿）
    ///   - 3 个卡槽位于桌面下方
    ///   - 左键拖拽移动卡牌；拖入合法卡槽自动吸附
    ///   - 右键翻转卡牌（翻转动画 0.3 s）
    ///   - 鼠标悬停时卡牌轻微抬起高亮
    /// </summary>
    public class CardTableTestSetup : MonoBehaviour
    {
        private void Start()
        {
            EnsureCamera();
            var table = CreateTable();
            SpawnCards(table);
            SpawnSlots(table);
            SpawnLabels(table);
        }

        // ─── 相机 ─────────────────────────────────────────────────────────────

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;

            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.10f, 0.10f, 0.13f);
            go.transform.position = new Vector3(0f, 6.5f, -1.5f);
            go.transform.rotation = Quaternion.Euler(68f, 0f, 0f);
        }

        // ─── 桌面 ─────────────────────────────────────────────────────────────

        private static GameObject CreateTable()
        {
            var root = new GameObject("CardTable");
            root.transform.position = Vector3.zero;

            var surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            surface.name = "TableSurface";
            surface.transform.SetParent(root.transform, false);
            surface.transform.localScale = new Vector3(1.4f, 1f, 1.4f);
            surface.GetComponent<MeshRenderer>().material.color =
                new Color(0.20f, 0.16f, 0.12f);
            // 桌面保留 Collider 以供未来交互，这里不删除
            return root;
        }

        // ─── 物资卡 ───────────────────────────────────────────────────────────

        private static void SpawnCards(GameObject table)
        {
            const float startX  = -1.43f;
            const float spacing = 0.95f;
            const float cardY   = 0.013f;
            const float cardZ   = 0.8f;

            (string name, string desc, int qty)[] items =
            {
                ("木材", "用于建造\n基础结构。", 3),
                ("食物", "维持队员\n行动力。",   5),
                ("药草", "应急治疗\n材料。",     2),
                ("铁矿", "打造武器\n与工具。",   4),
            };

            for (int i = 0; i < items.Length; i++)
            {
                EntityCreator.CreateResourceCard(items[i].name,1,table.transform);
            }
        }

        // ─── 卡槽 ─────────────────────────────────────────────────────────────

        private static void SpawnSlots(GameObject table)
        {
            const float startX  = -0.95f;
            const float spacing = 0.95f;
            const float slotZ   = -0.8f;

            for (int i = 0; i < 3; i++)
            {
                CardSlot.Create(
                    table.transform,
                    new Vector3(startX + i * spacing, 0f, slotZ));
            }
        }

        // ─── 标签 ─────────────────────────────────────────────────────────────

        private static void SpawnLabels(GameObject table)
        {
            // 场景标题
            MakeLabel("物 资 卡 测 试", table.transform,
                new Vector3(0f, 0.01f, 2.2f), 0.22f);

            // 卡牌区说明
            MakeLabel("物资卡（右键翻转 · 左键拖拽）", table.transform,
                new Vector3(0f, 0.01f, 1.5f), 0.10f);

            // 卡槽区说明
            MakeLabel("放 置 槽", table.transform,
                new Vector3(0f, 0.01f, -0.3f), 0.13f);
        }

        private static void MakeLabel(string text, Transform parent,
            Vector3 localPos, float fontSize = 0.13f)
        {
            var go = new GameObject($"Label_{text}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = new Color(0.80f, 0.76f, 0.68f);
            tmp.rectTransform.sizeDelta = new Vector2(6f, 0.4f);
        }

   
    }
}
