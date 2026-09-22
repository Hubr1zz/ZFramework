using Core;
using HuntingInDarkness.ViewLayer.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Bootstrap
{
    /// <summary>安装营地必须存在的世界空间出猎与事件输入端口。</summary>
    public static class PlayableSettlementWorldSpacePortInstaller
    {
        public static void EnsureInstalled(GameObject host, GameManager manager, PlayableBootstrapSettings settings)
        {
            if (host == null) return;

            PlayableHuntDestinationView destinationView = host.GetComponent<PlayableHuntDestinationView>() ?? host.AddComponent<PlayableHuntDestinationView>();
            destinationView.Initialize(manager, settings?.HuntDestinations);

            PlayableSettlementEventView eventView = host.GetComponent<PlayableSettlementEventView>() ?? host.AddComponent<PlayableSettlementEventView>();
            eventView.Initialize(manager);
        }
    }
}
