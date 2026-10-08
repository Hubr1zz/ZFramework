using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.CombatSystem;
using GameplayBase.Card.BossActionCard;
using HuntingInDarkness.Combat;
using HuntingInDarkness.GameCore.Cards;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Hunters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SO.Boss.ActionCard;
using HuntingInDarkness.ViewLayer.Tabletop;

namespace HuntingInDarkness.ViewLayer.Combat
{
    public readonly struct CombatScreenChoice
    {
        public string Label { get; }
        public string Detail { get; }
        public int Id { get; }
        public bool IsEnabled { get; }

        public CombatScreenChoice(int id, string label, string detail = "", bool isEnabled = true)
        {
            Id = id;
            Label = label ?? string.Empty;
            Detail = detail ?? string.Empty;
            IsEnabled = isEnabled;
        }
    }

    public sealed class CombatScreenView : MonoBehaviour
    {
        [Header("屏幕布局")]
        [SerializeField] private CanvasGroup screenGroup;
        [SerializeField] private GameObject decisionTrayRoot;
        [SerializeField] private GameObject normalChoicesRoot;
        [SerializeField] private GameObject attackResolutionRoot;
        [SerializeField] private TMP_Text phaseLabel;
        [SerializeField] private TMP_Text turnLabel;
        [SerializeField] private TMP_Text bossHealthLabel;
        [SerializeField] private TMP_Text bossIntentLabel;
        [SerializeField] private TMP_Text selectedHunterLabel;
        [SerializeField] private TMP_Text selectedHunterStateLabel;
        [SerializeField] private Image timePointFill;
        [SerializeField] private TMP_Text timePointForecastLabel;
        [SerializeField] private TMP_Text selectedHunterVitalsLabel;
        [SerializeField] private TMP_Text weaponLabel;
        [SerializeField] private TMP_Text mindLabel;
        [SerializeField] private Image[] inspirationSlotShapes;
        [SerializeField] private TMP_Text[] inspirationSlotLabels;
        [SerializeField] private TMP_Text[] bodyPartLabels;
        [SerializeField] private TMP_Text historyLabel;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Transform hunterRowsRoot;
        [SerializeField] private CombatScreenHunterWidget hunterRowPrefab;
        [SerializeField] private Transform actionCardsRoot;
        [SerializeField] private CombatScreenActionCardWidget actionCardPrefab;
        [SerializeField] private Transform bossPartsRoot;
        [SerializeField] private CombatScreenPartWidget bossPartPrefab;
        [SerializeField] private Transform promptChoicesRoot;
        [SerializeField] private CombatScreenChoiceWidget promptChoicePrefab;
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private TMP_Text cardDetailLabel;
        [SerializeField] private Button playCardButton;
        [SerializeField] private Button restoreCardButton;
        [SerializeField] private Button burstCardButton;
        [SerializeField] private TMP_Text cardActionReasonLabel;
        [SerializeField] private TMP_Text attackResultLabel;
        [SerializeField] private Transform attackCandidatesRoot;
        [SerializeField] private CombatScreenPartWidget attackCandidatePrefab;
        [SerializeField] private Button attackRevealButton;
        [SerializeField] private Button promptCloseButton;
        [SerializeField] private float ordinaryFeedbackDuration = 0.6f;
        [SerializeField] private float cardHoverLift = 12f;
        [SerializeField] private float cardFeedbackDuration = 0.25f;
        [SerializeField] private int historyEntryLimit = 6;

        private readonly List<CombatScreenHunterWidget> hunterRows = new();
        private readonly List<CombatScreenActionCardWidget> actionCards = new();
        private readonly List<CombatScreenPartWidget> bossParts = new();
        private readonly List<CombatScreenChoiceWidget> promptChoices = new();
        private readonly List<CombatScreenPartWidget> attackCandidates = new();
        private readonly Queue<string> history = new();
        private PlayableCombatSession session;
        private CancellationTokenSource pendingPromptCancellation;
        private int selectedHunterId = -1;
        private int selectedCardId = -1;
        private int hoveredCardId = -1;
        private int attackCandidateIndex = -1;
        private UniTaskCompletionSource attackRevealCompletion;
        private int paymentOwnerId = -1;
        private readonly HashSet<int> paymentCandidates = new();
        private readonly HashSet<int> paymentSelected = new();
        private int paymentRemainingCount;
        private UniTaskCompletionSource feedbackCompletion;
        private IReadOnlyList<CombatHitLocationCandidateMetadata> currentAttackCandidateMetadata = Array.Empty<CombatHitLocationCandidateMetadata>();
        private string lastRenderSignature;
        private bool modalChoiceOpen;
        private int promptGeneration;
        private bool attackSequenceActive;
        private string lastAttackFeedback = string.Empty;
        private AttackResultCard? currentAttackResult;

        public float OrdinaryFeedbackDuration => ordinaryFeedbackDuration;
        public float CardHoverLift => cardHoverLift;
        public float CardFeedbackDuration => cardFeedbackDuration;

        public void ValidateReferences()
        {
            Require(screenGroup, nameof(screenGroup));
            Require(decisionTrayRoot, nameof(decisionTrayRoot));
            Require(normalChoicesRoot, nameof(normalChoicesRoot));
            Require(attackResolutionRoot, nameof(attackResolutionRoot));
            Require(phaseLabel, nameof(phaseLabel));
            Require(turnLabel, nameof(turnLabel));
            Require(bossHealthLabel, nameof(bossHealthLabel));
            Require(bossIntentLabel, nameof(bossIntentLabel));
            Require(selectedHunterLabel, nameof(selectedHunterLabel));
            Require(selectedHunterStateLabel, nameof(selectedHunterStateLabel));
            Require(timePointFill, nameof(timePointFill));
            Require(timePointForecastLabel, nameof(timePointForecastLabel));
            Require(selectedHunterVitalsLabel, nameof(selectedHunterVitalsLabel));
            Require(weaponLabel, nameof(weaponLabel));
            Require(mindLabel, nameof(mindLabel));
            if (inspirationSlotShapes == null || inspirationSlotLabels == null || inspirationSlotShapes.Length != inspirationSlotLabels.Length || inspirationSlotShapes.Length == 0)
                throw new MissingReferenceException($"[{nameof(CombatScreenView)}] 灵感槽控件引用未完整绑定。");
            if (bodyPartLabels == null || bodyPartLabels.Length != 5)
                throw new MissingReferenceException($"[{nameof(CombatScreenView)}] 五个身体部位示意引用未完整绑定。");
            Require(historyLabel, nameof(historyLabel));
            Require(endTurnButton, nameof(endTurnButton));
            Require(hunterRowsRoot, nameof(hunterRowsRoot));
            Require(hunterRowPrefab, nameof(hunterRowPrefab));
            Require(actionCardsRoot, nameof(actionCardsRoot));
            Require(actionCardPrefab, nameof(actionCardPrefab));
            Require(bossPartsRoot, nameof(bossPartsRoot));
            Require(bossPartPrefab, nameof(bossPartPrefab));
            Require(promptChoicesRoot, nameof(promptChoicesRoot));
            Require(promptChoicePrefab, nameof(promptChoicePrefab));
            Require(promptLabel, nameof(promptLabel));
            Require(cardDetailLabel, nameof(cardDetailLabel));
            Require(playCardButton, nameof(playCardButton));
            Require(restoreCardButton, nameof(restoreCardButton));
            Require(burstCardButton, nameof(burstCardButton));
            Require(cardActionReasonLabel, nameof(cardActionReasonLabel));
            Require(attackResultLabel, nameof(attackResultLabel));
            Require(attackCandidatesRoot, nameof(attackCandidatesRoot));
            Require(attackCandidatePrefab, nameof(attackCandidatePrefab));
            Require(attackRevealButton, nameof(attackRevealButton));
            Require(promptCloseButton, nameof(promptCloseButton));
        }

        public void Bind(PlayableCombatSession combatSession)
        {
            ValidateReferences();
            session = combatSession ?? throw new ArgumentNullException(nameof(combatSession));
            selectedHunterId = session.CombatRoster.Count > 0 ? session.CombatRoster[0].Id : -1;
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(EndTurn);
            playCardButton.onClick.RemoveAllListeners();
            playCardButton.onClick.AddListener(PlaySelectedCard);
            restoreCardButton.onClick.RemoveAllListeners();
            restoreCardButton.onClick.AddListener(RestoreSelectedCard);
            burstCardButton.onClick.RemoveAllListeners();
            burstCardButton.onClick.AddListener(BurstSelectedCard);
            promptCloseButton.onClick.RemoveAllListeners();
            attackRevealButton.onClick.RemoveAllListeners();
            attackRevealButton.onClick.AddListener(CommitAttackReveal);
            playCardButton.interactable = false;
            restoreCardButton.interactable = false;
            burstCardButton.interactable = false;
            Refresh();
        }

        public void Refresh()
        {
            if (session == null || !session.IsActive) return;
            string signature = BuildRenderSignature();
            if (signature == lastRenderSignature) return;
            lastRenderSignature = signature;
            RenderHeader();
            RenderHunterRows();
            RenderSelectedHunter();
            RenderActionCards();
            RenderBossParts();
            endTurnButton.interactable = !modalChoiceOpen && !ScreenModalInputGate.IsBlocked && !session.IsResolvingAction && session.CurrentPhase == TurnPhase.PlayerTurn;
            UpdateActionButtonAvailability();
        }

        public void SelectCharacterForInspection(int characterId)
        {
            selectedHunterId = characterId;
            selectedCardId = -1;
            cardDetailLabel.text = string.Empty;
            cardActionReasonLabel.text = string.Empty;
            lastRenderSignature = null;
            Refresh();
        }

        public void ShowBoardPrompt(string prompt)
        {
            decisionTrayRoot.SetActive(true);
            normalChoicesRoot.SetActive(true);
            attackResolutionRoot.SetActive(false);
            promptLabel.text = prompt ?? string.Empty;
        }

        public void ClearBoardPrompt()
        {
            if (modalChoiceOpen) return;
            promptLabel.text = string.Empty;
            decisionTrayRoot.SetActive(false);
        }

        public void HighlightBossDestinations(IReadOnlyList<Vector2Int> validTiles, IReadOnlyList<Vector2Int> compliantTiles)
        {
            session?.HighlightBossDestinations(validTiles, compliantTiles);
        }

        public void ClearBossHighlights() => session?.ClearBossHighlights();

        public void ShowBossIntentPreview()
        {
            if (modalChoiceOpen) return;
            session?.HighlightBossIntentPreview();
        }

        public void HideBossIntentPreview()
        {
            if (modalChoiceOpen) return;
            if (selectedCardId >= 0) session?.HighlightCardPreview(selectedCardId);
            else session?.ClearCardPreview();
        }

        public async UniTask<bool> ShowBossIntentPreviewAsync(string prompt, CancellationToken cancellationToken = default)
        {
            int selected = await ShowChoicesAsync($"行动提交预览\n{prompt}", new[] { new CombatScreenChoice(1, "确认并执行 Boss 意图", "命运变化按行动规则结算。"), new CombatScreenChoice(0, "返回调整落点与目标", "Boss 尚未移动，命运尚未变化。") }, cancellationToken);
            return selected == 1;
        }

        public async UniTask<int> ShowChoicesAsync(string prompt, IReadOnlyList<CombatScreenChoice> choices, CancellationToken cancellationToken = default)
        {
            if (choices == null || choices.Count == 0) return -1;
            CancelPendingPrompt();
            decisionTrayRoot.SetActive(true);
            normalChoicesRoot.SetActive(true);
            attackResolutionRoot.SetActive(false);
            int generation = ++promptGeneration;
            CancellationTokenSource promptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            var completion = new UniTaskCompletionSource<int>();
            pendingPromptCancellation = promptCancellation;
            modalChoiceOpen = true;
            promptLabel.text = prompt ?? string.Empty;
            EnsureCount(promptChoicesRoot, promptChoices, choices.Count, promptChoicePrefab);
            for (int index = 0; index < choices.Count; index++)
            {
                CombatScreenChoice choice = choices[index];
                promptChoices[index].Bind(choice.Label, choice.Detail, choice.IsEnabled, () => completion.TrySetResult(choice.Id));
            }
            promptCloseButton.gameObject.SetActive(false);
            screenGroup.interactable = true;
            screenGroup.blocksRaycasts = true;
            lastRenderSignature = null;
            Refresh();
            using (ScreenModalInputGate.Acquire(this))
            {
                try
                {
                    return await completion.Task.AttachExternalCancellation(promptCancellation.Token);
                }
                finally
                {
                    ClosePrompt(generation, promptCancellation);
                }
            }
        }

        public async UniTask ShowFeedbackAsync(string message, CombatFeedbackKind kind, CancellationToken cancellationToken = default)
        {
            CancelPendingPrompt();
            int generation = ++promptGeneration;
            promptLabel.text = message ?? string.Empty;
            ClearPromptChoices();
            AddHistory(message);
            if (kind == CombatFeedbackKind.Ordinary)
            {
                decisionTrayRoot.SetActive(true);
                if (attackSequenceActive)
                {
                    normalChoicesRoot.SetActive(false);
                    attackResolutionRoot.SetActive(true);
                    lastAttackFeedback = message ?? string.Empty;
                    attackResultLabel.text = lastAttackFeedback;
                }
                else
                {
                    normalChoicesRoot.SetActive(true);
                    attackResolutionRoot.SetActive(false);
                }
                await UniTask.Delay(TimeSpan.FromSeconds(ordinaryFeedbackDuration), cancellationToken: cancellationToken);
                if (generation == promptGeneration && !attackSequenceActive) ClearBoardPrompt();
                return;
            }

            decisionTrayRoot.SetActive(true);
            normalChoicesRoot.SetActive(true);
            attackResolutionRoot.SetActive(false);
            CancellationTokenSource promptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            pendingPromptCancellation = promptCancellation;
            feedbackCompletion = new UniTaskCompletionSource();
            modalChoiceOpen = true;
            promptCloseButton.gameObject.SetActive(true);
            promptCloseButton.interactable = true;
            TMP_Text continueLabel = promptCloseButton.GetComponentInChildren<TMP_Text>();
            if (continueLabel == null) throw new MissingReferenceException($"[{nameof(CombatScreenView)}] 继续按钮缺少 TMP_Text。");
            continueLabel.text = kind == CombatFeedbackKind.PhaseConfirmation ? "进入下一轮" : "继续";
            promptCloseButton.onClick.RemoveAllListeners();
            promptCloseButton.onClick.AddListener(() => feedbackCompletion.TrySetResult());
            using (ScreenModalInputGate.Acquire(this))
            {
                try
                {
                    await feedbackCompletion.Task.AttachExternalCancellation(promptCancellation.Token);
                }
                finally
                {
                    ClosePrompt(generation, promptCancellation);
                }
            }
        }

        public async UniTask RevealAttackResultAsync(string prompt, CombatAttackRevealContext context, CancellationToken cancellationToken = default)
        {
            attackSequenceActive = true;
            decisionTrayRoot.SetActive(true);
            normalChoicesRoot.SetActive(false);
            attackResolutionRoot.SetActive(true);
            promptLabel.text = prompt ?? string.Empty;
            currentAttackCandidateMetadata = context.Candidates;
            currentAttackResult = context.Result;
            attackResultLabel.text = $"{lastAttackFeedback}\n结果牌 {context.CurrentIndex}/{context.Count}：背面朝上".TrimStart('\n');
            EnsureCount(attackCandidatesRoot, attackCandidates, context.Candidates.Count, attackCandidatePrefab);
            attackCandidateIndex = -1;
            for (int index = 0; index < context.Candidates.Count; index++)
            {
                CombatHitLocationCandidateMetadata candidate = context.Candidates[index];
                attackCandidates[index].Bind(candidate.Name, $"HP {candidate.CurrentHp}/{candidate.MaxHp} · 有效韧性 {candidate.EffectiveToughness}", "结果尚未揭示");
                attackCandidates[index].gameObject.SetActive(true);
            }
            attackRevealButton.gameObject.SetActive(context.CurrentIndex == 1);
            attackRevealButton.interactable = context.CurrentIndex == 1;
            if (context.CurrentIndex == 1)
            {
                CancelPendingPrompt();
                int generation = ++promptGeneration;
                CancellationTokenSource revealCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
                pendingPromptCancellation = revealCancellation;
                attackRevealCompletion = new UniTaskCompletionSource();
                modalChoiceOpen = true;
                using (ScreenModalInputGate.Acquire(this))
                {
                    try
                    {
                        await attackRevealCompletion.Task.AttachExternalCancellation(revealCancellation.Token);
                    }
                    finally
                    {
                        revealCancellation.Dispose();
                        if (generation == promptGeneration)
                        {
                            if (pendingPromptCancellation == revealCancellation) pendingPromptCancellation = null;
                            modalChoiceOpen = false;
                        }
                    }
                }
            }
            else
            {
                await UniTask.Delay(TimeSpan.FromSeconds(cardFeedbackDuration), cancellationToken: cancellationToken);
            }
            string resultSummary = context.Result == AttackResultCard.Failure ? "失败牌：本次不会击穿" : "成功牌仍需通过部位韧性判定";
            attackResultLabel.text = $"{lastAttackFeedback}\n结果牌 {context.CurrentIndex}/{context.Count}：{(context.Result == AttackResultCard.Success ? "成功牌" : "失败牌")}\n{resultSummary}".TrimStart('\n');
            for (int index = 0; index < context.Candidates.Count; index++)
            {
                CombatHitLocationCandidateMetadata candidate = context.Candidates[index];
                string outcome = GetAttackCandidateOutcome(context.Result, candidate.Succeeds);
                attackCandidates[index].Bind(candidate.Name, $"HP {candidate.CurrentHp}/{candidate.MaxHp} · 有效韧性 {candidate.EffectiveToughness}", outcome);
            }
        }

        public async UniTask<HitLocationRuntimeState> SelectHitLocationAsync(string prompt, IReadOnlyList<HitLocationRuntimeState> candidates, IReadOnlyList<CombatHitLocationCandidateMetadata> candidateMetadata, CancellationToken cancellationToken = default)
        {
            if (candidates == null || candidates.Count == 0) return null;
            decisionTrayRoot.SetActive(true);
            normalChoicesRoot.SetActive(false);
            attackResolutionRoot.SetActive(true);
            currentAttackCandidateMetadata = candidateMetadata;
            promptLabel.text = prompt ?? string.Empty;
            EnsureCount(attackCandidatesRoot, attackCandidates, candidates.Count, attackCandidatePrefab);
            var completion = new UniTaskCompletionSource<HitLocationRuntimeState>();
            CancelPendingPrompt();
            int generation = ++promptGeneration;
            CancellationTokenSource choiceCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            pendingPromptCancellation = choiceCancellation;
            for (int index = 0; index < candidates.Count; index++)
            {
                HitLocationRuntimeState candidate = candidates[index];
                CombatHitLocationCandidateMetadata metadata = candidateMetadata[index];
                string outcome = currentAttackResult.HasValue ? GetAttackCandidateOutcome(currentAttackResult.Value, metadata.Succeeds) : "等待结果牌揭示";
                attackCandidates[index].BindSelectable(metadata.Name, $"HP {metadata.CurrentHp}/{metadata.MaxHp} · 有效韧性 {metadata.EffectiveToughness}", outcome, () => completion.TrySetResult(candidate));
            }
            modalChoiceOpen = true;
            using (ScreenModalInputGate.Acquire(this))
            {
                try
                {
                    return await completion.Task.AttachExternalCancellation(choiceCancellation.Token);
                }
                finally
                {
                    choiceCancellation.Dispose();
                    if (generation == promptGeneration)
                    {
                        if (pendingPromptCancellation == choiceCancellation) pendingPromptCancellation = null;
                        modalChoiceOpen = false;
                        promptLabel.text = string.Empty;
                        if (!attackSequenceActive) decisionTrayRoot.SetActive(false);
                        lastRenderSignature = null;
                        Refresh();
                    }
                }
            }
        }

        public async UniTask PresentResolvedFocusAsync(int firstColorIndex, int secondColorIndex, CancellationToken cancellationToken = default)
        {
            HuntingInDarkness.ViewLayer.Presentation.PhysicalInteractionScreenView stage = await HuntingInDarkness.ViewLayer.Presentation.PhysicalInteractionScreenView.OpenAsync(cancellationToken);
            try
            {
                await stage.PresentResolvedFocusAsync(firstColorIndex, secondColorIndex, cancellationToken);
            }
            finally
            {
                stage.Close();
            }
        }

        public async UniTask ShowDeathResultAsync(int selectedIndex, string resultText, CancellationToken cancellationToken = default, string faceTitle = null)
        {
            HuntingInDarkness.ViewLayer.Presentation.PhysicalInteractionScreenView stage = HuntingInDarkness.ViewLayer.Presentation.PhysicalInteractionScreenView.Current;
            if (stage == null) throw new InvalidOperationException("死亡判定结果缺少已打开的实体抽牌台。");
            try
            {
                await stage.ShowSelectedResultAsync(selectedIndex, resultText, true, cancellationToken, faceTitle);
            }
            finally
            {
                stage.Close();
            }
        }

        public void EndAttackPresentation()
        {
            attackSequenceActive = false;
            lastAttackFeedback = string.Empty;
            currentAttackResult = null;
            decisionTrayRoot.SetActive(false);
            promptLabel.text = string.Empty;
            attackResultLabel.text = string.Empty;
        }

        private static string GetAttackCandidateOutcome(AttackResultCard result, bool succeeds)
        {
            if (result == AttackResultCard.Failure) return "失败牌：本次不会击穿";
            return succeeds ? "成功牌可击穿" : "成功牌力量不足";
        }

        public void ShowCardDetails(int cardId)
        {
            if (modalChoiceOpen || ScreenModalInputGate.IsBlocked) return;
            PresentCardDetails(cardId, true);
        }

        private void PresentCardDetails(int cardId, bool selectCard)
        {
            if (session == null || session.GetCard(cardId) is not CharacterActionCardInstance card) return;
            if (selectCard) selectedCardId = cardId;
            string reason = session.GetCardUnavailableReason(cardId);
            int current = session.GetTimePoints(card.OwnerCharacterId);
            int limit = session.GetTimePointLimit(card.OwnerCharacterId);
            string rollover = current + card.TimePointCost > limit ? $"\n超出本轮上限：本次仍会执行，之后该猎人结束行动；结转 {current + card.TimePointCost - limit} TP。" : string.Empty;
            string description = card.CurrentFace == CardFace.FaceUp ? card.FaceUpDescription : card.FaceDownDescription;
            string restoreText = string.Join(" / ", card.RestoreConditions.ConvertAll(condition => condition.Description));
            string burstText = card.BurstReward != null ? card.BurstReward.rewardDescription : "无弃牌收益";
            string restoreReason = session.GetRestoreUnavailableReason(cardId);
            string burstReason = session.GetBurstUnavailableReason(cardId);
            bool isSelected = selectedCardId == cardId;
            bool canRestore = string.IsNullOrEmpty(restoreReason);
            bool canBurst = string.IsNullOrEmpty(burstReason);
            List<string> actionReasons = new();
            if (!string.IsNullOrEmpty(reason)) actionReasons.Add($"不可打出：{reason}");
            if (!canRestore && card.CurrentFace == CardFace.FaceDown) actionReasons.Add($"恢复不可用：{restoreReason}");
            if (!canBurst && (card.BurstReward != null || card.CanDiscard)) actionReasons.Add($"爆发不可用：{burstReason}");
            cardActionReasonLabel.text = string.Join("\n", actionReasons);
            string actionReasonText = string.Empty;
            if (!string.IsNullOrEmpty(cardActionReasonLabel.text)) actionReasonText = $"\n{cardActionReasonLabel.text}\n";
            cardDetailLabel.text = $"{card.CardName}\n费用：{card.CostDescription}{rollover}{actionReasonText}\n{description}\n恢复条件：{restoreText}\n恢复费用：{session.GetRestoreCostDescription(cardId)}\n爆发：{burstText}";
            playCardButton.interactable = isSelected && string.IsNullOrEmpty(reason) && !session.IsResolvingAction && session.CurrentPhase == TurnPhase.PlayerTurn && !modalChoiceOpen;
            restoreCardButton.interactable = isSelected && canRestore && !modalChoiceOpen;
            burstCardButton.interactable = isSelected && canBurst && !modalChoiceOpen;
            restoreCardButton.gameObject.SetActive(card.CurrentFace == CardFace.FaceDown);
            burstCardButton.gameObject.SetActive(card.BurstReward != null || card.CanDiscard);
            SetButtonLabel(restoreCardButton, "恢复");
            SetButtonLabel(burstCardButton, "爆发");
            if (selectCard) session.HighlightCardPreview(cardId);
            lastRenderSignature = null;
            Refresh();
        }

        public void CancelPendingPrompt()
        {
            CancellationTokenSource current = pendingPromptCancellation;
            pendingPromptCancellation = null;
            if (current != null) current.Cancel();
        }

        private void ClosePrompt(int generation, CancellationTokenSource owner)
        {
            owner.Dispose();
            if (generation != promptGeneration) return;
            if (pendingPromptCancellation == owner) pendingPromptCancellation = null;
            modalChoiceOpen = false;
            feedbackCompletion = null;
            promptLabel.text = string.Empty;
            decisionTrayRoot.SetActive(false);
            normalChoicesRoot.SetActive(true);
            attackResolutionRoot.SetActive(false);
            promptCloseButton.gameObject.SetActive(false);
            ClearPromptChoices();
            lastRenderSignature = null;
            Refresh();
        }

        private void RenderHeader()
        {
            phaseLabel.text = session.CurrentPhase switch
            {
                TurnPhase.PlayerTurn => "猎人行动",
                TurnPhase.BossTurn => "Boss 行动",
                _ => session.CurrentPhase.ToString()
            };
            turnLabel.text = $"第 {session.CurrentTurnNumber} 轮　{BuildActionRhythmText()}";
            if (session.Boss is IBossVitalityState vitality)
                bossHealthLabel.text = $"Boss 生命  {vitality.CurrentHealth} / {vitality.MaxHealth}";
            if (session.BossRevealedCards.Count > 0)
            {
                BossActionCardData card = session.BossRevealedCards[0];
                var intent = new StringBuilder(card.actionName);
                foreach (BossActionCardEffectData effect in card.effects)
                    if (effect is PlayableBossAttackEffectData attack)
                        intent.AppendLine($"\n索敌：{GetTargetPolicyName(attack.TargetPolicy)} · 移动 {attack.MovementDistance} · 范围 {attack.AttackRange}\n失败：{attack.NoTargetOrRangeFailure}");
                intent.AppendLine();
                intent.AppendLine(card.description);
                intent.Append(card.windupDescription);
                bossIntentLabel.text = intent.ToString();
            }
            else
            {
                bossIntentLabel.text = "Boss 意图待揭示";
            }
        }

        private void RenderHunterRows()
        {
            IReadOnlyList<ICharacterState> characters = session.CombatRoster;
            EnsureCount(hunterRowsRoot, hunterRows, characters.Count, hunterRowPrefab);
            for (int index = 0; index < characters.Count; index++)
            {
                int characterId = characters[index].Id;
                CharacterRuntimeData data = session.GetCharacterData(characterId);
                bool canAct = session.CanCharacterAct(characterId);
                int timePoints = session.GetTimePoints(characterId);
                int limit = session.GetTimePointLimit(characterId);
                string status = canAct ? $"TP {timePoints}/{limit} · 意志 {data.Willpower}" : $"{session.GetCharacterActionUnavailableReason(characterId)}\nTP {timePoints}/{limit} · 意志 {data.Willpower}";
                hunterRows[index].Bind(data, selectedHunterId == characterId, status, GetInjurySummary(data), SelectHunter);
            }
        }

        private void RenderSelectedHunter()
        {
            CharacterRuntimeData data = session.GetCharacterData(selectedHunterId);
            if (data == null)
            {
                selectedHunterLabel.text = "选择猎人";
                selectedHunterStateLabel.text = string.Empty;
                selectedHunterVitalsLabel.text = string.Empty;
                timePointFill.fillAmount = 0;
                timePointForecastLabel.text = string.Empty;
                weaponLabel.text = string.Empty;
                mindLabel.text = string.Empty;
                return;
            }

            selectedHunterLabel.text = data.Name;
            int currentTimePoints = session.GetTimePoints(data.Id);
            int limit = session.GetTimePointLimit(data.Id);
            selectedHunterStateLabel.text = $"时点 {currentTimePoints} / {limit}　意志 {data.Willpower}";
            timePointFill.fillAmount = limit <= 0 ? 0 : Mathf.Clamp01((float)currentTimePoints / limit);
            int previewCardId = hoveredCardId >= 0 ? hoveredCardId : selectedCardId;
            if (previewCardId >= 0 && session.GetCard(previewCardId) is CharacterActionCardInstance previewCard && previewCard.OwnerCharacterId == data.Id)
            {
                int expected = currentTimePoints + previewCard.TimePointCost;
                int excess = Mathf.Max(0, expected - limit);
                timePointForecastLabel.text = excess > 0 ? $"预计 {expected}/{limit} · 超额 {excess}，本次仍生效，之后结束行动" : $"预计 {expected}/{limit}";
            }
            else timePointForecastLabel.text = $"本轮 {currentTimePoints}/{limit}";
            selectedHunterVitalsLabel.text = GetInjurySummary(data);
            RenderBodyPartDiagram(data);
            weaponLabel.text = data.EquippedWeapon != null ? $"武器：{data.EquippedWeapon.weaponName}" : "武器：未装备";
            mindLabel.text = BuildMindSummary(data.Id);
            RenderMindSlots(data.Id);
        }

        private void RenderActionCards()
        {
            IReadOnlyList<ICharacterActionCardInstanceState> cards = session.GetCardsOf(selectedHunterId);
            EnsureCount(actionCardsRoot, actionCards, cards.Count, actionCardPrefab);
            for (int index = 0; index < cards.Count; index++)
            {
                ICharacterActionCardInstanceState cardState = cards[index];
                if (cardState is not CharacterActionCardInstance card) continue;
                string reason = session.GetCardUnavailableReason(card.InstanceId);
                actionCards[index].Bind(card, card.InstanceId == selectedCardId, reason, ShowCardDetails, HoverActionCard, EndHoverActionCard);
            }
        }

        private void RenderBossParts()
        {
            var visibleParts = new List<HitLocationRuntimeState>();
            foreach (HitLocationRuntimeState state in session.BossHitLocationStates)
                if (state.IsFaceUp || state.IsPersistentActive || state.IsDestroyed) visibleParts.Add(state);
            EnsureCount(bossPartsRoot, bossParts, visibleParts.Count, bossPartPrefab);
            for (int index = 0; index < visibleParts.Count; index++)
            {
                HitLocationRuntimeState part = visibleParts[index];
                string locationName = part.Data.locationName;
                string state = part.IsDestroyed ? "已摧毁" : part.IsPersistentActive ? "持续留场" : part.IsFaceUp ? "已揭示" : "隐藏";
                bossParts[index].Bind(locationName, $"HP {part.CurrentHp}/{part.Data.maxHp} · {state}", $"韧性 {part.Data.toughness}");
            }
        }

        private void SelectHunter(int characterId)
        {
            if (modalChoiceOpen || ScreenModalInputGate.IsBlocked) return;
            selectedHunterId = characterId;
            selectedCardId = -1;
            session.OnSelectCharacter(characterId);
            Refresh();
        }

        private void PlaySelectedCard()
        {
            if (selectedCardId < 0 || modalChoiceOpen || ScreenModalInputGate.IsBlocked || session.IsResolvingAction || session.CurrentPhase != TurnPhase.PlayerTurn) return;
            int cardId = selectedCardId;
            if (session.GetCard(cardId) is not CharacterActionCardInstance card || !string.IsNullOrEmpty(session.GetCardUnavailableReason(card.InstanceId)) || !session.CanCharacterAct(card.OwnerCharacterId)) return;
            selectedHunterId = card.OwnerCharacterId;
            session.OnSelectCharacter(selectedHunterId);
            session.OnPlayCard(cardId, -1);
        }

        private void RestoreSelectedCard()
        {
            if (selectedCardId < 0 || modalChoiceOpen || ScreenModalInputGate.IsBlocked || session.IsResolvingAction || session.CurrentPhase != TurnPhase.PlayerTurn) return;
            int cardId = selectedCardId;
            if (!session.CanRestoreCard(cardId) || session.GetCard(cardId) is not CharacterActionCardInstance card) return;
            selectedHunterId = card.OwnerCharacterId;
            session.OnSelectCharacter(selectedHunterId);
            session.OnRestoreCard(cardId);
        }

        private void BurstSelectedCard()
        {
            if (selectedCardId < 0 || modalChoiceOpen || ScreenModalInputGate.IsBlocked || session.IsResolvingAction || session.CurrentPhase != TurnPhase.PlayerTurn) return;
            int cardId = selectedCardId;
            if (!session.CanBurstCard(cardId) || session.GetCard(cardId) is not CharacterActionCardInstance card) return;
            selectedHunterId = card.OwnerCharacterId;
            session.OnSelectCharacter(selectedHunterId);
            session.OnDiscardCard(cardId);
        }

        private void CommitAttackReveal()
        {
            attackRevealButton.interactable = false;
            attackRevealCompletion?.TrySetResult();
        }

        private void EndTurn()
        {
            if (session == null || modalChoiceOpen || ScreenModalInputGate.IsBlocked || session.IsResolvingAction || session.CurrentPhase != TurnPhase.PlayerTurn) return;
            session.OnEndTurn();
        }

        private void CancelPrompt()
        {
            pendingPromptCancellation?.Cancel();
        }

        private void ClosePrompt()
        {
            promptLabel.text = string.Empty;
            modalChoiceOpen = false;
            promptCloseButton.gameObject.SetActive(false);
            screenGroup.interactable = true;
            screenGroup.blocksRaycasts = true;
            ClearPromptChoices();
            pendingPromptCancellation?.Dispose();
            pendingPromptCancellation = null;
            lastRenderSignature = null;
            Refresh();
        }

        private void AddHistory(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            history.Enqueue(message);
            while (history.Count > Mathf.Max(1, historyEntryLimit)) history.Dequeue();
            var text = new StringBuilder();
            foreach (string entry in history) text.AppendLine(entry);
            historyLabel.text = text.ToString();
        }

        private static string GetInjurySummary(CharacterRuntimeData character)
        {
            HunterBodyPartState head = character.CombatStats.InjuryState.GetPart(HunterBodyPart.Head);
            HunterBodyPartState torso = character.CombatStats.InjuryState.GetPart(HunterBodyPart.Torso);
            HunterBodyPartState arms = character.CombatStats.InjuryState.GetPart(HunterBodyPart.Arms);
            HunterBodyPartState legs = character.CombatStats.InjuryState.GetPart(HunterBodyPart.Legs);
            return $"头 {head.CurrentHealth}/{head.Definition.MaxHealth}　躯 {torso.CurrentHealth}/{torso.Definition.MaxHealth}\n臂 {arms.CurrentHealth}/{arms.Definition.MaxHealth}　腿 {legs.CurrentHealth}/{legs.Definition.MaxHealth}";
        }

        private string BuildMindSummary(int characterId)
        {
            return $"思维 {session.GetCombatInspirationTokens(characterId).Count}/{session.GetCombatInspirationCapacity(characterId)}";
        }

        public void PresentInspirationPaymentPreview(int ownerId, IReadOnlyList<int> candidateTokenIds, IReadOnlyCollection<int> selectedTokenIds, int remainingCount)
        {
            paymentOwnerId = ownerId;
            paymentCandidates.Clear();
            paymentSelected.Clear();
            if (candidateTokenIds != null)
                foreach (int id in candidateTokenIds) paymentCandidates.Add(id);
            if (selectedTokenIds != null)
                foreach (int id in selectedTokenIds) paymentSelected.Add(id);
            paymentRemainingCount = Mathf.Max(0, remainingCount);
            RenderMindSlots(ownerId);
        }

        public void ClearInspirationPaymentPreview(int ownerId)
        {
            if (paymentOwnerId != ownerId) return;
            paymentOwnerId = -1;
            paymentCandidates.Clear();
            paymentSelected.Clear();
            paymentRemainingCount = 0;
            RenderMindSlots(selectedHunterId);
        }

        private void RenderMindSlots(int characterId)
        {
            IReadOnlyList<CombatInspirationToken> tokens = session.GetCombatInspirationTokens(characterId);
            int capacity = session.GetCombatInspirationCapacity(characterId);
            for (int index = 0; index < inspirationSlotLabels.Length; index++)
            {
                TMP_Text label = inspirationSlotLabels[index];
                Image shape = inspirationSlotShapes[index];
                if (index >= capacity)
                {
                    label.text = string.Empty;
                    shape.color = new Color(0.12f, 0.13f, 0.15f, 0.5f);
                    continue;
                }
                if (index >= tokens.Count)
                {
                    label.text = "空槽";
                    shape.color = new Color(0.24f, 0.25f, 0.27f, 1f);
                    continue;
                }
                CombatInspirationToken token = tokens[index];
                if (token.IsBurden)
                {
                    label.text = "▣ 负面/锁";
                    shape.color = new Color(0.35f, 0.36f, 0.38f, 1f);
                    continue;
                }
                bool selected = paymentOwnerId == characterId && paymentSelected.Contains(token.Id);
                bool candidate = paymentOwnerId == characterId && paymentCandidates.Contains(token.Id);
                string paymentStatus = selected ? "已选" : candidate ? paymentCandidates.Count == 1 ? "将付" : $"待选 {paymentRemainingCount}" : "可用";
                label.text = $"{GetShape(token.Color)} {GetColorName(token.Color)} · {paymentStatus}";
                shape.color = GetInspirationColor(token.Color, candidate || selected);
            }
        }

        private static Color GetInspirationColor(CombatInspirationColor color, bool highlighted)
        {
            Color result = color switch
            {
                CombatInspirationColor.Red => new Color(0.72f, 0.25f, 0.22f, 1f),
                CombatInspirationColor.Blue => new Color(0.22f, 0.48f, 0.78f, 1f),
                CombatInspirationColor.Yellow => new Color(0.85f, 0.66f, 0.18f, 1f),
                _ => Color.gray
            };
            return highlighted ? Color.Lerp(result, Color.white, 0.34f) : result;
        }

        private void RenderBodyPartDiagram(CharacterRuntimeData data)
        {
            HunterBodyPart[] parts = { HunterBodyPart.Head, HunterBodyPart.Torso, HunterBodyPart.Arms, HunterBodyPart.Arms, HunterBodyPart.Legs };
            for (int index = 0; index < parts.Length; index++)
            {
                HunterBodyPartState state = data.CombatStats.InjuryState.GetPart(parts[index]);
                if (index == 2)
                {
                    bodyPartLabels[index].gameObject.SetActive(false);
                    continue;
                }
                bodyPartLabels[index].gameObject.SetActive(true);
                string partName = index == 3 ? "双臂" : GetBodyPartName(parts[index]);
                bodyPartLabels[index].text = $"{partName}{state.CurrentHealth}/{state.Definition.MaxHealth} 甲{state.Armor}";
            }
        }

        private static string GetBodyPartName(HunterBodyPart part) => part switch
        {
            HunterBodyPart.Head => "头",
            HunterBodyPart.Torso => "躯干",
            HunterBodyPart.Arms => "手臂",
            HunterBodyPart.Legs => "腿",
            _ => part.ToString()
        };

        private static string GetColorName(CombatInspirationColor color) => color switch
        {
            CombatInspirationColor.Red => "红",
            CombatInspirationColor.Blue => "蓝",
            CombatInspirationColor.Yellow => "黄",
            _ => color.ToString()
        };

        private static string GetShape(CombatInspirationColor color) => color switch
        {
            CombatInspirationColor.Red => "○",
            CombatInspirationColor.Blue => "◆",
            CombatInspirationColor.Yellow => "▲",
            _ => "○"
        };

        private static string GetTargetPolicyName(BossTargetPolicy policy) => policy switch
        {
            BossTargetPolicy.Nearest => "最近猎人",
            BossTargetPolicy.MostInjured => "伤势最重",
            BossTargetPolicy.Random => "随机猎人",
            BossTargetPolicy.PlayerChoice => "玩家选择",
            _ => policy.ToString()
        };

        private static void SetButtonLabel(Button button, string label)
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>();
            if (text == null) throw new MissingReferenceException($"[{nameof(CombatScreenView)}] 操作按钮缺少 TMP_Text。");
            text.text = label;
        }

        private string BuildRenderSignature()
        {
            var signature = new StringBuilder();
            signature.Append((int)session.CurrentPhase).Append('|').Append((int)session.CurrentBossActionStage).Append('|').Append(session.CurrentTurnNumber).Append('|').Append(selectedHunterId).Append('|').Append(selectedCardId).Append('|').Append(session.IsResolvingAction);
            if (session.Boss is IBossVitalityState vitality) signature.Append('|').Append(vitality.CurrentHealth).Append('/').Append(vitality.MaxHealth);
            foreach (ICharacterState state in session.CombatRoster)
            {
                CharacterRuntimeData character = session.GetCharacterData(state.Id);
                signature.Append('|').Append(state.Id).Append(':').Append(session.GetTimePoints(state.Id)).Append('/').Append(session.GetTimePointLimit(state.Id)).Append(':').Append(character.Willpower);
                foreach (HunterBodyPart part in Enum.GetValues(typeof(HunterBodyPart))) signature.Append(':').Append(character.CombatStats.InjuryState.GetPart(part).CurrentHealth);
                foreach (CombatInspirationToken token in session.GetCombatInspirationTokens(state.Id)) signature.Append(':').Append(token.Id).Append('/').Append(token.IsBurden);
                foreach (ICharacterActionCardInstanceState cardState in session.GetCardsOf(state.Id))
                    if (cardState is CharacterActionCardInstance card) signature.Append(':').Append(card.InstanceId).Append('/').Append((int)card.CurrentFace).Append('/').Append(card.IsAvailableThisTurn);
            }
            foreach (HitLocationRuntimeState part in session.BossHitLocationStates) signature.Append('|').Append(RuntimeHelpers.GetHashCode(part.Data)).Append(':').Append(part.CurrentHp).Append(':').Append(part.IsFaceUp).Append(':').Append(part.IsDestroyed).Append(':').Append(part.IsPersistentActive);
            if (session.BossRevealedCards.Count > 0) signature.Append('|').Append(RuntimeHelpers.GetHashCode(session.BossRevealedCards[0]));
            return signature.ToString();
        }

        private void HoverActionCard(int cardId)
        {
            if (modalChoiceOpen || ScreenModalInputGate.IsBlocked) return;
            hoveredCardId = cardId;
            session?.HighlightCardPreview(cardId);
            PresentCardDetails(cardId, false);
        }

        private void UpdateActionButtonAvailability()
        {
            if (hoveredCardId >= 0 && hoveredCardId != selectedCardId)
            {
                playCardButton.interactable = false;
                restoreCardButton.interactable = false;
                burstCardButton.interactable = false;
                return;
            }
            if (selectedCardId < 0 || session.GetCard(selectedCardId) is not CharacterActionCardInstance card)
            {
                playCardButton.interactable = false;
                restoreCardButton.interactable = false;
                burstCardButton.interactable = false;
                return;
            }
            bool canSubmit = !modalChoiceOpen && !ScreenModalInputGate.IsBlocked && !session.IsResolvingAction && session.CurrentPhase == TurnPhase.PlayerTurn;
            playCardButton.interactable = canSubmit && string.IsNullOrEmpty(session.GetCardUnavailableReason(selectedCardId));
            restoreCardButton.interactable = canSubmit && session.CanRestoreCard(selectedCardId);
            burstCardButton.interactable = canSubmit && session.CanBurstCard(selectedCardId);
            restoreCardButton.gameObject.SetActive(card.CurrentFace == CardFace.FaceDown);
            burstCardButton.gameObject.SetActive(card.BurstReward != null || card.CanDiscard);
        }

        private void Update()
        {
            if (session == null || modalChoiceOpen || attackSequenceActive || ScreenModalInputGate.IsBlocked || session.IsResolvingAction || session.CurrentPhase != TurnPhase.PlayerTurn) return;
            if (!Input.GetKeyDown(KeyCode.Escape) && !Input.GetMouseButtonDown(1)) return;
            if (hoveredCardId < 0 && selectedCardId < 0) return;
            hoveredCardId = -1;
            selectedCardId = -1;
            session.ClearCardPreview();
            cardDetailLabel.text = string.Empty;
            cardActionReasonLabel.text = string.Empty;
            UpdateActionButtonAvailability();
        }

        private void EndHoverActionCard()
        {
            if (modalChoiceOpen || ScreenModalInputGate.IsBlocked) return;
            hoveredCardId = -1;
            if (selectedCardId >= 0) session?.HighlightCardPreview(selectedCardId);
            else session?.ClearCardPreview();
            if (selectedCardId >= 0) PresentCardDetails(selectedCardId, false);
            else
            {
                cardDetailLabel.text = string.Empty;
                cardActionReasonLabel.text = string.Empty;
                playCardButton.interactable = false;
                restoreCardButton.interactable = false;
                burstCardButton.interactable = false;
            }
        }

        private string BuildActionRhythmText()
        {
            if (session.CurrentPhase == TurnPhase.PlayerTurn) return "<b>猎人行动</b> → Boss 行动 → 后摇确认";
            return session.CurrentBossActionStage switch
            {
                BossActionStage.Windup => "猎人行动 → <b>Boss 前摇</b> → Boss 执行 → 后摇确认",
                BossActionStage.Execution => "猎人行动 → <b>Boss 执行</b> → 后摇确认",
                BossActionStage.Recovery => "猎人行动 → Boss 行动 → <b>后摇确认</b>",
                _ => "猎人行动 → Boss 行动 → 后摇确认"
            };
        }

        private static void EnsureCount<T>(Transform root, List<T> widgets, int count, T prefab) where T : Component
        {
            while (widgets.Count > count)
            {
                int index = widgets.Count - 1;
                if (widgets[index] != null) Destroy(widgets[index].gameObject);
                widgets.RemoveAt(index);
            }
            while (widgets.Count < count)
                widgets.Add(Instantiate(prefab, root, false));
        }

        private void ClearPromptChoices()
        {
            for (int index = promptChoices.Count - 1; index >= 0; index--)
                if (promptChoices[index] != null) Destroy(promptChoices[index].gameObject);
            promptChoices.Clear();
        }

        private static void Require(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null) throw new MissingReferenceException($"[{nameof(CombatScreenView)}] {fieldName} 未绑定。");
        }

        private void OnDestroy()
        {
            CancelPendingPrompt();
        }
    }
}
