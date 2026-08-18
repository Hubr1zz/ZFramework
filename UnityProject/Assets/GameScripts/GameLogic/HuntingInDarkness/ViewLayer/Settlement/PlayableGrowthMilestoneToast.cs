using System.Collections.Generic;
using Core;
using GameplayBase;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Settlement
{
    /// <summary>非阻塞展示知识与胆识里程碑，让长期成长获得明确回报。</summary>
    public sealed class PlayableGrowthMilestoneToast : MonoBehaviour
    {
        private const float VisibleSeconds = 8f;
        private readonly Queue<string> pendingMessages = new();
        private GameManager manager;
        private string message;
        private float hideAt;
        private GUIStyle style;

        public void Initialize(GameManager gameManager)
        {
            if (manager != null) return;
            manager = gameManager;
            EventBus.Subscribe<HunterGrowthMilestoneReachedEvent>(OnMilestoneReached);
        }

        private void OnGUI()
        {
            if (manager == null || manager.CurrentGamePhase != GamePhase.Settlement) return;
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
            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 18, wordWrap = true };
            GUI.Box(new Rect((Screen.width - 560f) * 0.5f, Screen.height - 104f, 560f, 80f), message, style);
        }

        private void OnMilestoneReached(HunterGrowthMilestoneReachedEvent evt)
        {
            string attributeName = evt.Attribute == HunterGrowthChoice.Courage ? "胆识" : "知识";
            string nextMessage = $"{evt.HunterName} 达成{attributeName} {evt.Threshold}：{evt.DisplayName}";
            if (!string.IsNullOrWhiteSpace(evt.Description)) nextMessage += $"\n{evt.Description}";
            pendingMessages.Enqueue(nextMessage);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<HunterGrowthMilestoneReachedEvent>(OnMilestoneReached);
        }
    }
}
