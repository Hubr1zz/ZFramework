using Core;
using GameplayBase;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Settlement
{
    /// <summary>症状成长 View：让猎人在营地逐年内化弱点，或以勇气和成长将其克服。</summary>
    public sealed class PlayableSymptomGrowthView : MonoBehaviour
    {
        private const int WindowId = 68025;
        private GameManager manager;
        private PlayableSymptomGrowthService service;
        private HunterInstance selectedHunter;
        private bool visible;
        private string statusText = string.Empty;
        private Vector2 scrollPosition;
        private GUIStyle windowStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle mutedStyle;
        private GUIStyle statusStyle;
        private Texture2D windowTexture;

        public void Initialize(GameManager gameManager, PlayableSymptomCatalog catalog)
        {
            manager = gameManager;
            service = new PlayableSymptomGrowthService(() => manager?.SettlementData, catalog);
        }

        private void OnGUI()
        {
            if (manager == null || manager.CurrentGamePhase != GamePhase.Settlement || manager.SettlementData == null) return;

            EnsureStyles();
            int previousDepth = GUI.depth;
            GUI.depth = -830;
            if (visible)
            {
                GUI.Button(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, GUIStyle.none);
                GUI.Window(WindowId, GetWindowRect(), DrawWindow, "面对症状", windowStyle);
            }
            else
                DrawEntryButton();
            GUI.depth = previousDepth;
        }

        private void DrawEntryButton()
        {
            bool actionable = service.HasActionableHunter();
            GUI.enabled = actionable;
            if (GUI.Button(new Rect((Screen.width - 300f) * 0.5f, 150f, 300f, 36f), actionable ? "面对症状" : "营地中无人受症状困扰"))
                Open();
            GUI.enabled = true;
        }

        private Rect GetWindowRect()
        {
            float width = Mathf.Min(680f, Screen.width - 48f);
            float height = Mathf.Min(620f, Screen.height - 48f);
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(10f);
            GUILayout.Label("弱点不会凭空消失", titleStyle);
            GUILayout.Label("每年可消耗意志面对一次症状。内化会保留弱点并获得补偿；达到足够勇气后，也可消耗成长将其克服。", bodyStyle);
            GUILayout.Space(10f);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            if (selectedHunter == null || !selectedHunter.IsAvailable)
                DrawHunterSelection();
            else
                DrawSymptoms();
            GUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(statusText))
                GUILayout.Label(statusText, statusStyle);
            if (selectedHunter != null && GUILayout.Button("选择其他猎人", GUILayout.Height(32f)))
            {
                selectedHunter = null;
                statusText = string.Empty;
            }
            if (GUILayout.Button("离开", GUILayout.Height(38f)))
                Close();
        }

        private void DrawHunterSelection()
        {
            GUILayout.Label("选择需要面对自身弱点的猎人", bodyStyle);
            bool found = false;
            foreach (HunterInstance hunter in manager.SettlementData.GetAvailableHunters())
            {
                int symptomCount = service.GetSymptoms(hunter).Count;
                if (symptomCount == 0) continue;
                found = true;
                if (GUILayout.Button($"{hunter.Name}　症状 {symptomCount}　意志 {hunter.Willpower}/{hunter.WillpowerMax}　勇气 {hunter.Courage}　成长 {hunter.UnspentGrowth}", GUILayout.Height(40f)))
                {
                    selectedHunter = hunter;
                    statusText = string.Empty;
                }
            }
            if (!found)
                GUILayout.Label("没有可处理的已配置症状。", mutedStyle);
        }

        private void DrawSymptoms()
        {
            GUILayout.Label(selectedHunter.Name, titleStyle);
            GUILayout.Label($"意志 {selectedHunter.Willpower}/{selectedHunter.WillpowerMax}　勇气 {selectedHunter.Courage}　成长 {selectedHunter.UnspentGrowth}", mutedStyle);
            foreach (SymptomDefinition definition in service.GetSymptoms(selectedHunter))
            {
                HunterSymptomState state = HunterSymptomRules.Find(selectedHunter, definition.Id);
                GUILayout.Space(10f);
                GUILayout.Label(definition.DisplayName, bodyStyle);
                GUILayout.Label(definition.Description, mutedStyle);
                GUILayout.Label($"内化进度 {state.InternalizationProgress}/{definition.InternalizationThreshold}{(state.IsInternalized ? "　·　已内化" : string.Empty)}", mutedStyle);

                GUI.enabled = !state.IsInternalized && state.LastReflectionYear != manager.SettlementData.CurrentYear && selectedHunter.Willpower >= definition.ReflectionWillpowerCost;
                if (GUILayout.Button($"面对并内化（意志 -{definition.ReflectionWillpowerCost}）", GUILayout.Height(34f)))
                    Internalize(definition);
                GUI.enabled = selectedHunter.Courage >= definition.OvercomeCourageRequirement && selectedHunter.UnspentGrowth >= definition.OvercomeGrowthCost;
                if (GUILayout.Button($"克服（勇气需 {definition.OvercomeCourageRequirement}，成长 -{definition.OvercomeGrowthCost}）", GUILayout.Height(34f)))
                    Overcome(definition);
                GUI.enabled = true;
            }
        }

        private void Internalize(SymptomDefinition definition)
        {
            if (!service.TryInternalize(selectedHunter, definition, out statusText)) return;
            HunterSymptomState state = HunterSymptomRules.Find(selectedHunter, definition.Id);
            statusText = state.IsInternalized ? $"{selectedHunter.Name} 已将“{definition.DisplayName}”内化为新的力量。" : $"{selectedHunter.Name} 面对了“{definition.DisplayName}”（{state.InternalizationProgress}/{definition.InternalizationThreshold}）。";
            manager.SaveSettlementProgress();
        }

        private void Overcome(SymptomDefinition definition)
        {
            if (!service.TryOvercome(selectedHunter, definition, out statusText)) return;
            statusText = $"{selectedHunter.Name} 已克服“{definition.DisplayName}”，其负面影响消失。";
            manager.SaveSettlementProgress();
        }

        private void Open()
        {
            visible = true;
            selectedHunter = null;
            statusText = string.Empty;
            scrollPosition = Vector2.zero;
        }

        private void Close()
        {
            visible = false;
            selectedHunter = null;
            statusText = string.Empty;
        }

        private void EnsureStyles()
        {
            if (windowStyle != null) return;
            windowTexture = new Texture2D(1, 1);
            windowTexture.SetPixel(0, 0, new Color(0.025f, 0.018f, 0.015f, 0.985f));
            windowTexture.Apply();
            windowStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 18, 18), normal = { background = windowTexture } };
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.96f, 0.76f, 0.36f) } };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, normal = { textColor = new Color(0.84f, 0.84f, 0.82f) } };
            mutedStyle = new GUIStyle(bodyStyle) { fontSize = 13, normal = { textColor = new Color(0.64f, 0.66f, 0.68f) } };
            statusStyle = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.72f, 0.9f, 0.62f) } };
        }

        private void OnDestroy()
        {
            if (windowTexture != null)
                Destroy(windowTexture);
        }
    }
}
