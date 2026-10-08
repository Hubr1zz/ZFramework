using System;
using GameplayBase;
using GameplayBase.CombatSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuntingInDarkness.ViewLayer.Combat
{
    public sealed class CombatScreenHunterWidget : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private TMP_Text injuryLabel;
        [SerializeField] private Image selectionMark;

        public void Bind(CharacterRuntimeData character, bool selected, string state, string injury, Action<int> onSelect)
        {
            if (button == null) throw new MissingReferenceException($"[{nameof(CombatScreenHunterWidget)}] button 未绑定。");
            if (nameLabel == null) throw new MissingReferenceException($"[{nameof(CombatScreenHunterWidget)}] nameLabel 未绑定。");
            if (stateLabel == null) throw new MissingReferenceException($"[{nameof(CombatScreenHunterWidget)}] stateLabel 未绑定。");
            if (injuryLabel == null) throw new MissingReferenceException($"[{nameof(CombatScreenHunterWidget)}] injuryLabel 未绑定。");
            if (selectionMark == null) throw new MissingReferenceException($"[{nameof(CombatScreenHunterWidget)}] selectionMark 未绑定。");
            int characterId = character.Id;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelect?.Invoke(characterId));
            nameLabel.text = character.Name;
            stateLabel.text = state;
            injuryLabel.text = injury;
            selectionMark.enabled = selected;
        }
    }
}
