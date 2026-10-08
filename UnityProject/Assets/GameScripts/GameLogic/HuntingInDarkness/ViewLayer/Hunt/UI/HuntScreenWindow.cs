using GameLogic;
using UnityEngine;

namespace UI.Hunt
{
    [Window(UILayer.UI, "HuntScreen", fullScreen: false)]
    public sealed class HuntScreenWindow : UIWindow
    {
        [SerializeField] private HuntScreenView view;

        public HuntScreenView View => view;

        protected override void BindMemberProperty()
        {
            view = gameObject.GetComponent<HuntScreenView>();
            if (view == null) throw new MissingComponentException($"[{nameof(HuntScreenWindow)}] Prefab 根物体缺少 View。");
            view.BindWindow(this);
        }

        public void CloseOwnedWindow() => GameModule.UI.CloseUI<HuntScreenWindow>();

        protected override void OnDestroy()
        {
            if (view != null) view.NotifyWindowClosed();
            view = null;
            base.OnDestroy();
        }
    }
}
