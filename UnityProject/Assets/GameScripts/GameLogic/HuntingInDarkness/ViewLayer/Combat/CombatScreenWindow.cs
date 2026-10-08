using GameLogic;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Combat
{
    [Window(UILayer.UI, "CombatScreen", fullScreen: false)]
    public sealed class CombatScreenWindow : UIWindow
    {
        private CombatScreenView view;

        public CombatScreenView View => view;

        public void Bind(HuntingInDarkness.Combat.PlayableCombatSession session)
        {
            if (view == null) throw new MissingReferenceException($"[{nameof(CombatScreenWindow)}] prefab 根物件必须绑定 {nameof(CombatScreenView)}。");
            view.Bind(session);
        }

        public void CloseForSession()
        {
            view?.CancelPendingPrompt();
            Close();
        }

        protected override void BindMemberProperty()
        {
            base.BindMemberProperty();
            view = gameObject.GetComponent<CombatScreenView>();
            if (view == null) throw new MissingComponentException($"[{nameof(CombatScreenWindow)}] 根物件缺少 {nameof(CombatScreenView)}。");
            view.ValidateReferences();
        }

        protected override void OnDestroy()
        {
            view?.CancelPendingPrompt();
            base.OnDestroy();
        }
    }
}
