using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunt;
using HuntingInDarkness.Hunt;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    public struct HuntNoiseResolvedEvent
    {
        public string InteractionId;
        public string DestinationId;
        public Vector2Int Coordinate;
        public int NoiseScore;
        public int DangerCardCount;
        public int DeckSize;
        public bool IsDanger;
        public string EventId;
    }

    /// <summary>在揭图提交前冻结噪音牌堆并等待一次实体抽牌；取消或非法结果不会改变地图。</summary>
    public sealed class PrepareHuntNoiseAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly Vector2Int coordinate;
        private readonly HuntTileInteractionKind intendedKind;
        private readonly Guid huntSessionId;
        private readonly string destinationId;
        private readonly ITabletopRandomInteractionPresenter presenter;
        private readonly PlayableHuntNoiseProfile profile;
        private readonly NoiseCheckPlan basePlan;
        private readonly IReadOnlyList<EventData> eligibleDangerEvents;

        public PrepareHuntNoiseAction(HuntManager manager, Vector2Int coordinate, HuntTileInteractionKind intendedKind, Guid huntSessionId, string destinationId, ITabletopRandomInteractionPresenter presenter, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.coordinate = coordinate;
            this.intendedKind = intendedKind;
            this.huntSessionId = huntSessionId;
            this.destinationId = destinationId ?? string.Empty;
            this.presenter = presenter;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            PlayableHuntNoiseProfile resolvedProfile = null;
            NoiseCheckPlan resolvedPlan = default;
            IReadOnlyList<EventData> resolvedEvents = Array.Empty<EventData>();
            if (intendedKind == HuntTileInteractionKind.Reveal && manager.Map.TryGetValue(coordinate, out HexTileInstance tile) && tile.State == TileState.Interactable && !tile.HasBossEncounter && tile.Config?.tileRevealEvent == null)
            {
                resolvedProfile = manager.NoiseProfile;
                if (resolvedProfile != null)
                {
                    resolvedProfile.TryCreatePlan(manager.ActiveHunters, out resolvedPlan);
                    resolvedEvents = resolvedProfile.GetEligibleDangerEvents(manager.CurrentYear);
                }
            }
            profile = resolvedProfile;
            basePlan = resolvedPlan;
            eligibleDangerEvents = resolvedEvents;
        }

        public PlayableHuntNoiseResolution Resolution { get; private set; }
        public NoiseCheckPlan Plan { get; private set; }
        public int NoiseModifier { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
        public override ReactionPhases OpenReactionPhases => ReactionPhases.BeforeExecution | ReactionPhases.AfterResolved;

        public void AddNoiseModifier(int value) => NoiseModifier = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, (long)NoiseModifier + value));

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (intendedKind != HuntTileInteractionKind.Reveal) return ActionOutcome.Success();
            if (!manager.Map.TryGetValue(coordinate, out HexTileInstance tile) || tile.State != TileState.Interactable) return ActionOutcome.Failure("地块状态已变化，无法准备噪音检查");
            if (tile.HasBossEncounter || tile.Config?.tileRevealEvent != null) return ActionOutcome.Success();
            if (profile == null || !profile.IsEnabled) return ActionOutcome.Failure("当前目的地没有可用的噪音风险牌堆");
            if (!basePlan.IsEnabled) return ActionOutcome.Failure("当前目的地的噪音配置无效");
            if (eligibleDangerEvents.Count == 0) return ActionOutcome.Failure("当前年份没有可用的噪音危险事件");
            if (presenter == null) return ActionOutcome.Failure("当前没有可用的实体风险抽牌表现器");

            Plan = HuntNoiseRules.ApplyNoiseModifier(basePlan, NoiseModifier, profile.MaxDangerCards);
            string interactionId = $"hunt-noise:{huntSessionId:N}:{coordinate.x},{coordinate.y}";
            string actorId = manager.SelectedHunter != null ? manager.SelectedHunter.InstanceId.ToString() : string.Empty;
            string resultRule = Plan.DangerCardCount > 0 ? $"抽到 1–{Plan.DangerCardCount} 为危险，否则安静" : "本次风险牌堆中没有危险牌";
            var request = new TabletopRandomInteractionRequest(interactionId, TabletopRandomInteractionKind.DrawCards, actorId, destinationId, 1, Plan.DeckSize, profile.ProfileId, $"队伍噪音 {Plan.NoiseScore} · 危险牌 {Plan.DangerCardCount}/{Plan.DeckSize}\n{resultRule}");
            TabletopRandomInteractionResult result = await presenter.PresentAsync(request, cancellationToken);
            if (!TabletopRandomInteractionResultValidator.TryGetCheckTotal(request, result, out int cardValue)) return ActionOutcome.Failure(result.Cancelled ? "已取消风险抽牌，地块没有翻开" : "风险抽牌返回了无效结果");
            EventData selectedEvent = Plan.IsDangerCard(cardValue) ? manager.HuntEvents.SelectNoiseEvent(eligibleDangerEvents) : null;
            if (Plan.IsDangerCard(cardValue) && selectedEvent == null) return ActionOutcome.Failure("危险牌没有可解析的当前年份事件");
            Resolution = new PlayableHuntNoiseResolution(interactionId, destinationId, Plan, cardValue, selectedEvent);
            return ActionOutcome.Success();
        }
    }
}
