using Core;
using GameplayBase;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Settlement
{
    /// <summary>营地工坊 View：展示设施条件并提交一次原子建造事务。</summary>
    public sealed class PlayableWorkshopConstructionView : MonoBehaviour
    {
        private const int WindowId = 68025;
        private const string CatalogPath = "HuntingInDarkness/PlayableWorkshopCatalog";
        private GameManager manager;
        private PlayableWorkshopCatalog catalog;
        private PlayableWorkshopConstructionService service;
        private bool visible;
        private string statusText = string.Empty;
        private GUIStyle windowStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle mutedStyle;
        private GUIStyle statusStyle;
        private Texture2D windowTexture;

        public void Initialize(GameManager gameManager)
        {
            manager = gameManager;
            catalog = Resources.Load<PlayableWorkshopCatalog>(CatalogPath);
            service = new PlayableWorkshopConstructionService(() => manager?.SettlementData);
        }

        private void OnGUI()
        {
            if (manager == null || manager.CurrentGamePhase != GamePhase.Settlement || manager.SettlementData == null || catalog == null) return;

            EnsureStyles();
            int previousDepth = GUI.depth;
            GUI.depth = -830;
            if (visible)
            {
                GUI.Button(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, GUIStyle.none);
                GUI.Window(WindowId, GetWindowRect(), DrawWindow, "工坊建设", windowStyle);
            }
            else
                DrawWorkshopButton();
            GUI.depth = previousDepth;
        }

        private void DrawWorkshopButton()
        {
            var buttonRect = new Rect((Screen.width - 300f) * 0.5f, 106f, 300f, 36f);
            if (GUI.Button(buttonRect, "工坊建设"))
            {
                visible = true;
                statusText = string.Empty;
            }
        }

        private Rect GetWindowRect()
        {
            float width = Mathf.Min(640f, Screen.width - 48f);
            float height = Mathf.Min(560f, Screen.height - 48f);
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(10f);
            GUILayout.Label("把新知识变成真正能工作的地方", titleStyle);
            GUILayout.Label("发明决定营地懂得什么；建成工坊后，相应制造配方才会投入使用。", bodyStyle);
            GUILayout.Space(12f);

            foreach (PlayableWorkshopDefinition definition in catalog.Workshops)
                DrawWorkshop(definition);

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(statusText))
                GUILayout.Label(statusText, statusStyle);
            if (GUILayout.Button("返回营地", GUILayout.Height(38f)))
            {
                visible = false;
                statusText = string.Empty;
            }
        }

        private void DrawWorkshop(PlayableWorkshopDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.WorkshopId)) return;

            bool built = manager.SettlementData.IsWorkshopBuilt(definition.WorkshopId);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(built ? $"◆ {definition.DisplayName}（已建成）" : $"◇ {definition.DisplayName}", bodyStyle);
            GUILayout.Label(definition.Description, mutedStyle);
            GUILayout.Label(FormatRequirements(definition), mutedStyle);
            if (!built)
            {
                bool canBuild = service.CanBuild(definition, out string reason);
                GUI.enabled = canBuild;
                if (GUILayout.Button(canBuild ? "投入材料并建造" : reason, GUILayout.Height(36f)))
                    Build(definition);
                GUI.enabled = true;
            }
            GUILayout.EndVertical();
            GUILayout.Space(6f);
        }

        private void Build(PlayableWorkshopDefinition definition)
        {
            if (!service.TryBuild(definition, out string reason))
            {
                statusText = reason;
                return;
            }
            statusText = $"{definition.DisplayName} 已建成，相关配方现已开放。";
            manager.SaveSettlementProgress();
        }

        private static string FormatRequirements(PlayableWorkshopDefinition definition)
        {
            string requirement = definition.RequiredInvention != null ? $"前置发明：{definition.RequiredInvention.inventionName}" : "无需前置发明";
            var costs = new System.Collections.Generic.List<string>();
            foreach (PlayableWorkshopCost cost in definition.Costs)
                if (cost?.Item != null)
                    costs.Add($"{cost.Item.itemName} ×{cost.Amount}");
            return costs.Count == 0 ? requirement : $"{requirement}　·　材料：{string.Join("、", costs)}";
        }

        private void EnsureStyles()
        {
            if (windowStyle != null) return;

            windowTexture = new Texture2D(1, 1);
            windowTexture.SetPixel(0, 0, new Color(0.025f, 0.018f, 0.015f, 0.985f));
            windowTexture.Apply();
            windowStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(18, 18, 18, 18),
                normal = { background = windowTexture }
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.96f, 0.76f, 0.36f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                normal = { textColor = new Color(0.84f, 0.84f, 0.82f) }
            };
            mutedStyle = new GUIStyle(bodyStyle)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.64f, 0.66f, 0.68f) }
            };
            statusStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.72f, 0.9f, 0.62f) }
            };
        }

        private void OnDestroy()
        {
            if (windowTexture != null)
                Destroy(windowTexture);
        }
    }
}
