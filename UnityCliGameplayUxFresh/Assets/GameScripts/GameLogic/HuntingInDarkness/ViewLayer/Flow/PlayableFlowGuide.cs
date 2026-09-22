using Core;
using HuntingInDarkness.Bootstrap;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Flow
{
    /// <summary>
    /// 可游玩切片的开场叙事。常驻阶段指引已迁移到实体桌面，不提供玩法命令旁路。
    /// </summary>
    public sealed class PlayableFlowGuide : MonoBehaviour
    {
        private GameManager manager;
        private bool openingVisible;
        private string openingNarrative;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;

        public void Initialize(GameManager gameManager, PlayableBootstrapSettings settings)
        {
            manager = gameManager;
            openingVisible = settings != null && settings.ShowOpeningNarrative;
            openingNarrative = settings != null ? settings.OpeningNarrative : string.Empty;
            enabled = openingVisible;
        }

        public void SkipOpeningNarrative()
        {
            openingVisible = false;
            enabled = false;
        }

        private void OnGUI()
        {
            if (manager == null || !openingVisible) return;
            EnsureStyles();
            DrawOpeningNarrative();
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
                SkipOpeningNarrative();
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
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = new Color(0.86f, 0.88f, 0.9f) }
            };
        }
    }
}
