#if UNITY_EDITOR
using Cards3D;
using TMPro;
using UI;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 菜单路径：CardTactics → Create Prefabs → ResourceCard3D
///
/// 生成的 Prefab 层级：
///   ResourceCard3D               ← ResourceCard3D 脚本 + BoxCollider
///   ├── Body                     ← Cube，scale(0.75, 0.025, 1.05)，Renderer 已连线
///   ├── Name                     ← TextMeshPro，朝上(Euler 90,0,0)，已连线
///   ├── Count                    ← TextMeshPro
///   └── Label                    ← TextMeshPro
///
/// 保存路径：Assets/Prefabs/Cards/ResourceCard3D.prefab
/// </summary>
public static class CardPrefabCreator
{
    const string PrefabDir = "Assets/Prefabs/Cards";

    [MenuItem("CardTactics/Create Prefabs/ResourceCard3D")]
    static void CreateResourceCard3DPrefab()
    {
        EnsureDir(PrefabDir);

        // ── 根节点 ──────────────────────────────────────────────────────
        var root = new GameObject("ResourceCard3D");

        // BoxCollider（匹配 CardView3D.BuildBaseGeometry 的规格）
        const float CW        = CardView3D.CW;   // 0.75
        const float CH        = CardView3D.CH;   // 1.05
        const float CD        = CardView3D.CD;   // 0.025
        const float HoverLift = 0.12f;

        var col = root.AddComponent<BoxCollider>();
        col.size   = new Vector3(CW, HoverLift + CD * 2f, CH);
        col.center = new Vector3(0f, HoverLift * 0.4f, 0f);

        // ── Body ────────────────────────────────────────────────────────
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(CW, CD, CH);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        var bodyRenderer = body.GetComponent<Renderer>();

        // ── TextMeshPro 子节点 ───────────────────────────────────────────
        float ty = CD * 0.5f + 0.003f;

        var nameTmp  = MakeTMP("Name",  root.transform,
            new Vector3(0f, ty,  CH * 0.36f), 0.10f, new Vector2(CW - 0.06f, 0.18f));

        var countTmp = MakeTMP("Count", root.transform,
            new Vector3(0f, ty,  CH * 0.00f), 0.16f, new Vector2(CW - 0.08f, 0.30f));

        var labelTmp = MakeTMP("Label", root.transform,
            new Vector3(0f, ty, -CH * 0.35f), 0.07f, new Vector2(CW - 0.06f, 0.12f));

        // ── ResourceCard3D 脚本 + 字段连线 ──────────────────────────────
        var card = root.AddComponent<ResourceCard3D>();

        // 通过 SerializedObject 给 [SerializeField] private 字段赋值
        var so = new SerializedObject(card);
        so.FindProperty("_bodyRenderer").objectReferenceValue = bodyRenderer;
        so.FindProperty("_nameText").objectReferenceValue     = nameTmp;
        so.FindProperty("_countText").objectReferenceValue    = countTmp;
        so.FindProperty("_labelText").objectReferenceValue    = labelTmp;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ── 保存为 Prefab ────────────────────────────────────────────────
        string path = $"{PrefabDir}/ResourceCard3D.prefab";
        var prefab  = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        AssetDatabase.Refresh();
        Selection.activeObject = prefab; // 保存后自动选中
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[CardPrefabCreator] Prefab saved: {path}");
    }

    // ── 工具 ────────────────────────────────────────────────────────────

    static TextMeshPro MakeTMP(string goName, Transform parent,
        Vector3 localPos, float fontSize, Vector2 rectSize)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.fontSize              = fontSize;
        tmp.alignment             = TextAlignmentOptions.Center;
        tmp.color                 = new Color(0.08f, 0.08f, 0.08f);
        tmp.rectTransform.sizeDelta = rectSize;
#if UNITY_6000_0_OR_NEWER
        tmp.textWrappingMode      = TMPro.TextWrappingModes.Normal;
#else
        tmp.enableWordWrapping    = true;
#endif
        tmp.overflowMode          = TextOverflowModes.Ellipsis;
        return tmp;
    }

    static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts  = path.Split('/');
        var parent = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var full = parent + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, parts[i]);
            parent = full;
        }
    }
}
#endif
