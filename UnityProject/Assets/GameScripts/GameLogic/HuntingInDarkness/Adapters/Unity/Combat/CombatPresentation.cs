using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameplayBase.CombatSystem;
using HuntingInDarkness.GameCore.Combat;
using SO.Boss.HitLocation;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    public enum CombatFeedbackKind
    {
        Ordinary,
        Important,
        PhaseConfirmation
    }

    public readonly struct CombatHitLocationCandidateMetadata
    {
        public HitLocationRuntimeState Location { get; }
        public string Name { get; }
        public int CurrentHp { get; }
        public int MaxHp { get; }
        public int EffectiveToughness { get; }
        public bool Succeeds { get; }
        public bool IsDestroyed { get; }

        public CombatHitLocationCandidateMetadata(HitLocationRuntimeState location, string name, int currentHp, int maxHp, int effectiveToughness, bool succeeds, bool isDestroyed)
        {
            Location = location ?? throw new ArgumentNullException(nameof(location));
            Name = name ?? string.Empty;
            CurrentHp = currentHp;
            MaxHp = maxHp;
            EffectiveToughness = effectiveToughness;
            Succeeds = succeeds;
            IsDestroyed = isDestroyed;
        }
    }

    public readonly struct CombatAttackRevealContext
    {
        public int CurrentIndex { get; }
        public int Count { get; }
        public AttackResultCard Result { get; }
        public HitResult? PreviousResult { get; }
        public int TotalAttackPower { get; }
        public IReadOnlyList<CombatHitLocationCandidateMetadata> Candidates { get; }

        public CombatAttackRevealContext(int currentIndex, int count, AttackResultCard result, HitResult? previousResult, int totalAttackPower, IReadOnlyList<CombatHitLocationCandidateMetadata> candidates)
        {
            CurrentIndex = currentIndex;
            Count = count;
            Result = result;
            PreviousResult = previousResult;
            TotalAttackPower = totalAttackPower;
            Candidates = candidates ?? Array.Empty<CombatHitLocationCandidateMetadata>();
        }
    }

    public readonly struct CombatBossTargetPreview
    {
        public int TargetId { get; }
        public int Distance { get; }
        public bool InRange { get; }
        public bool IsRuleTarget { get; }

        public CombatBossTargetPreview(int targetId, int distance, bool inRange, bool isRuleTarget)
        {
            TargetId = targetId;
            Distance = distance;
            InRange = inRange;
            IsRuleTarget = isRuleTarget;
        }
    }

    public readonly struct CombatBossIntentPreviewContext
    {
        public Vector2Int Origin { get; }
        public Vector2Int SelectedTile { get; }
        public int AttackRange { get; }
        public int ExpectedTargetId { get; }
        public int SelectedTargetId { get; }
        public bool IsMoveDeviation { get; }
        public bool IsTargetDeviation { get; }
        public IReadOnlyDictionary<int, int> FateByHunterId { get; }
        public IReadOnlyList<CombatBossTargetPreview> Targets { get; }

        public CombatBossIntentPreviewContext(Vector2Int origin, Vector2Int selectedTile, int attackRange, int expectedTargetId, int selectedTargetId, bool isMoveDeviation, bool isTargetDeviation, IReadOnlyDictionary<int, int> fateByHunterId, IReadOnlyList<CombatBossTargetPreview> targets)
        {
            Origin = origin;
            SelectedTile = selectedTile;
            AttackRange = attackRange;
            ExpectedTargetId = expectedTargetId;
            SelectedTargetId = selectedTargetId;
            IsMoveDeviation = isMoveDeviation;
            IsTargetDeviation = isTargetDeviation;
            FateByHunterId = fateByHunterId ?? throw new ArgumentNullException(nameof(fateByHunterId));
            Targets = targets ?? Array.Empty<CombatBossTargetPreview>();
        }
    }

    public interface ICombatPresentationInput
    {
        UniTask ShowFeedback(string message, CombatFeedbackKind kind, CancellationToken cancellationToken = default);
        UniTask RevealAttackResult(string prompt, CombatAttackRevealContext context, CancellationToken cancellationToken = default);
        UniTask<bool> ShowBossIntentPreview(string prompt, CombatBossIntentPreviewContext context, CancellationToken cancellationToken = default);
        UniTask PresentResolvedFocusAsync(int firstColorIndex, int secondColorIndex, CancellationToken cancellationToken = default);
        UniTask ShowDeathResultAsync(int selectedIndex, string resultText, CancellationToken cancellationToken = default, string faceTitle = null);
        void EndAttackPresentation();
    }

    public interface ICombatInspirationPaymentPresentation
    {
        void PresentInspirationPaymentPreview(int ownerId, IReadOnlyList<int> candidateTokenIds, IReadOnlyCollection<int> selectedTokenIds, int remainingCount);
        void ClearInspirationPaymentPreview(int ownerId);
    }

    public static class CombatPresentationDispatch
    {
        public static UniTask ShowOrdinary(IPlayerInputProvider input, string message, CancellationToken cancellationToken = default)
        {
            if (input is ICombatPresentationInput presentation)
                return presentation.ShowFeedback(message, CombatFeedbackKind.Ordinary, cancellationToken);
            return input.ShowResult(message, cancellationToken);
        }

        public static UniTask ShowImportant(IPlayerInputProvider input, string message, CancellationToken cancellationToken = default)
        {
            if (input is ICombatPresentationInput presentation)
                return presentation.ShowFeedback(message, CombatFeedbackKind.Important, cancellationToken);
            return input.ShowResult(message, cancellationToken);
        }

        public static UniTask ShowPhaseConfirmation(IPlayerInputProvider input, string message, CancellationToken cancellationToken = default)
        {
            if (input is ICombatPresentationInput presentation)
                return presentation.ShowFeedback(message, CombatFeedbackKind.PhaseConfirmation, cancellationToken);
            return input.ShowResult(message, cancellationToken);
        }

        public static UniTask RevealAttackResult(IPlayerInputProvider input, string prompt, CombatAttackRevealContext context, CancellationToken cancellationToken = default)
        {
            if (input is ICombatPresentationInput presentation)
                return presentation.RevealAttackResult(prompt, context, cancellationToken);
            if (input is IAttackResultBatchInputProvider batchInput)
                return batchInput.RequestRevealAttackResult(prompt, cancellationToken);
            return UniTask.CompletedTask;
        }

        public static UniTask<bool> ShowBossIntentPreview(IPlayerInputProvider input, string prompt, CombatBossIntentPreviewContext context, CancellationToken cancellationToken = default)
        {
            if (input is ICombatPresentationInput presentation)
                return presentation.ShowBossIntentPreview(prompt, context, cancellationToken);
            return UniTask.FromResult(true);
        }

        public static UniTask PresentResolvedFocusAsync(IPlayerInputProvider input, int firstColorIndex, int secondColorIndex, CancellationToken cancellationToken = default)
        {
            if (input is ICombatPresentationInput presentation)
                return presentation.PresentResolvedFocusAsync(firstColorIndex, secondColorIndex, cancellationToken);
            return UniTask.CompletedTask;
        }

        public static UniTask ShowDeathResultAsync(IPlayerInputProvider input, int selectedIndex, string resultText, CancellationToken cancellationToken = default, string faceTitle = null)
        {
            if (input is ICombatPresentationInput presentation)
                return presentation.ShowDeathResultAsync(selectedIndex, resultText, cancellationToken, faceTitle);
            return input.ShowResult(resultText, cancellationToken);
        }

        public static void EndAttackPresentation(IPlayerInputProvider input)
        {
            if (input is ICombatPresentationInput presentation) presentation.EndAttackPresentation();
        }
    }
}
