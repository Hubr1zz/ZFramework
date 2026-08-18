using Core;
using GameplayBase;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Settlement
{
    /// <summary>营火招募 View：选择模板、命名并提交一次长期招募。</summary>
    public sealed class PlayableRecruitmentView : MonoBehaviour
    {
        private const int WindowId = 68023;
        private GameManager manager;
        private PlayableRecruitmentService service;
        private bool visible;
        private int selectedTemplateIndex;
        private string requestedName = string.Empty;
        private string statusText = string.Empty;
        private HunterInstance recruitedHunter;
        private GUIStyle windowStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle mutedStyle;
        private GUIStyle successStyle;
        private Texture2D windowTexture;

        public void Initialize(GameManager gameManager, PlayableSettlementContentCatalog catalog)
        {
            manager = gameManager;
            service = new PlayableRecruitmentService(() => manager?.SettlementData, () => manager?.SettlementHunters, catalog?.RecruitmentTemplates, catalog?.RecruitmentCostItem, catalog?.RecruitmentCost ?? 0, catalog?.MaximumLivingHunters ?? 1);
        }

        private void OnGUI()
        {
            if (manager == null || manager.CurrentGamePhase != GamePhase.Settlement || manager.SettlementData == null) return;

            EnsureStyles();
            int previousDepth = GUI.depth;
            GUI.depth = -850;
            if (visible)
            {
                GUI.Button(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, GUIStyle.none);
                GUI.Window(WindowId, GetWindowRect(), DrawWindow, "营火招募", windowStyle);
            }
            else
                DrawRecruitmentButton();
            GUI.depth = previousDepth;
        }

        private void DrawRecruitmentButton()
        {
            bool canRecruit = service.CanRecruit(out string reason);
            int cost = service.GetCurrentCost();
            string costLabel = cost == 0 ? "免费援助" : $"{service.CostItemName} ×{cost}";
            bool campEmpty = manager.SettlementData.GetAvailableHunters().Count == 0;
            string label = campEmpty ? $"营火将熄灭 · 呼唤幸存者（{costLabel}）" : $"营火招募（{costLabel}）";
            var buttonRect = new Rect((Screen.width - 300f) * 0.5f, 18f, 300f, 38f);

            GUI.enabled = canRecruit;
            if (GUI.Button(buttonRect, canRecruit ? label : reason))
                Open();
            GUI.enabled = true;
        }

        private Rect GetWindowRect()
        {
            float width = Mathf.Min(600f, Screen.width - 48f);
            float height = Mathf.Min(540f, Screen.height - 48f);
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(10f);
            if (recruitedHunter != null)
            {
                DrawSuccess();
                return;
            }

            GUILayout.Label("给陌生人一个留下的理由", titleStyle);
            GUILayout.Label("选择一段生存经历，并亲自为这名猎人取名。这个名字会永久写入营地年鉴。", bodyStyle);
            int cost = service.GetCurrentCost();
            GUILayout.Label(cost == 0 ? "营地已无人守火：本次援助不消耗物资。" : $"接纳成本：{service.CostItemName} ×{cost}　·　每年限一次", mutedStyle);
            GUILayout.Space(10f);

            if (service.Templates.Count == 0)
            {
                GUILayout.Label("当前没有配置可招募的猎人模板。", mutedStyle);
            }
            else
            {
                for (int index = 0; index < service.Templates.Count; index++)
                {
                    HunterData template = service.Templates[index];
                    bool selected = index == selectedTemplateIndex;
                    if (GUILayout.Toggle(selected, $"{(selected ? "◆" : "◇")} {template.hunterName}", GUI.skin.button, GUILayout.Height(34f)))
                        selectedTemplateIndex = index;
                }
                DrawSelectedTemplate();
            }

            GUILayout.Space(10f);
            GUILayout.Label($"名字（最多 {RecruitmentRules.MaximumNameLength} 个字符）", bodyStyle);
            requestedName = GUILayout.TextField(requestedName, RecruitmentRules.MaximumNameLength, GUILayout.Height(32f));
            if (!string.IsNullOrEmpty(statusText))
                GUILayout.Label(statusText, mutedStyle);
            GUILayout.FlexibleSpace();

            GUI.enabled = service.Templates.Count > 0 && service.CanRecruit(out _);
            if (GUILayout.Button("接纳并记入年鉴", GUILayout.Height(42f)))
                Recruit();
            GUI.enabled = true;
            if (GUILayout.Button("暂不接纳", GUILayout.Height(34f)))
                Close();
        }

        private void DrawSelectedTemplate()
        {
            if (selectedTemplateIndex < 0 || selectedTemplateIndex >= service.Templates.Count) return;

            HunterData template = service.Templates[selectedTemplateIndex];
            HunterCombatStats stats = template.initialStats;
            GUILayout.Label($"力量 {stats.strength}　技巧 {stats.accuracy}　敏捷 {stats.evasion}　移动 {stats.movement}　意志 {template.initialWillpower}", mutedStyle);
            if (template.startingTraits.Count > 0)
                GUILayout.Label($"特性：{string.Join("、", template.startingTraits)}", mutedStyle);
        }

        private void DrawSuccess()
        {
            GUILayout.Label("新的名字留在了火光里", titleStyle);
            GUILayout.Space(18f);
            GUILayout.Label($"{recruitedHunter.Name} 已加入营地。", successStyle);
            GUILayout.Label("你现在可以为其分配装备，并选择加入下一次狩猎。", bodyStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("回到营地", GUILayout.Height(42f)))
                Close();
        }

        private void Recruit()
        {
            HunterData template = selectedTemplateIndex >= 0 && selectedTemplateIndex < service.Templates.Count ? service.Templates[selectedTemplateIndex] : null;
            if (!service.TryRecruit(template, requestedName, out recruitedHunter, out statusText)) return;
            manager.SaveSettlementProgress();
        }

        private void Open()
        {
            visible = true;
            selectedTemplateIndex = 0;
            requestedName = string.Empty;
            statusText = string.Empty;
            recruitedHunter = null;
        }

        private void Close()
        {
            visible = false;
            requestedName = string.Empty;
            statusText = string.Empty;
            recruitedHunter = null;
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
                fontSize = 23,
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
            successStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
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
