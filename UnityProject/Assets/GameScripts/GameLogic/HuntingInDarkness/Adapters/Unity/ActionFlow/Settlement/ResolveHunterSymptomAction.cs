using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    public interface ISettlementSymptomContent
    {
        IReadOnlyList<SymptomDefinition> GetDefinitions();
        bool TryGetById(string symptomId, out SymptomDefinition definition);
    }

    public readonly struct HunterSymptomCommandResult
    {
        public HunterSymptomCommandResult(bool succeeded, string reason, string symptomId, string symptomName, SymptomResolutionChoice choice, int previousProgress, int currentProgress, int previousWillpower, int currentWillpower, int previousGrowth, int currentGrowth, bool isInternalized, bool isOvercome)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            SymptomId = symptomId ?? string.Empty;
            SymptomName = symptomName ?? string.Empty;
            Choice = choice;
            PreviousProgress = previousProgress;
            CurrentProgress = currentProgress;
            PreviousWillpower = previousWillpower;
            CurrentWillpower = currentWillpower;
            PreviousGrowth = previousGrowth;
            CurrentGrowth = currentGrowth;
            IsInternalized = isInternalized;
            IsOvercome = isOvercome;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public string SymptomId { get; }
        public string SymptomName { get; }
        public SymptomResolutionChoice Choice { get; }
        public int PreviousProgress { get; }
        public int CurrentProgress { get; }
        public int PreviousWillpower { get; }
        public int CurrentWillpower { get; }
        public int PreviousGrowth { get; }
        public int CurrentGrowth { get; }
        public bool IsInternalized { get; }
        public bool IsOvercome { get; }

        public static HunterSymptomCommandResult Failed(string reason) => new(false, reason, string.Empty, string.Empty, default, 0, 0, 0, 0, 0, 0, false, false);
    }

    public struct HunterSymptomResolvedEvent
    {
        public int HunterId;
        public string HunterName;
        public string SymptomId;
        public string SymptomName;
        public SymptomResolutionChoice Choice;
        public bool IsInternalized;
        public bool IsOvercome;
    }

    /// <summary>面对或克服症状的唯一权威提交点；Reactor 可阻止或改写本次选择。</summary>
    public sealed class ResolveHunterSymptomAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly HunterInstance hunter;
        private readonly string symptomId;
        private readonly ISettlementSymptomContent symptomContent;
        private readonly ActionEventOutbox eventOutbox;

        public ResolveHunterSymptomAction(SettlementInstance settlement, HunterInstance hunter, string symptomId, SymptomResolutionChoice choice, ISettlementSymptomContent symptomContent, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.hunter = hunter ?? throw new ArgumentNullException(nameof(hunter));
            this.symptomId = symptomId ?? string.Empty;
            this.symptomContent = symptomContent ?? throw new ArgumentNullException(nameof(symptomContent));
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Choice = choice;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public SymptomResolutionChoice Choice { get; private set; }
        public HunterSymptomCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        public void SetChoice(SymptomResolutionChoice choice) => Choice = choice;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(settlement.GetHunter(hunter.InstanceId), hunter)) return Fail("猎人不属于当前营地。");
            if (!symptomContent.TryGetById(symptomId, out SymptomDefinition definition)) return Fail("症状内容尚未配置。");
            HunterSymptomState state = HunterSymptomRules.Find(hunter, symptomId);
            if (state == null) return Fail("猎人没有这一症状。");

            int previousProgress = state.InternalizationProgress;
            int previousWillpower = hunter.Willpower;
            int previousGrowth = hunter.UnspentGrowth;
            bool succeeded;
            string reason;
            if (Choice == SymptomResolutionChoice.Internalize)
                succeeded = HunterSymptomRules.TryInternalize(hunter, definition, settlement.CurrentYear, out reason);
            else if (Choice == SymptomResolutionChoice.Overcome)
                succeeded = HunterSymptomRules.TryOvercome(hunter, definition, out reason);
            else
                return Fail("症状处理方式无效。");
            if (!succeeded) return Fail(reason);

            Result = new HunterSymptomCommandResult(true, string.Empty, definition.Id, definition.DisplayName, Choice, previousProgress, state.InternalizationProgress, previousWillpower, hunter.Willpower, previousGrowth, hunter.UnspentGrowth, state.IsInternalized, state.IsOvercome);
            eventOutbox.Stage(new HunterSymptomResolvedEvent
            {
                HunterId = hunter.InstanceId,
                HunterName = hunter.Name,
                SymptomId = definition.Id,
                SymptomName = definition.DisplayName,
                Choice = Choice,
                IsInternalized = state.IsInternalized,
                IsOvercome = state.IsOvercome
            });
            eventOutbox.Stage(new SettlementTransactionCommittedEvent { TransactionId = $"symptom:{hunter.InstanceId}:{definition.Id}:{Choice}", Kind = SettlementTransactionKind.Symptom });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = HunterSymptomCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }
}
