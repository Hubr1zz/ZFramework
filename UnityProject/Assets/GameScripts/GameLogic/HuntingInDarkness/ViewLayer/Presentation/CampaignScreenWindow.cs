using GameLogic;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Presentation
{
    [Window(UILayer.UI, "CampaignScreen", fullScreen: false)]
    public sealed class CampaignScreenWindow : UIWindow
    {
        [SerializeField] private CampaignScreenView view;

        public CampaignScreenView View => view;

        protected override void BindMemberProperty()
        {
            view = gameObject.GetComponent<CampaignScreenView>();
            if (view == null) throw new MissingComponentException($"[{nameof(CampaignScreenWindow)}] Prefab 根物体缺少 View。");
            view.BindWindow(this);
        }

        public void CloseOwnedWindow() => GameModule.UI.CloseUI<CampaignScreenWindow>();

        protected override void OnDestroy()
        {
            if (view != null) view.NotifyWindowClosed();
            view = null;
            base.OnDestroy();
        }
    }
}
