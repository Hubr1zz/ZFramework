using System;
using GameplayBase;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuntingInDarkness.ViewLayer.Combat
{
    public sealed class CombatScreenPartWidget : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text healthLabel;
        [SerializeField] private TMP_Text toughnessLabel;

        public void Bind(string partName, string health, string toughness)
        {
            if (nameLabel == null) throw new MissingReferenceException($"[{nameof(CombatScreenPartWidget)}] nameLabel 未绑定。");
            if (healthLabel == null) throw new MissingReferenceException($"[{nameof(CombatScreenPartWidget)}] healthLabel 未绑定。");
            if (toughnessLabel == null) throw new MissingReferenceException($"[{nameof(CombatScreenPartWidget)}] toughnessLabel 未绑定。");
            nameLabel.text = partName;
            healthLabel.text = health;
            toughnessLabel.text = toughness;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }
        }

        public void BindSelectable(string partName, string health, string toughness, Action onSelect)
        {
            if (button == null) throw new MissingReferenceException($"[{nameof(CombatScreenPartWidget)}] button 未绑定。");
            Bind(partName, health, toughness);
            button.onClick.AddListener(() => onSelect?.Invoke());
            button.interactable = true;
        }
    }
}
