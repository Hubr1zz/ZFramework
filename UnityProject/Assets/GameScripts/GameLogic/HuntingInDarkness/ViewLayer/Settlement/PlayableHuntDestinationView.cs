using System.Collections.Generic;
using Core;
using GameplayBase;
using HuntingInDarkness.Hunt;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Settlement
{
    /// <summary>在组队与正式出发之间插入一次可取消的目的地选择。</summary>
    public sealed class PlayableHuntDestinationView : MonoBehaviour
    {
        private const int WindowId = 68025;
        private readonly List<int> pendingHunterIds = new();
        private readonly List<PlayableHuntDestination> availableDestinations = new();
        private GameManager manager;
        private PlayableSettlementHud settlementHud;
        private PlayableHuntDestinationCatalog catalog;
        private bool visible;
        private int selectedIndex;
        private Vector2 scrollPosition;
        private string statusText = string.Empty;
        private GUIStyle windowStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle mutedStyle;
        private GUIStyle selectedStyle;
        private Texture2D windowTexture;

        public void Initialize(GameManager gameManager, PlayableSettlementHud hud, PlayableHuntDestinationCatalog destinationCatalog)
        {
            manager = gameManager;
            settlementHud = hud;
            catalog = destinationCatalog;
            if (settlementHud != null)
                settlementHud.DepartureRequested += RequestDeparture;
        }

        private void OnGUI()
        {
            if (!visible || manager == null || manager.CurrentGamePhase != GamePhase.Settlement) return;

            EnsureStyles();
            int previousDepth = GUI.depth;
            GUI.depth = -920;
            GUI.Button(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, GUIStyle.none);
            GUI.Window(WindowId, GetWindowRect(), DrawWindow, "选择狩猎目的地", windowStyle);
            GUI.depth = previousDepth;
        }

        private Rect GetWindowRect()
        {
            float width = Mathf.Min(680f, Screen.width - 48f);
            float height = Mathf.Min(580f, Screen.height - 48f);
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(10f);
            GUILayout.Label("决定这次要把火光留在身后多远", titleStyle);
            GUILayout.Label($"出发人数：{pendingHunterIds.Count}。地区会改变地图地块与常见资源；途中仍可主动撤退。", bodyStyle);
            GUILayout.Space(12f);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(Mathf.Min(330f, Screen.height * 0.45f)));
            for (int index = 0; index < availableDestinations.Count; index++)
            {
                PlayableHuntDestination destination = availableDestinations[index];
                bool selected = index == selectedIndex;
                GUILayout.BeginVertical(selected ? selectedStyle : GUI.skin.box);
                if (GUILayout.Toggle(selected, $"{(selected ? "◆" : "◇")} {destination.DisplayName}", GUI.skin.button, GUILayout.Height(36f)))
                    selectedIndex = index;
                GUILayout.Label(destination.Description, bodyStyle);
                GUILayout.Label($"常见收获：{destination.ResourceHint}　·　风险：{destination.DangerHint}", mutedStyle);
                GUILayout.EndVertical();
                GUILayout.Space(6f);
            }
            GUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(statusText))
                GUILayout.Label(statusText, mutedStyle);
            GUILayout.FlexibleSpace();

            GUI.enabled = availableDestinations.Count > 0;
            if (GUILayout.Button("确认路线并出发", GUILayout.Height(44f)))
                ConfirmDeparture();
            GUI.enabled = true;
            if (GUILayout.Button("返回整备", GUILayout.Height(34f)))
                Close();
        }

        private void RequestDeparture(List<int> hunterIds)
        {
            if (manager?.SettlementData == null || hunterIds == null || hunterIds.Count == 0) return;
            if (catalog == null || !catalog.IsConfigured)
            {
                manager.TryDepartForHunt(new List<int>(hunterIds));
                return;
            }

            pendingHunterIds.Clear();
            pendingHunterIds.AddRange(hunterIds);
            availableDestinations.Clear();
            if (catalog != null)
                availableDestinations.AddRange(catalog.GetAvailable(manager.SettlementData.CurrentYear));
            selectedIndex = FindActiveDestinationIndex();
            scrollPosition = Vector2.zero;
            statusText = availableDestinations.Count > 0 ? string.Empty : "当前没有可前往的目的地。";
            visible = true;
        }

        private int FindActiveDestinationIndex()
        {
            PlayableHuntDestination active = PlayableHuntDestinationRuntime.ActiveDestination;
            int index = availableDestinations.IndexOf(active);
            return index >= 0 ? index : 0;
        }

        private void ConfirmDeparture()
        {
            if (selectedIndex < 0 || selectedIndex >= availableDestinations.Count) return;
            if (!PlayableHuntDestinationRuntime.TrySelect(availableDestinations[selectedIndex], manager.SettlementData.CurrentYear, out statusText)) return;
            if (!manager.TryDepartForHunt(new List<int>(pendingHunterIds)))
            {
                statusText = "小队状态已经变化，请返回营地重新整备。";
                return;
            }

            Close();
        }

        private void Close()
        {
            visible = false;
            pendingHunterIds.Clear();
            availableDestinations.Clear();
            statusText = string.Empty;
        }

        private void EnsureStyles()
        {
            if (windowStyle != null) return;

            windowTexture = new Texture2D(1, 1);
            windowTexture.SetPixel(0, 0, new Color(0.02f, 0.025f, 0.03f, 0.99f));
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
                normal = { textColor = new Color(0.82f, 0.9f, 0.72f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                normal = { textColor = new Color(0.84f, 0.85f, 0.82f) }
            };
            mutedStyle = new GUIStyle(bodyStyle)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.64f, 0.68f, 0.7f) }
            };
            selectedStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 8, 8),
                normal = { textColor = new Color(0.84f, 0.93f, 0.75f) }
            };
        }

        private void OnDestroy()
        {
            if (settlementHud != null)
                settlementHud.DepartureRequested -= RequestDeparture;
            if (windowTexture != null)
                Destroy(windowTexture);
        }
    }
}
