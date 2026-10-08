using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using Cards3D;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Board;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Combat;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.ViewLayer.Tabletop;
using HuntingInDarkness.ViewLayer.Combat;
using HuntingInDarkness.ViewLayer.Presentation;
using GameLogic;
using SO.Character;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameplayBase.CombatSystem
{
    /// <summary>
    /// IPlayerInputProvider 的 UGUI 实现。
    /// 纯 C# 类，由 GameManager 构造并注入 BoardManager / HexBoardVisualizer 引用。
    /// </summary>
    public class UIPlayerInputProvider : IPlayerInputProvider, IPlayerOptionInputProvider, IAttackResultDeckInputProvider, IAttackResultBatchInputProvider, IBossHitDeckInputProvider, IDeathDeckInputProvider, IBossIntentInputProvider, ICombatPresentationInput, ICombatInspirationPaymentPresentation, System.IDisposable
    {
        private readonly BoardManager _boardManager;
        private readonly HexBoardVisualizer _boardVisualizer;
        private readonly System.Func<int, string> resolveTargetName;
        private readonly PlayableCombatSession combatSession;
        private readonly Camera boardCamera;
        private readonly CancellationTokenSource lifetimeCancellation = new();
        private CombatScreenWindow combatScreenWindow;
        private CombatScreenView combatScreenView;
        private Task<CombatScreenWindow> pendingWindowLoad;
        private IReadOnlyList<CombatHitLocationCandidateMetadata> currentAttackCandidates = System.Array.Empty<CombatHitLocationCandidateMetadata>();
        private PhysicalInteractionScreenView deathCardStage;
        private bool disposed;

        // ─── 懒初始化 UI 元素 ───
        private Canvas _canvas;
        private GameObject _panelRoot;
        private Text _promptText;
        private Transform _buttonContainer;
        private bool _initialized;

        public UIPlayerInputProvider(BoardManager boardManager, HexBoardVisualizer boardVisualizer, System.Func<int, string> resolveTargetName = null)
            : this(boardManager, boardVisualizer, resolveTargetName, null, null)
        {
        }

        public UIPlayerInputProvider(BoardManager boardManager, HexBoardVisualizer boardVisualizer, System.Func<int, string> resolveTargetName, PlayableCombatSession combatSession, Camera boardCamera)
        {
            _boardManager    = boardManager;
            _boardVisualizer = boardVisualizer;
            this.resolveTargetName = resolveTargetName;
            this.combatSession = combatSession;
            this.boardCamera = boardCamera;
        }

        public async UniTask OpenScreenAsync(PlayableCombatSession session)
        {
            if (combatSession == null || session != combatSession) throw new InvalidOperationException("战斗屏幕只能由显式配置的正式战斗会话打开。");
            if (disposed) throw new ObjectDisposedException(nameof(UIPlayerInputProvider));
            pendingWindowLoad ??= GameModule.UI.ShowUIAsyncAwait<CombatScreenWindow>().AsTask();
            combatScreenWindow = await pendingWindowLoad.AsUniTask().AttachExternalCancellation(lifetimeCancellation.Token);
            if (disposed)
            {
                combatScreenWindow.CloseForSession();
                return;
            }
            combatScreenView = combatScreenWindow.View;
            if (combatScreenView == null) throw new MissingReferenceException("CombatScreen prefab 缺少 CombatScreenView 引用。");
            combatScreenWindow.Bind(session);
        }

        public void RefreshScreen() => combatScreenView?.Refresh();

        public void PresentInspirationPaymentPreview(int ownerId, IReadOnlyList<int> candidateTokenIds, IReadOnlyCollection<int> selectedTokenIds, int remainingCount)
            => RequireCombatScreen().PresentInspirationPaymentPreview(ownerId, candidateTokenIds, selectedTokenIds, remainingCount);

        public void ClearInspirationPaymentPreview(int ownerId) => combatScreenView?.ClearInspirationPaymentPreview(ownerId);

        public void SelectCharacterForInspection(int characterId) => combatScreenView?.SelectCharacterForInspection(characterId);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetimeCancellation.Cancel();
            combatScreenView?.CancelPendingPrompt();
            deathCardStage?.Close();
            if (combatScreenWindow != null)
                combatScreenWindow.CloseForSession();
            else if (pendingWindowLoad != null)
                CloseWindowAfterPendingLoadAsync(pendingWindowLoad).Forget();
            lifetimeCancellation.Dispose();
        }

        private async UniTaskVoid CloseWindowAfterPendingLoadAsync(Task<CombatScreenWindow> loadTask)
        {
            try
            {
                CombatScreenWindow window = await loadTask;
                if (window != null) window.CloseForSession();
            }
            catch (System.Exception exception)
            {
                if (exception is not System.OperationCanceledException) Debug.LogException(exception);
            }
        }

        // ═══════════════════════════════════════════
        // 懒初始化
        // ═══════════════════════════════════════════

        private void EnsureInitialized()
        {
            if (combatSession != null) throw new InvalidOperationException("正式战斗已启用 CombatScreen，禁止创建旧式动态输入 UI。");
            if (_initialized) return;
            _initialized = true;

#if UNITY_2023_1_OR_NEWER
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
#else
            Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
#endif
            foreach (Canvas candidate in canvases)
            {
                if (candidate == null || candidate.GetComponentInParent<CardInspectionOverlay>() != null || candidate.GetComponentInParent<TabletopEventPanel3D>() != null || candidate.GetComponentInParent<CampLedgerPanel3D>() != null) continue;
                _canvas = candidate;
                break;
            }
            if (_canvas == null)
            {
                var canvasGo = new GameObject("CombatInputCanvas");
                _canvas = canvasGo.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 100;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode =
                    CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            _panelRoot = new GameObject("CombatInputPanel", typeof(RectTransform));
            _panelRoot.transform.SetParent(_canvas.transform, false);
            var panelRt = _panelRoot.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.25f, 0.2f);
            panelRt.anchorMax = new Vector2(0.75f, 0.8f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            var bg = _panelRoot.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.92f);

            var textGo = new GameObject("Prompt", typeof(RectTransform));
            textGo.transform.SetParent(_panelRoot.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0, 0.5f);
            textRt.anchorMax = new Vector2(1, 1);
            textRt.offsetMin = new Vector2(15, 0);
            textRt.offsetMax = new Vector2(-15, -10);
            _promptText = textGo.AddComponent<Text>();
            _promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _promptText.fontSize = 16;
            _promptText.resizeTextForBestFit = true;
            _promptText.resizeTextMinSize = 12;
            _promptText.resizeTextMaxSize = 16;
            _promptText.color = Color.white;
            _promptText.alignment = TextAnchor.MiddleCenter;
            _promptText.supportRichText = true;
            _promptText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var containerGo = new GameObject("Buttons", typeof(RectTransform));
            containerGo.transform.SetParent(_panelRoot.transform, false);
            var containerRt = containerGo.GetComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0.1f, 0.05f);
            containerRt.anchorMax = new Vector2(0.9f, 0.48f);
            containerRt.offsetMin = Vector2.zero;
            containerRt.offsetMax = Vector2.zero;
            _buttonContainer = containerGo.transform;

            _panelRoot.SetActive(false);
        }

        // ═══════════════════════════════════════════
        // IPlayerInputProvider 实现
        // ═══════════════════════════════════════════

        public async UniTask<int> RequestRoll(string prompt, int maxExclusive, CancellationToken cancellationToken = default)
        {
            if (combatSession != null)
            {
                CombatScreenView view = RequireCombatScreen();
                int choice = await view.ShowChoicesAsync(prompt, new[] { new CombatScreenChoice(0, "投掷", "掷出骰子并查看判定结果。") }, cancellationToken);
                return choice < 0 ? -1 : UnityEngine.Random.Range(0, maxExclusive);
            }
            EnsureInitialized();
            var tcs = new UniTaskCompletionSource<int>();

            ShowPanel(prompt, new ButtonConfig[]
            {
                new("掷骰！", () =>
                {
                    int result = UnityEngine.Random.Range(0, maxExclusive);
                    tcs.TrySetResult(result);
                })
            });

            try
            {
                return await tcs.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                HidePanel();
            }
        }

        public async UniTask<int> RequestDrawAttackResult(string prompt, AttackResultDeckComposition composition, CancellationToken cancellationToken = default)
        {
            if (combatSession != null)
            {
                int choice = await RequireCombatScreen().ShowChoicesAsync(prompt, new[] { new CombatScreenChoice(0, "抽取结果牌", $"牌堆：成功 {composition.SuccessCards} / 失败 {composition.FailureCards}") }, cancellationToken);
                return choice < 0 ? -1 : UnityEngine.Random.Range(0, composition.TotalCards);
            }
            EnsureInitialized();
            var tcs = new UniTaskCompletionSource<int>();

            ShowPanel(prompt, new ButtonConfig[]
            {
                new("抽取结果牌", () => tcs.TrySetResult(UnityEngine.Random.Range(0, composition.TotalCards)))
            });

            try
            {
                return await tcs.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                HidePanel();
            }
        }

        public async UniTask RequestRevealAttackResult(string prompt, CancellationToken cancellationToken = default)
        {
            if (combatSession != null)
            {
                await RequireCombatScreen().ShowChoicesAsync(prompt, new[] { new CombatScreenChoice(0, "翻开当前结果牌", "只揭示当前结果。") }, cancellationToken);
                return;
            }
            EnsureInitialized();
            var tcs = new UniTaskCompletionSource();

            ShowPanel(prompt, new ButtonConfig[]
            {
                new("抽取下一张", () => tcs.TrySetResult())
            });

            try
            {
                await tcs.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                HidePanel();
            }
        }

        public async UniTask<int> RequestDrawBossHitResult(string prompt, BossHitDeckComposition composition, CancellationToken cancellationToken = default)
        {
            if (combatSession != null)
                return UnityEngine.Random.Range(0, composition.TotalCards);
            EnsureInitialized();
            var tcs = new UniTaskCompletionSource<int>();

            ShowPanel(prompt, new ButtonConfig[]
            {
                new("抽取命中牌", () => tcs.TrySetResult(UnityEngine.Random.Range(0, composition.TotalCards)))
            });

            try
            {
                return await tcs.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                HidePanel();
            }
        }

        public async UniTask<int> RequestDrawDeathCard(string prompt, DeathDeckComposition composition, CancellationToken cancellationToken = default)
        {
            if (combatSession != null)
            {
                deathCardStage = await PhysicalInteractionScreenView.OpenAsync(cancellationToken);
                try
                {
                    return await deathCardStage.SelectCardAsync(prompt, $"普通存活 {composition.OrdinarySurvivalCards} · 生存卡 {composition.SurvivalEventCards} · 死亡 {composition.DeathCards}。所有牌背相同。", composition.TotalCards, cancellationToken);
                }
                catch
                {
                    deathCardStage.Close();
                    deathCardStage = null;
                    throw;
                }
            }
            EnsureInitialized();
            if (composition.TotalCards <= 0)
                return 0;

            var tcs = new UniTaskCompletionSource<int>();
            var buttons = new ButtonConfig[composition.TotalCards];
            for (int i = 0; i < buttons.Length; i++)
            {
                int facedownPosition = i;
                buttons[i] = new ButtonConfig($"◆\n背面牌 {i + 1}", () => tcs.TrySetResult(facedownPosition));
            }

            ShowCardGrid($"{prompt}\n\n<color=#aaaaaa>已知构成：普通存活 {composition.OrdinarySurvivalCards} / 生存卡 {composition.SurvivalEventCards} / 死亡 {composition.DeathCards}</color>", buttons);
            try
            {
                return await tcs.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                HidePanel();
            }
        }

        public async UniTask ShowResult(string message, CancellationToken cancellationToken = default)
        {
            if (combatSession != null)
            {
                await RequireCombatScreen().ShowFeedbackAsync(message, CombatFeedbackKind.Important, cancellationToken);
                return;
            }
            EnsureInitialized();
            var tcs = new UniTaskCompletionSource();

            ShowPanel(message, new ButtonConfig[]
            {
                new("确认", () => tcs.TrySetResult())
            });

            try
            {
                await tcs.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                HidePanel();
            }
        }

        public async UniTask<int> RequestSelectTarget(string prompt, List<int> validTargetIds, CancellationToken cancellationToken = default)
        {
            if (combatSession != null)
            {
                if (validTargetIds == null || validTargetIds.Count == 0) return -1;
                var choices = new List<CombatScreenChoice>(validTargetIds.Count + 1);
                foreach (int targetId in validTargetIds) choices.Add(new CombatScreenChoice(targetId, ResolveTargetLabel(targetId)));
                choices.Add(new CombatScreenChoice(-1, "取消"));
                return await RequireCombatScreen().ShowChoicesAsync(prompt, choices, cancellationToken);
            }
            EnsureInitialized();
            if (validTargetIds == null || validTargetIds.Count == 0)
                return -1;

            var tcs = new UniTaskCompletionSource<int>();
            var buttons = new ButtonConfig[validTargetIds.Count + 1];
            for (int i = 0; i < validTargetIds.Count; i++)
            {
                int targetId = validTargetIds[i];
                string targetName = resolveTargetName?.Invoke(targetId);
                buttons[i] = new ButtonConfig(string.IsNullOrWhiteSpace(targetName) ? $"猎人 #{targetId}" : targetName, () => tcs.TrySetResult(targetId));
            }
            buttons[buttons.Length - 1] = new ButtonConfig("取消", () => tcs.TrySetResult(-1));

            ShowCardGrid(prompt, buttons, 2);
            try
            {
                return await tcs.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                HidePanel();
            }
        }

        /// <summary>
        /// 玩家在棋盘上点击格子以选择移动目标。左键确认，右键取消（返回 null）。
        /// </summary>
        public async UniTask<Vector2Int?> RequestSelectTile(
            string prompt, List<Vector2Int> validTiles, CancellationToken cancellationToken = default)
        {
            if (combatSession != null) return await SelectBoardTileAsync(prompt, validTiles, null, cancellationToken);
            EnsureInitialized();

            if (_boardManager == null || validTiles == null || validTiles.Count == 0)
                return null;

            _boardVisualizer?.Highlight(validTiles);
            ShowHint($"{prompt}\n<color=#aaaaaa>右键取消</color>");

            var tilePositions = new List<(Vector2Int coord, Vector3 world)>(validTiles.Count);
            float threshold = _boardManager.CellSize * 0.6f;
            foreach (var tile in validTiles)
                tilePositions.Add((tile, _boardManager.TileToWorld(tile)));

            Vector2Int? selected = null;
            try
            {
                while (selected == null)
                {
                    await UniTask.NextFrame(cancellationToken: cancellationToken);
                    if (CardInspectionOverlay.BlocksWorldInput || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) continue;
                    if (Input.GetMouseButtonDown(1)) break;
                    if (Input.GetMouseButtonDown(0) && Camera.main != null)
                    {
                        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                        if (Mathf.Abs(ray.direction.y) > 0.001f)
                        {
                            float t = -ray.origin.y / ray.direction.y;
                            if (t > 0f)
                            {
                                Vector3 hitPoint = ray.origin + ray.direction * t;
                                selected = FindClosestValidTile(hitPoint, tilePositions, threshold);
                            }
                        }
                    }
                }
            }
            finally
            {
                _boardVisualizer?.ClearHighlights();
                HideHint();
            }
            return selected;
        }

        public async UniTask<Vector2Int?> RequestBossDestination(string prompt, List<Vector2Int> validTiles, List<Vector2Int> compliantTiles, CancellationToken cancellationToken = default)
        {
            if (combatSession != null)
            {
                if (validTiles == null || validTiles.Count == 0) return null;
                var view = RequireCombatScreen();
                while (!cancellationToken.IsCancellationRequested)
                {
                    view.HighlightBossDestinations(validTiles, compliantTiles);
                    int choice;
                    try
                    {
                        choice = await view.ShowChoicesAsync(prompt, new[]
                        {
                            new CombatScreenChoice(0, "采用规则落点", compliantTiles != null && compliantTiles.Count > 0 ? $"{FormatTile(compliantTiles[0])} · 不增加命运" : "保持当前位置"),
                            new CombatScreenChoice(1, "在棋盘选择落点", "偏离规则落点会影响所有存活猎人。")
                        }, cancellationToken);
                    }
                    finally
                    {
                        view.ClearBossHighlights();
                    }
                    if (choice == 0) return compliantTiles != null && compliantTiles.Count > 0 ? compliantTiles[0] : validTiles[0];
                    if (choice != 1) continue;
                    Vector2Int? selected = await SelectBoardTileAsync(prompt, validTiles, compliantTiles, cancellationToken);
                    if (selected.HasValue) return selected;
                }
                return null;
            }
            EnsureInitialized();
            if (_boardManager == null || validTiles == null || validTiles.Count == 0) return null;

            _boardVisualizer?.HighlightIntent(validTiles, compliantTiles);
            ShowHint($"{prompt}\n<color=#66dd77>绿色：遵循行动</color>　<color=#ff8833>橙色：偏移，全员命运 +1</color>\n<color=#aaaaaa>右键采用绿色落点</color>");
            var positions = new List<(Vector2Int coord, Vector3 world)>(validTiles.Count);
            foreach (Vector2Int tile in validTiles)
                positions.Add((tile, _boardManager.TileToWorld(tile)));

            float threshold = _boardManager.CellSize * 0.6f;
            try
            {
                while (true)
                {
                    await UniTask.NextFrame(cancellationToken: cancellationToken);
                    if (CardInspectionOverlay.BlocksWorldInput || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) continue;
                    if (Input.GetMouseButtonDown(1)) return null;
                    if (!Input.GetMouseButtonDown(0) || Camera.main == null) continue;
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    if (Mathf.Abs(ray.direction.y) <= 0.001f) continue;
                    float distance = -ray.origin.y / ray.direction.y;
                    if (distance <= 0f) continue;
                    Vector2Int? selected = FindClosestValidTile(ray.origin + ray.direction * distance, positions, threshold);
                    if (selected.HasValue) return selected;
                }
            }
            finally
            {
                _boardVisualizer?.ClearHighlights();
                HideHint();
            }
        }

        public async UniTask<int> RequestBossTarget(string prompt, List<int> validTargetIds, List<int> compliantTargetIds, CancellationToken cancellationToken = default)
        {
            if (combatSession != null)
            {
                if (validTargetIds == null || validTargetIds.Count == 0) return -1;
                int ruleTarget = compliantTargetIds != null && compliantTargetIds.Count > 0 ? compliantTargetIds[0] : validTargetIds[0];
                var choices = new List<CombatScreenChoice>(validTargetIds.Count + 2)
                {
                    new(int.MinValue, "采用规则目标", ResolveTargetLabel(ruleTarget))
                };
                foreach (int targetId in validTargetIds)
                {
                    bool isRuleTarget = compliantTargetIds != null && compliantTargetIds.Contains(targetId);
                    choices.Add(new CombatScreenChoice(targetId, ResolveTargetLabel(targetId), isRuleTarget ? "规则目标 · 无索敌偏移" : $"索敌偏移 · {ResolveTargetLabel(ruleTarget)}命运 +1"));
                }
                while (!cancellationToken.IsCancellationRequested)
                {
                    int choice = await RequireCombatScreen().ShowChoicesAsync(prompt, choices, cancellationToken);
                    if (choice == int.MinValue) return ruleTarget;
                    if (validTargetIds.Contains(choice)) return choice;
                }
                return -1;
            }
            EnsureInitialized();
            if (validTargetIds == null || validTargetIds.Count == 0) return -1;

            var tcs = new UniTaskCompletionSource<int>();
            var buttons = new ButtonConfig[validTargetIds.Count + 1];
            for (int index = 0; index < validTargetIds.Count; index++)
            {
                int targetId = validTargetIds[index];
                string targetName = resolveTargetName?.Invoke(targetId);
                string prefix = compliantTargetIds != null && compliantTargetIds.Contains(targetId) ? "[规则目标] " : "[偏移：规则目标命运+1] ";
                buttons[index] = new ButtonConfig(prefix + (string.IsNullOrWhiteSpace(targetName) ? $"猎人 #{targetId}" : targetName), () => tcs.TrySetResult(targetId));
            }
            buttons[buttons.Length - 1] = new ButtonConfig("采用规则目标", () => tcs.TrySetResult(-1));
            ShowCardGrid(prompt, buttons, 2);
            try
            {
                return await tcs.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                HidePanel();
            }
        }

        public async UniTask<int> RequestSelectCard(string prompt, List<int> validCardIds, CancellationToken cancellationToken = default)
        {
            if (combatSession != null)
            {
                if (validCardIds == null || validCardIds.Count == 0) return -1;
                var choices = new List<CombatScreenChoice>(validCardIds.Count + 1);
                foreach (int cardId in validCardIds)
                {
                    string cardName = combatSession.GetCard(cardId)?.CardName ?? $"行动卡 #{cardId}";
                    choices.Add(new CombatScreenChoice(cardId, cardName));
                }
                choices.Add(new CombatScreenChoice(-1, "取消"));
                return await RequireCombatScreen().ShowChoicesAsync(prompt, choices, cancellationToken);
            }
            EnsureInitialized();
            if (validCardIds == null || validCardIds.Count == 0)
                return -1;

            var tcs = new UniTaskCompletionSource<int>();
            var buttons = new ButtonConfig[validCardIds.Count + 1];
            for (int i = 0; i < validCardIds.Count; i++)
            {
                int cardId = validCardIds[i];
                buttons[i] = new ButtonConfig(
                    $"行动卡 #{cardId}",
                    () => tcs.TrySetResult(cardId));
            }
            buttons[buttons.Length - 1] = new ButtonConfig(
                "取消",
                () => tcs.TrySetResult(-1));

            ShowPanel(prompt, buttons);
            int result = await tcs.Task;
            HidePanel();
            return result;
        }

        public async UniTask<int> RequestSelectOption(string prompt, List<PlayerChoiceOption> options, int cancelOptionId = -1, string cancelLabel = "取消", CancellationToken cancellationToken = default)
        {
            if (combatSession != null)
            {
                if (options == null || options.Count == 0) return cancelOptionId;
                var choices = new List<CombatScreenChoice>(options.Count + 1);
                foreach (PlayerChoiceOption option in options) choices.Add(new CombatScreenChoice(option.Id, option.Label));
                choices.Add(new CombatScreenChoice(cancelOptionId, cancelLabel));
                return await RequireCombatScreen().ShowChoicesAsync(prompt, choices, cancellationToken);
            }
            EnsureInitialized();
            if (options == null || options.Count == 0)
                return cancelOptionId;

            var tcs = new UniTaskCompletionSource<int>();
            var buttons = new ButtonConfig[options.Count + 1];
            for (int i = 0; i < options.Count; i++)
            {
                PlayerChoiceOption option = options[i];
                buttons[i] = new ButtonConfig(option.Label, () => tcs.TrySetResult(option.Id));
            }
            buttons[buttons.Length - 1] = new ButtonConfig(cancelLabel, () => tcs.TrySetResult(cancelOptionId));

            ShowPanel(prompt, buttons);
            try
            {
                return await tcs.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                HidePanel();
            }
        }

        public async UniTask PlayShuffleAndReveal(
            List<HitLocationRuntimeState> allCards,
            List<HitLocationRuntimeState> toReveal,
            CancellationToken cancellationToken = default)
        {
            EventBus.Publish(new HitLocationShuffleStartedEvent());
            bool revealApplied = false;
            try
            {
                await UniTask.Delay(500, cancellationToken: cancellationToken);
                foreach (var state in toReveal)
                {
                    state.Reveal();
                    EventBus.Publish(new HitLocationFlippedFaceUpEvent { CardData = state.Data });
                }
                revealApplied = true;
                await UniTask.Delay(300, cancellationToken: cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                if (revealApplied)
                {
                    foreach (HitLocationRuntimeState state in toReveal)
                    {
                        if (state == null || state.IsDestroyed) continue;
                        state.Hide();
                        EventBus.Publish(new HitLocationFlippedFaceDownEvent { CardData = state.Data });
                    }
                }
                throw;
            }
        }

        public async UniTask<HitLocationRuntimeState> RequestSelectRevealedCard(
            string prompt, List<HitLocationRuntimeState> revealedCards, CancellationToken cancellationToken = default)
        {
            if (combatSession != null) return await RequireCombatScreen().SelectHitLocationAsync(prompt, revealedCards, currentAttackCandidates, cancellationToken);
            EnsureInitialized();
            var tcs = new UniTaskCompletionSource<HitLocationRuntimeState>();

            var buttons = new ButtonConfig[revealedCards.Count];
            for (int i = 0; i < revealedCards.Count; i++)
            {
                var card = revealedCards[i];
                buttons[i] = new ButtonConfig(
                    $"{card.Data.locationName}\nHP:{card.CurrentHp}/{card.Data.maxHp}",
                    () => tcs.TrySetResult(card));
            }

            ShowPanel(prompt, buttons);

            try
            {
                return await tcs.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                HidePanel();
            }
        }

        public async UniTask<WeaponData> RequestSelectWeapon(string prompt, List<WeaponData> candidates, CancellationToken cancellationToken = default)
        {
            if (combatSession != null)
            {
                var choices = new List<CombatScreenChoice>(candidates.Count);
                for (int index = 0; index < candidates.Count; index++)
                {
                    WeaponData weapon = candidates[index];
                    int id = index;
                    choices.Add(new CombatScreenChoice(id, weapon.weaponName, $"力量 {weapon.strengthBonus} · 射程/尝试由武器规则决定"));
                }
                int selected = await RequireCombatScreen().ShowChoicesAsync(prompt, choices, cancellationToken);
                return selected >= 0 && selected < candidates.Count ? candidates[selected] : null;
            }
            EnsureInitialized();
            var tcs = new UniTaskCompletionSource<WeaponData>();

            var buttons = new ButtonConfig[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                var weapon = candidates[i];
                string label = $"{weapon.weaponName}";
                if (PlayableHunterCombatAdapter.TryGetWeaponProfile(weapon, out var profile))
                    label += $"\n威力 {weapon.strengthBonus}  尝试 {profile.Speed}\n射程 {profile.Range}  精准 {profile.Accuracy:+#;-#;0}";
                else
                {
                    if (weapon.strengthBonus != 0) label += $"\n力量+{weapon.strengthBonus}";
                    if (weapon.speedBonus != 0) label += $"  速度+{weapon.speedBonus}";
                }
                buttons[i] = new ButtonConfig(label, () => tcs.TrySetResult(weapon));
            }

            ShowPanel(prompt, buttons);

            try
            {
                return await tcs.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                HidePanel();
            }
        }

        public async UniTask ShowFeedback(string message, CombatFeedbackKind kind, CancellationToken cancellationToken = default)
        {
            if (combatSession == null)
            {
                await ShowResult(message, cancellationToken);
                return;
            }
            await RequireCombatScreen().ShowFeedbackAsync(message, kind, cancellationToken);
        }

        public async UniTask RevealAttackResult(string prompt, CombatAttackRevealContext context, CancellationToken cancellationToken = default)
        {
            if (combatSession == null)
            {
                await RequestRevealAttackResult(prompt, cancellationToken);
                return;
            }
            currentAttackCandidates = context.Candidates;
            await RequireCombatScreen().RevealAttackResultAsync(prompt, context, cancellationToken);
        }

        public async UniTask<bool> ShowBossIntentPreview(string prompt, CombatBossIntentPreviewContext context, CancellationToken cancellationToken = default)
        {
            if (combatSession == null)
            {
                await ShowResult(prompt, cancellationToken);
                return true;
            }
            var text = new System.Text.StringBuilder(prompt);
            text.AppendLine($"\n规则目标：{ResolveTargetLabel(context.ExpectedTargetId)}　选择目标：{ResolveTargetLabel(context.SelectedTargetId)}");
            text.AppendLine($"Boss 移动：{FormatTile(context.Origin)} → {FormatTile(context.SelectedTile)} · 攻击范围 {context.AttackRange}");
            foreach (CombatBossTargetPreview target in context.Targets)
                text.AppendLine($"{ResolveTargetLabel(target.TargetId)} · 距离 {target.Distance} · {(target.InRange ? "在射程内" : "超出射程")}{(target.IsRuleTarget ? " · 规则目标" : string.Empty)}");
            foreach (KeyValuePair<int, int> fate in context.FateByHunterId)
                text.AppendLine($"{ResolveTargetLabel(fate.Key)}命运 +{fate.Value}");
            bool selectedTargetInRange = false;
            foreach (CombatBossTargetPreview target in context.Targets)
                if (target.TargetId == context.SelectedTargetId && target.InRange) selectedTargetInRange = true;
            if (!selectedTargetInRange) text.AppendLine("目标超出射程：该攻击按原规则落空。");
            return await RequireCombatScreen().ShowBossIntentPreviewAsync(text.ToString(), cancellationToken);
        }

        public async UniTask PresentResolvedFocusAsync(int firstColorIndex, int secondColorIndex, CancellationToken cancellationToken = default)
        {
            if (combatSession == null) return;
            await RequireCombatScreen().PresentResolvedFocusAsync(firstColorIndex, secondColorIndex, cancellationToken);
        }

        public async UniTask ShowDeathResultAsync(int selectedIndex, string resultText, CancellationToken cancellationToken = default, string faceTitle = null)
        {
            if (combatSession == null)
            {
                await ShowResult(resultText, cancellationToken);
                return;
            }
            if (deathCardStage == null) throw new InvalidOperationException("死亡结果到达时没有待揭示的死亡牌。");
            PhysicalInteractionScreenView stage = deathCardStage;
            try
            {
                await stage.ShowSelectedResultAsync(selectedIndex, resultText, true, cancellationToken, faceTitle);
            }
            finally
            {
                deathCardStage = null;
                stage.Close();
            }
        }

        public void EndAttackPresentation() => combatScreenView?.EndAttackPresentation();

        private CombatScreenView RequireCombatScreen()
        {
            if (combatScreenView == null) throw new InvalidOperationException("正式战斗屏幕尚未完成初始化。请在打开猎人行动前等待 OpenScreenAsync。");
            return combatScreenView;
        }

        private string ResolveTargetLabel(int targetId)
        {
            string targetName = resolveTargetName?.Invoke(targetId);
            return string.IsNullOrWhiteSpace(targetName) ? $"猎人 #{targetId}" : targetName;
        }

        private static string FormatTile(Vector2Int tile) => $"({tile.x}, {tile.y})";

        private async UniTask<Vector2Int?> SelectBoardTileAsync(string prompt, List<Vector2Int> validTiles, List<Vector2Int> compliantTiles, CancellationToken cancellationToken)
        {
            if (_boardManager == null || validTiles == null || validTiles.Count == 0) return null;
            if (boardCamera == null) throw new MissingReferenceException("正式战斗输入缺少配置的 BoardCamera 引用。");
            CombatScreenView view = RequireCombatScreen();
            if (compliantTiles != null) _boardVisualizer?.HighlightIntent(validTiles, compliantTiles);
            else _boardVisualizer?.Highlight(validTiles);
            view.ShowBoardPrompt(prompt);
            var positions = new List<(Vector2Int coord, Vector3 world)>(validTiles.Count);
            foreach (Vector2Int tile in validTiles) positions.Add((tile, _boardManager.TileToWorld(tile)));
            float threshold = _boardManager.CellSize * 0.6f;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await UniTask.NextFrame(cancellationToken: cancellationToken);
                    if (CardInspectionOverlay.BlocksWorldInput || ScreenModalInputGate.IsBlocked || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) continue;
                    if (Input.GetMouseButtonDown(1)) return null;
                    if (!Input.GetMouseButtonDown(0)) continue;
                    Ray ray = boardCamera.ScreenPointToRay(Input.mousePosition);
                    if (Mathf.Abs(ray.direction.y) <= 0.001f) continue;
                    float distance = -ray.origin.y / ray.direction.y;
                    if (distance <= 0f) continue;
                    Vector2Int? selected = FindClosestValidTile(ray.origin + ray.direction * distance, positions, threshold);
                    if (selected.HasValue) return selected;
                }
                return null;
            }
            finally
            {
                _boardVisualizer?.ClearHighlights();
                view.ClearBoardPrompt();
            }
        }

        // ═══════════════════════════════════════════
        // 屏幕提示 HUD
        // ═══════════════════════════════════════════

        private GameObject _hintGo;
        private Text _hintText;

        private void ShowHint(string message)
        {
            EnsureInitialized();
            if (_hintGo == null)
            {
                _hintGo = new GameObject("TileSelectHint", typeof(RectTransform));
                _hintGo.transform.SetParent(_canvas.transform, false);
                var rt = _hintGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0.12f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                var bg = _hintGo.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.65f);

                var tGo = new GameObject("HintText", typeof(RectTransform));
                tGo.transform.SetParent(_hintGo.transform, false);
                var tRt = tGo.GetComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                _hintText = tGo.AddComponent<Text>();
                _hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _hintText.fontSize = 15;
                _hintText.color = Color.white;
                _hintText.alignment = TextAnchor.MiddleCenter;
                _hintText.supportRichText = true;
            }
            _hintText.text = message;
            _hintGo.SetActive(true);
        }

        private void HideHint()
        {
            _hintGo?.SetActive(false);
        }

        // ═══════════════════════════════════════════
        // 面板显示/隐藏
        // ═══════════════════════════════════════════

        private struct ButtonConfig
        {
            public string Label;
            public System.Action OnClick;
            public ButtonConfig(string label, System.Action onClick)
            {
                Label = label;
                OnClick = onClick;
            }
        }

        private void ShowPanel(string prompt, ButtonConfig[] buttons)
        {
            for (int i = _buttonContainer.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_buttonContainer.GetChild(i).gameObject);

            _promptText.text = prompt;

            float btnWidth = 1f / buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                var btn = buttons[i];
                CreateButton(btn.Label, btn.OnClick,
                    new Vector2(btnWidth * i, 0),
                    new Vector2(btnWidth * (i + 1), 1));
            }

            _panelRoot.SetActive(true);
        }

        private void ShowCardGrid(string prompt, ButtonConfig[] buttons, int maxColumns = 4)
        {
            for (int i = _buttonContainer.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_buttonContainer.GetChild(i).gameObject);

            _promptText.text = prompt;
            int columns = Mathf.Min(Mathf.Max(1, maxColumns), buttons.Length);
            int rows = Mathf.CeilToInt((float)buttons.Length / columns);
            for (int i = 0; i < buttons.Length; i++)
            {
                int column = i % columns;
                int row = i / columns;
                float minX = (float)column / columns;
                float maxX = (float)(column + 1) / columns;
                float minY = 1f - (float)(row + 1) / rows;
                float maxY = 1f - (float)row / rows;
                CreateButton(buttons[i].Label, buttons[i].OnClick, new Vector2(minX, minY), new Vector2(maxX, maxY));
            }

            _panelRoot.SetActive(true);
        }

        private void HidePanel()
        {
            _panelRoot.SetActive(false);
        }

        private void CreateButton(string label, System.Action onClick,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(_buttonContainer, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(4, 4);
            rt.offsetMax = new Vector2(-4, -4);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.45f, 0.65f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.55f, 0.75f);
            colors.pressedColor     = new Color(0.15f, 0.35f, 0.55f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;

            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            text.raycastTarget = false;
        }

        // ═══════════════════════════════════════════
        // 工具方法
        // ═══════════════════════════════════════════

        private static Vector2Int? FindClosestValidTile(
            Vector3 worldPos,
            List<(Vector2Int coord, Vector3 world)> candidates,
            float threshold)
        {
            Vector2Int? best = null;
            float bestDist = threshold;
            foreach (var (coord, world) in candidates)
            {
                float dist = Vector3.Distance(
                    new Vector3(worldPos.x, 0f, worldPos.z),
                    new Vector3(world.x,    0f, world.z));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = coord;
                }
            }
            return best;
        }
    }
}
