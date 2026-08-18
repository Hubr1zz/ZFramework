using Core;
using GameplayBase;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Settlement
{
    /// <summary>营火休养 View：选择负伤猎人与部位，消费配置资源恢复普通生命。</summary>
    public sealed class PlayableHunterRecoveryView : MonoBehaviour
    {
        private const int WindowId = 68024;
        private static readonly HunterBodyPart[] bodyParts = { HunterBodyPart.Head, HunterBodyPart.Torso, HunterBodyPart.Arms, HunterBodyPart.Legs };
        private GameManager manager;
        private PlayableHunterRecoveryService service;
        private HunterInstance selectedHunter;
        private bool visible;
        private string statusText = string.Empty;
        private GUIStyle windowStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle mutedStyle;
        private GUIStyle statusStyle;
        private Texture2D windowTexture;

        public void Initialize(GameManager gameManager, PlayableSettlementContentCatalog catalog)
        {
            manager = gameManager;
            service = new PlayableHunterRecoveryService(() => manager?.SettlementData, catalog?.RecoveryCostItem, catalog?.RecoveryCost ?? 0, catalog?.RecoveryAmount ?? 1);
        }

        private void OnGUI()
        {
            if (manager == null || manager.CurrentGamePhase != GamePhase.Settlement || manager.SettlementData == null) return;

            EnsureStyles();
            int previousDepth = GUI.depth;
            GUI.depth = -840;
            if (visible)
            {
                GUI.Button(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, GUIStyle.none);
                GUI.Window(WindowId, GetWindowRect(), DrawWindow, "营火休养", windowStyle);
            }
            else
                DrawRecoveryButton();
            GUI.depth = previousDepth;
        }

        private void DrawRecoveryButton()
        {
            bool hasWoundedHunter = service.HasRecoverableHunter();
            string costLabel = service.Cost == 0 ? "无需物资" : $"{service.CostItemName} ×{service.Cost}";
            var buttonRect = new Rect((Screen.width - 300f) * 0.5f, 62f, 300f, 36f);
            GUI.enabled = hasWoundedHunter;
            if (GUI.Button(buttonRect, hasWoundedHunter ? $"营火休养（每次 {costLabel}）" : "所有存活猎人均无普通伤势"))
                Open();
            GUI.enabled = true;
        }

        private Rect GetWindowRect()
        {
            float width = Mathf.Min(620f, Screen.width - 48f);
            float height = Mathf.Min(560f, Screen.height - 48f);
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(10f);
            GUILayout.Label("伤口不会在黑暗里自行愈合", titleStyle);
            GUILayout.Label($"每次休养消耗 {FormatCost()}，为一个部位恢复 {service.RecoveryAmount} 点普通生命。永久损伤与症状不会因此消失。", bodyStyle);
            GUILayout.Space(10f);

            if (selectedHunter == null || !selectedHunter.IsAlive)
                DrawHunterSelection();
            else
                DrawBodyPartSelection();

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(statusText))
                GUILayout.Label(statusText, statusStyle);
            if (selectedHunter != null && GUILayout.Button("选择其他猎人", GUILayout.Height(32f)))
            {
                selectedHunter = null;
                statusText = string.Empty;
            }
            if (GUILayout.Button("结束休养", GUILayout.Height(38f)))
                Close();
        }

        private void DrawHunterSelection()
        {
            GUILayout.Label("选择负伤猎人", bodyStyle);
            bool found = false;
            foreach (HunterInstance hunter in manager.SettlementData.GetAvailableHunters())
            {
                if (!IsWounded(hunter)) continue;
                found = true;
                if (GUILayout.Button($"{hunter.Name}　{FormatHealth(hunter)}", GUILayout.Height(38f)))
                {
                    selectedHunter = hunter;
                    statusText = string.Empty;
                }
            }
            if (!found)
                GUILayout.Label("没有需要处理的普通伤势。", mutedStyle);
        }

        private void DrawBodyPartSelection()
        {
            GUILayout.Label(selectedHunter.Name, titleStyle);
            GUILayout.Label(FormatHealth(selectedHunter), mutedStyle);
            GUILayout.Space(8f);
            foreach (HunterBodyPart bodyPart in bodyParts)
            {
                HunterRecoveryRules.GetHealth(selectedHunter, bodyPart, out int currentHealth, out int maximumHealth);
                bool canTreat = service.CanTreat(selectedHunter, bodyPart, out string reason);
                string label = canTreat ? $"治疗{GetBodyPartName(bodyPart)}　{currentHealth}/{maximumHealth} → {Mathf.Min(maximumHealth, currentHealth + service.RecoveryAmount)}/{maximumHealth}" : $"{GetBodyPartName(bodyPart)}　{currentHealth}/{maximumHealth}　·　{reason}";
                GUI.enabled = canTreat;
                if (GUILayout.Button(label, GUILayout.Height(38f)))
                    Treat(bodyPart);
                GUI.enabled = true;
            }
        }

        private void Treat(HunterBodyPart bodyPart)
        {
            if (!service.TryTreat(selectedHunter, bodyPart, out HunterRecoveryResult result, out statusText)) return;

            statusText = $"{selectedHunter.Name} 的{GetBodyPartName(bodyPart)}恢复 {result.RecoveredHealth} 点生命（{result.CurrentHealth}/{result.MaximumHealth}）。";
            manager.SaveSettlementProgress();
        }

        private bool IsWounded(HunterInstance hunter)
        {
            foreach (HunterBodyPart bodyPart in bodyParts)
                if (HunterRecoveryRules.CanRecover(hunter, bodyPart, out _))
                    return true;
            return false;
        }

        private string FormatCost() => service.Cost == 0 ? "无需物资" : $"{service.CostItemName} ×{service.Cost}";

        private static string FormatHealth(HunterInstance hunter)
        {
            return $"头 {hunter.HP.head}/{hunter.MaxHP.head}　躯干 {hunter.HP.body}/{hunter.MaxHP.body}　臂 {hunter.HP.arms}/{hunter.MaxHP.arms}　腿 {hunter.HP.legs}/{hunter.MaxHP.legs}";
        }

        private static string GetBodyPartName(HunterBodyPart bodyPart)
        {
            return bodyPart switch
            {
                HunterBodyPart.Head => "头部",
                HunterBodyPart.Torso => "躯干",
                HunterBodyPart.Arms => "手臂",
                HunterBodyPart.Legs => "腿部",
                _ => "未知部位"
            };
        }

        private void Open()
        {
            visible = true;
            selectedHunter = null;
            statusText = string.Empty;
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
