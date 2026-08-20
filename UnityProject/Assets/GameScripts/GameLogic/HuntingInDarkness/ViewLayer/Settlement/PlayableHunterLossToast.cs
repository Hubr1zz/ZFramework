using System.Collections.Generic;
using Core;
using GameplayBase;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Settlement
{
    /// <summary>在营地阶段非阻塞展示猎人永久死亡及其激励结果。</summary>
    public sealed class PlayableHunterLossToast : MonoBehaviour
    {
        private const float VisibleSeconds = 8f;
        private readonly Queue<string> pendingMessages = new();
        private GameManager manager;
        private string message;
        private float hideAt;
        private GUIStyle style;

        public void Initialize(GameManager gameManager)
        {
            if (manager != null)
                return;
            manager = gameManager;
            EventBus.Subscribe<HunterDiedEvent>(OnHunterDied);
        }

        private void OnGUI()
        {
            if (manager == null || manager.CurrentGamePhase != GamePhase.Settlement)
                return;
            if (string.IsNullOrEmpty(message) || Time.unscaledTime >= hideAt)
            {
                if (pendingMessages.Count == 0)
                {
                    message = string.Empty;
                    return;
                }
                message = pendingMessages.Dequeue();
                hideAt = Time.unscaledTime + VisibleSeconds;
            }
            style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                wordWrap = true
            };
            GUI.Box(new Rect((Screen.width - 560f) * 0.5f, 98f, 560f, 78f), message, style);
        }

        private void OnHunterDied(HunterDiedEvent evt)
        {
            string nextMessage = $"{evt.HunterName} 没能从黑暗中回来";
            if (!string.IsNullOrWhiteSpace(evt.CauseText))
                nextMessage += $"\n{evt.CauseText}";
            if (evt.InspiredHunterCount > 0 && evt.GrowthPerHunter > 0)
                nextMessage += $"\n营地记住了这次失去：{evt.InspiredHunterCount} 名猎人各获得 {evt.GrowthPerHunter} 点成长";
            pendingMessages.Enqueue(nextMessage);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<HunterDiedEvent>(OnHunterDied);
        }
    }
}
