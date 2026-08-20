using Core;
using GameplayBase;
using HuntingInDarkness.Bootstrap;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Flow
{
    /// <summary>
    /// 可游玩切片的轻量流程引导。只提交 GameManager 公共命令，不持有玩法权威状态。
    /// </summary>
    public sealed class PlayableFlowGuide : MonoBehaviour
    {
        private const float PanelWidth = 360f;
        private GameManager manager;
        private bool visible = true;
        private bool openingVisible;
        private bool settlementHudEnabled;
        private string openingNarrative;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle phaseStyle;
        private GUIStyle panelStyle;
        private Texture2D panelTexture;

        public void Initialize(GameManager gameManager, PlayableBootstrapSettings settings)
        {
            manager = gameManager;
            openingVisible = settings != null && settings.ShowOpeningNarrative;
            settlementHudEnabled = settings != null && settings.ShowSettlementHud;
            openingNarrative = settings != null ? settings.OpeningNarrative : string.Empty;
        }

        public void SkipOpeningNarrative() => openingVisible = false;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
                visible = !visible;
        }

        private void OnGUI()
        {
            if (manager == null) return;

            EnsureStyles();
            if (openingVisible)
            {
                DrawOpeningNarrative();
                return;
            }

            if (!visible)
            {
                if (GUI.Button(new Rect(16f, 16f, 170f, 34f), "显示狩猎引导 [Tab]"))
                    visible = true;
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, PanelWidth, Screen.height - 32f), panelStyle);
            GUILayout.Label("黑暗狩猎", titleStyle);
            GUILayout.Label("第一次狩猎", bodyStyle);
            GUILayout.Space(10f);
            GUILayout.Label(GetPhaseTitle(), phaseStyle);
            GUILayout.Label(GetObjective(), bodyStyle);
            GUILayout.Space(12f);
            DrawPhaseActions();
            GUILayout.FlexibleSpace();
            GUILayout.Label("鼠标点击地块、猎人与卡牌。\nWASD 或中键移动视角，滚轮缩放。Tab 隐藏引导。", bodyStyle);
            if (GUILayout.Button("隐藏引导 [Tab]", GUILayout.Height(32f)))
                visible = false;
            GUILayout.EndArea();
        }

        private void DrawPhaseActions()
        {
            switch (manager.CurrentGamePhase)
            {
                case GamePhase.Settlement:
                    if (settlementHudEnabled)
                        GUILayout.Label("在右侧营地面板选择 1—4 名猎人后出发。", bodyStyle);
                    else if (GUILayout.Button("整理行装，出发狩猎", GUILayout.Height(42f)))
                        manager.TransitionToPhase(GamePhase.Hunt);
                    break;
                case GamePhase.Hunt:
                    GUILayout.Label("点击蓝色地块翻开地图。需要结束探索时，使用地图边缘的实体回营卡。", bodyStyle);
                    break;
                case GamePhase.BossFight:
                    if (GUILayout.Button("结束猎人回合", GUILayout.Height(42f)))
                        manager.OnEndTurn();
                    break;
            }
        }

        private string GetPhaseTitle()
        {
            return manager.CurrentGamePhase switch
            {
                GamePhase.Settlement => "1 / 3  营地",
                GamePhase.Hunt => "2 / 3  狩猎",
                GamePhase.BossFight => "3 / 3  决战",
                _ => manager.CurrentGamePhase.ToString()
            };
        }

        private string GetObjective()
        {
            return manager.CurrentGamePhase switch
            {
                GamePhase.Settlement => "查看桌面上的猎人与物资，然后踏入黑暗。",
                GamePhase.Hunt => "翻开六边形地块，探索资源，并找到怪物的藏身处。",
                GamePhase.BossFight => "选择猎人并使用行动卡。所有人行动后，结束回合让怪物行动。",
                _ => string.Empty
            };
        }

        private void DrawOpeningNarrative()
        {
            var width = Mathf.Min(620f, Screen.width - 48f);
            var height = 270f;
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height), GUI.skin.window);
            GUILayout.Space(16f);
            GUILayout.Label("黑暗中的苏醒", titleStyle);
            GUILayout.Space(14f);
            GUILayout.Label(openingNarrative, bodyStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("记住同伴的名字，活下去", GUILayout.Height(46f)))
                openingVisible = false;
            GUILayout.Space(12f);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.96f, 0.82f, 0.52f) }
            };
            phaseStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = new Color(0.86f, 0.88f, 0.9f) }
            };
            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0.025f, 0.035f, 0.055f, 0.96f));
            panelTexture.Apply();
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(12, 12, 12, 12),
                normal = { background = panelTexture }
            };
        }

        private void OnDestroy()
        {
            if (panelTexture != null)
                Destroy(panelTexture);
        }
    }
}
