using System;
using Core;
using GameplayBase;
using HuntingInDarkness.GameCore.Combat;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Combat
{
    /// <summary>决战阶段持续展示 Boss 全局生命，不承担任何战斗结算。</summary>
    public sealed class PlayableBossVitalityView : MonoBehaviour
    {
        private Func<GamePhase> phaseProvider;
        private Func<IBossState> bossProvider;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle healthStyle;
        private Texture2D panelTexture;

        internal void Initialize(Func<GamePhase> currentPhase, Func<IBossState> currentBoss)
        {
            phaseProvider = currentPhase;
            bossProvider = currentBoss;
        }

        private void OnGUI()
        {
            if (phaseProvider?.Invoke() != GamePhase.BossFight) return;
            IBossState boss = bossProvider?.Invoke();
            if (boss is not IBossVitalityState vitality) return;

            EnsureStyles();
            const float width = 320f;
            var area = new Rect((Screen.width - width) * 0.5f, 14f, width, 78f);
            GUILayout.BeginArea(area, panelStyle);
            GUILayout.Label(boss.Name, titleStyle);
            GUILayout.Label($"生命 {vitality.CurrentHealth}/{vitality.MaxHealth}", healthStyle);
            Rect barRect = GUILayoutUtility.GetRect(width - 28f, 12f);
            GUI.Box(barRect, GUIContent.none);
            float ratio = vitality.MaxHealth > 0 ? (float)vitality.CurrentHealth / vitality.MaxHealth : 0f;
            var fillRect = new Rect(barRect.x + 2f, barRect.y + 2f, Mathf.Max(0f, (barRect.width - 4f) * ratio), barRect.height - 4f);
            EditorGui.DrawRect(fillRect, new Color(0.65f, 0.12f, 0.08f, 1f));
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (panelStyle != null) return;

            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0.06f, 0.025f, 0.02f, 0.94f));
            panelTexture.Apply();
            panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 8, 8), normal = { background = panelTexture } };
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.96f, 0.72f, 0.46f) } };
            healthStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, normal = { textColor = Color.white } };
        }

        private void OnDestroy()
        {
            if (panelTexture != null)
                Destroy(panelTexture);
        }

        private static class EditorGui
        {
            private static Texture2D texture;

            public static void DrawRect(Rect position, Color color)
            {
                texture ??= Texture2D.whiteTexture;
                Color previous = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(position, texture);
                GUI.color = previous;
            }
        }
    }
}
