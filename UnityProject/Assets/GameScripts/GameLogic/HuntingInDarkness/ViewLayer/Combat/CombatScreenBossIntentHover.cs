using UnityEngine;
using UnityEngine.EventSystems;

namespace HuntingInDarkness.ViewLayer.Combat
{
    public sealed class CombatScreenBossIntentHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private CombatScreenView view;

        public void ValidateReferences()
        {
            if (view == null) throw new MissingReferenceException($"[{nameof(CombatScreenBossIntentHover)}] 缺少 CombatScreenView 引用。");
        }

        public void OnPointerEnter(PointerEventData eventData) => view.ShowBossIntentPreview();
        public void OnPointerExit(PointerEventData eventData) => view.HideBossIntentPreview();
    }
}
