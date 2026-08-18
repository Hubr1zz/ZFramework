using Core;
using GameplayBase;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Settlement
{
    /// <summary>返回营地后短暂展示本场武器熟练度成长，不阻塞结算流程。</summary>
    public sealed class PlayableWeaponMasteryToast : MonoBehaviour
    {
        private const float VisibleSeconds = 7f;
        private GameManager manager;
        private string message;
        private float hideAt;
        private GUIStyle style;

        public void Initialize(GameManager gameManager)
        {
            manager = gameManager;
            EventBus.Subscribe<WeaponMasteryChangedEvent>(OnWeaponMasteryChanged);
        }

        private void OnGUI()
        {
            if (manager == null || manager.CurrentGamePhase != GamePhase.Settlement || string.IsNullOrEmpty(message) || Time.unscaledTime >= hideAt) return;
            style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                wordWrap = true
            };
            GUI.Box(new Rect((Screen.width - 520f) * 0.5f, 24f, 520f, 66f), message, style);
        }

        private void OnWeaponMasteryChanged(WeaponMasteryChangedEvent evt)
        {
            string masteryName = string.IsNullOrWhiteSpace(evt.MasteryName) ? evt.WeaponName : evt.MasteryName;
            string milestone = evt.ReachedMilestoneNames != null && evt.ReachedMilestoneNames.Length > 0 ? $"\n达成：{string.Join("、", evt.ReachedMilestoneNames)}" : string.Empty;
            string action = evt.Source == WeaponMasteryGainSource.Training ? "完成训练" : $"使用 {evt.WeaponName} 造成有效伤害";
            message = $"{evt.HunterName} {action}\n{masteryName}熟练度 {evt.OldValue} → {evt.NewValue}{milestone}";
            hideAt = Time.unscaledTime + VisibleSeconds;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<WeaponMasteryChangedEvent>(OnWeaponMasteryChanged);
        }
    }
}
