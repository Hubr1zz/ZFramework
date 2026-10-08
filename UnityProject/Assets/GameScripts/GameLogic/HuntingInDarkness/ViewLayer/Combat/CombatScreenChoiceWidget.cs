using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuntingInDarkness.ViewLayer.Combat
{
    public sealed class CombatScreenChoiceWidget : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text detail;

        public void Bind(string title, string subtitle, bool interactable, Action onClick)
        {
            if (button == null) throw new MissingReferenceException($"[{nameof(CombatScreenChoiceWidget)}] button 未绑定。");
            if (label == null) throw new MissingReferenceException($"[{nameof(CombatScreenChoiceWidget)}] label 未绑定。");
            if (detail == null) throw new MissingReferenceException($"[{nameof(CombatScreenChoiceWidget)}] detail 未绑定。");
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
            button.interactable = interactable;
            label.text = title;
            detail.text = subtitle ?? string.Empty;
        }
    }
}
