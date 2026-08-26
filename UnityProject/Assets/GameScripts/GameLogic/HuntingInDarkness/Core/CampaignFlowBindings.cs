using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.CombatSystem;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Settlement;
using UI;
using UI.Hunt;
using UI.Settlement;
using UnityEngine;

namespace Core
{
    internal delegate bool TryCreateCombatConfigurationDelegate(out PlayableCombatSessionConfiguration configuration, out string reason);

    /// <summary>
    /// Awake 时冻结的 Unity 组合回调。它只描述场景/配置边界，不持有 campaign runtime 或 phase state。
    /// </summary>
    internal sealed class CampaignFlowBindings
    {
        internal Action<GamePhase, GamePhase> ApplyPhaseRoots { get; set; }
        internal Action DeactivatePhaseRoots { get; set; }
        internal TryCreateCombatConfigurationDelegate TryCreateCombatConfiguration { get; set; }
        internal Func<CancellationToken> ResolveLifetimeToken { get; set; }
        internal Action<string> PresentDepartureBlockedNotice { get; set; }
        internal Action ClearDepartureBlockedNotice { get; set; }
        internal Action ResetSettlementNotices { get; set; }
        internal Action<bool> SettlementLoadCompleted { get; set; }
        internal Action<string> Info { get; set; }
        internal Action<string> Error { get; set; }
        internal SettlementTable3D SettlementTable { get; set; }
        internal GameObject SettlementRoot { get; set; }
        internal GameObject HuntRoot { get; set; }
        internal GameObject UiHunt { get; set; }
        internal SettlementUIManager SettlementUI { get; set; }
        internal PlayableWorkshopCatalog WorkshopCatalog { get; set; }
        internal PlayableSettlementContentCatalog SettlementContentCatalog { get; set; }
        internal ITabletopRandomInteractionPresenter TabletopInteraction { get; set; }
        internal IPlayableHuntRetreatInput HuntDepartureInput { get; set; }
        internal Action<string> Warning { get; set; }
    }

}
