using GameLogic;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Presentation
{
    [Window(UILayer.Top, "PhysicalInteractionScreen", fullScreen: false)]
    public sealed class PhysicalInteractionScreenWindow : UIWindow
    {
        [SerializeField] private PhysicalInteractionScreenView view;

        public PhysicalInteractionScreenView View => view;

        protected override void BindMemberProperty()
        {
            view = gameObject.GetComponent<PhysicalInteractionScreenView>();
            if (view == null) throw new MissingComponentException($"[{nameof(PhysicalInteractionScreenWindow)}] Prefab 根物体缺少 View。");
            view.BindWindow(this);
        }

        public void CloseOwnedWindow()
        {
            GameModule.UI.CloseUI<PhysicalInteractionScreenWindow>();
        }

        protected override void OnDestroy()
        {
            if (view != null) view.NotifyWindowClosed();
            view = null;
            base.OnDestroy();
        }
    }
}
