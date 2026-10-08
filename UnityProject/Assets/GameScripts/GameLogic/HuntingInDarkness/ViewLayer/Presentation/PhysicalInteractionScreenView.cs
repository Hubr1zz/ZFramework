using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ViewLayer.Tabletop;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HuntingInDarkness.ViewLayer.Presentation
{
    public sealed class PhysicalInteractionScreenView : MonoBehaviour
    {
        [SerializeField] private RawImage stageImage;
        [SerializeField] private UnityEngine.Camera stageCamera;
        [SerializeField] private Transform stageRoot;
        [SerializeField] private Vector3 stageWorldPosition = new(0f, -100f, 0f);
        [SerializeField] private Vector3 stageWorldRotation;
        [SerializeField] private Vector3 diceCameraLocalPosition = new(0f, 3.8f, -6.5f);
        [SerializeField] private Vector3 diceCameraLocalRotation = new(27f, 0f, 0f);
        [SerializeField] private Vector3 cardCameraLocalPosition = new(0f, 6f, -2f);
        [SerializeField] private Vector3 cardFocusOffset = new(0f, 0.22f, 0f);
        [SerializeField] private Transform runtimeContentRoot;
        [SerializeField] private Transform cardAnchor;
        [SerializeField] private PhysicalSelectionCard3D selectionCardPrefab;
        [SerializeField] private PhysicalDie3D dieD6Prefab;
        [SerializeField] private PhysicalDie3D dieD10Prefab;
        [SerializeField] private GameObject diceTrayPrefab;
        [SerializeField] private GameObject focusDiePrefab;
        [SerializeField, Min(0.01f)] private float diePrefabBaseSize = 0.34f;
        [SerializeField] private Vector2 diceTrayBaseSize = new(2f, 1.55f);
        [SerializeField] private Material[] focusColorMaterials = new Material[3];
        [SerializeField, Min(0.1f)] private float diceStageOrthographicSize = 0.85f;
        [SerializeField, Min(0.1f)] private float cardStageOrthographicSize = 2.25f;
        [SerializeField, Min(0.1f)] private float selectedCardOrthographicSize = 0.75f;
        [SerializeField] private Vector2 cardSize = new(0.6f, 0.9f);
        [SerializeField, Min(0.01f)] private float cardFramePadding = 1.15f;
        [SerializeField] private Vector3 firstFocusDiePosition = new(-0.55f, 0.28f, 0f);
        [SerializeField] private Vector3 secondFocusDiePosition = new(0.55f, 0.28f, 0f);
        [SerializeField] private Vector3 firstFocusDieSpawnRotation = new(420f, 395f, 378f);
        [SerializeField] private Vector3 secondFocusDieSpawnRotation = new(342f, 388f, 407f);
        [SerializeField] private Vector3 firstFocusDieLandingRotation = new(22f, 35f, 18f);
        [SerializeField] private Vector3 secondFocusDieLandingRotation = new(-18f, -28f, -12f);
        [SerializeField, Min(0.05f)] private float focusDieAnimationDuration = 0.55f;
        [SerializeField, Min(0f)] private float resultDisplayDuration = 0.8f;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private ScrollRect resultScrollRect;
        [SerializeField] private Button startButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField, Min(1)] private int cardsPerPage = 12;
        [SerializeField, Min(1)] private int cardColumns = 6;
        [SerializeField, Min(0.01f)] private float cardSpacing = 0.78f;
        [SerializeField, Min(0.01f)] private float cardRowSpacing = 1.08f;
        [SerializeField, Min(0f)] private float shuffleDuration = 0.3f;
        [SerializeField, Min(0.01f)] private float dragStartThreshold = 30f;
        [SerializeField] private LayerMask stageLayerMask;
        [SerializeField, Range(0, 31)] private int stageLayer;

        private readonly List<PhysicalSelectionCard3D> visibleCards = new();
        private readonly List<bool> revealedCards = new();
        private readonly List<string> revealedTitles = new();
        private readonly List<string> revealedResults = new();
        private readonly HashSet<int> selectableCardIndices = new();
        private PhysicalInteractionScreenWindow ownerWindow;
        private UniTaskCompletionSource throwSource;
        private UniTaskCompletionSource continueSource;
        private UniTaskCompletionSource<int> cardSelectionSource;
        private CancellationTokenSource operationCancellationSource;
        private IDisposable inputLease;
        private Action pendingThrowAction;
        private int candidateCount;
        private int currentPage;
        private string cardSelectionContext = string.Empty;
        private string currentCardResult = string.Empty;
        private int throwSignalCount;
        private bool pointerStartedOnStage;
        private bool pointerDragged;
        private Vector2 pointerDownPosition;
        private bool referencesValidated;
        private UnityAction previousPageAction;
        private UnityAction nextPageAction;
        private bool cardSessionActive;
        private bool isShuffling;
        private bool isRevealing;
        private static int openPending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            openPending = 0;
        }

        public static PhysicalInteractionScreenView Current { get; private set; }
        public Transform StageRoot => stageRoot;
        public Transform RuntimeContentRoot => runtimeContentRoot;
        public UnityEngine.Camera StageCamera => stageCamera;
        public PhysicalDie3D DieD6Prefab => dieD6Prefab;
        public PhysicalDie3D DieD10Prefab => dieD10Prefab;
        public GameObject DiceTrayPrefab => diceTrayPrefab;
        public float DiePrefabBaseSize => diePrefabBaseSize;
        public Vector2 DiceTrayBaseSize => diceTrayBaseSize;
        public CancellationToken OperationToken => operationCancellationSource?.Token ?? CancellationToken.None;

        public void DetachStageForStandalonePresentation()
        {
            if (stageRoot == null) throw new MissingReferenceException($"[{nameof(PhysicalInteractionScreenView)}] 缺少物理舞台根节点。对象：{name}");
            stageRoot.SetParent(null, false);
            stageRoot.SetPositionAndRotation(stageWorldPosition, Quaternion.Euler(stageWorldRotation));
            stageRoot.localScale = Vector3.one;
        }

        public void PrepareDiceStage()
        {
            ValidateReferences();
            stageCamera.transform.SetLocalPositionAndRotation(diceCameraLocalPosition, Quaternion.Euler(diceCameraLocalRotation));
            stageCamera.orthographicSize = diceStageOrthographicSize;
        }

        public void PrepareCardStage()
        {
            ValidateReferences();
            stageCamera.orthographicSize = cardStageOrthographicSize;
            stageCamera.transform.localPosition = cardCameraLocalPosition;
            stageCamera.transform.LookAt(cardAnchor.position + cardFocusOffset, stageRoot.up);
        }

        public void ClearStage()
        {
            EndCardSelection();
            if (runtimeContentRoot == null) return;
            for (int index = runtimeContentRoot.childCount - 1; index >= 0; index--)
                Destroy(runtimeContentRoot.GetChild(index).gameObject);
        }

        public void SetStageLayerRecursively(GameObject stageObject)
        {
            if (stageObject == null) throw new ArgumentNullException(nameof(stageObject));
            SetLayerRecursively(stageObject.transform, stageLayer);
        }

        public static async UniTask<PhysicalInteractionScreenView> OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Current != null || Interlocked.CompareExchange(ref openPending, 1, 0) != 0)
                throw new InvalidOperationException("物理交互屏幕已被当前操作占用。");

            PhysicalInteractionScreenWindow window = null;
            PhysicalInteractionScreenView view = null;
            try
            {
                window = await GameModule.UI.ShowUIAsyncAwait<PhysicalInteractionScreenWindow>();
                if (window == null) throw new MissingReferenceException($"[{nameof(PhysicalInteractionScreenView)}] UI 模块未能加载物理交互窗口。");
                view = window.View;
                if (view == null) throw new MissingReferenceException($"[{nameof(PhysicalInteractionScreenView)}] 窗口 Prefab 未绑定 View。");
                cancellationToken.ThrowIfCancellationRequested();
                view.BeginOperation(cancellationToken);
                return view;
            }
            catch
            {
                if (view != null && Current == view) view.Close();
                else if (window != null && Current == null) window.CloseOwnedWindow();
                throw;
            }
            finally
            {
                Interlocked.Exchange(ref openPending, 0);
            }
        }

        public async UniTask WaitForThrowAsync(Action throwAction, CancellationToken cancellationToken)
        {
            using CancellationTokenSource linkedSource = CreateLinkedOperationToken(cancellationToken);
            cancellationToken = linkedSource.Token;
            if (throwAction == null) throw new ArgumentNullException(nameof(throwAction));
            EnsureUsable();
            if (throwSource != null) throw new InvalidOperationException("当前投掷尚未完成。");
            throwSignalCount = 0;
            throwSource = new UniTaskCompletionSource();
            pendingThrowAction = throwAction;
            titleText.text = "实体判定";
            PrepareDiceStage();
            instructionText.text = "点击开始投掷，或在骰盘内拿起骰子后拖出释放。";
            SetResultText(string.Empty);
            startButton.gameObject.SetActive(true);
            startButton.interactable = true;
            startButton.onClick.AddListener(RequestThrow);
            try
            {
                await throwSource.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                startButton.onClick.RemoveListener(RequestThrow);
                startButton.gameObject.SetActive(false);
                startButton.interactable = true;
                pendingThrowAction = null;
                throwSource = null;
            }
        }

        public void PresentThrowResult(string text)
        {
            EnsureUsable();
            titleText.text = "判定结果";
            instructionText.text = "骰子稳定后读取朝上点数。";
            SetResultText(text);
        }

        public void PresentCardsResult(string text)
        {
            EnsureUsable();
            titleText.text = "抽牌结果";
            instructionText.text = "已完成本次牌组操作。";
            SetResultText(text);
        }

        public async UniTask<int> SelectCardAsync(string title, string instruction, int count, CancellationToken cancellationToken, string context = null)
        {
            await BeginCardSelectionAsync(title, instruction, count, cancellationToken, context);
            return await WaitForCardSelectionAsync(instruction, null, cancellationToken);
        }

        public async UniTask BeginCardSelectionAsync(string title, string instruction, int count, CancellationToken cancellationToken, string context = null)
        {
            using CancellationTokenSource linkedSource = CreateLinkedOperationToken(cancellationToken);
            cancellationToken = linkedSource.Token;
            EnsureUsable();
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (cardSessionActive || cardSelectionSource != null) throw new InvalidOperationException("当前已经有一项卡牌选择正在等待。");
            cardSessionActive = true;
            candidateCount = count;
            currentPage = 0;
            cardSelectionContext = context ?? string.Empty;
            currentCardResult = string.Empty;
            revealedCards.Clear();
            revealedTitles.Clear();
            revealedResults.Clear();
            for (int index = 0; index < count; index++)
            {
                revealedCards.Add(false);
                revealedTitles.Add(string.Empty);
                revealedResults.Add(string.Empty);
            }
            titleText.text = title ?? string.Empty;
            instructionText.text = instruction ?? "从背面相同的牌中选择一张。";
            PrepareCardStage();
            SetResultText(FormatCardSelectionContext());
            startButton.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(false);
            previousPageButton.gameObject.SetActive(true);
            nextPageButton.gameObject.SetActive(true);
            previousPageAction = () => ChangeCardPage(-1);
            nextPageAction = () => ChangeCardPage(1);
            previousPageButton.onClick.AddListener(previousPageAction);
            nextPageButton.onClick.AddListener(nextPageAction);
            try
            {
                BuildVisibleCards();
                if (shuffleDuration > 0f)
                {
                    isShuffling = true;
                    await AnimateBackShuffleAsync(shuffleDuration, cancellationToken);
                }
            }
            catch
            {
                cardSessionActive = false;
                throw;
            }
            finally
            {
                isShuffling = false;
                if (!cardSessionActive)
                {
                    previousPageButton.onClick.RemoveListener(previousPageAction);
                    nextPageButton.onClick.RemoveListener(nextPageAction);
                    previousPageAction = null;
                    nextPageAction = null;
                    cardSelectionContext = string.Empty;
                    previousPageButton.gameObject.SetActive(false);
                    nextPageButton.gameObject.SetActive(false);
                }
            }
        }

        public async UniTask<int> WaitForCardSelectionAsync(string instruction, IReadOnlyList<int> selectableIndices, CancellationToken cancellationToken)
        {
            using CancellationTokenSource linkedSource = CreateLinkedOperationToken(cancellationToken);
            cancellationToken = linkedSource.Token;
            EnsureUsable();
            if (!cardSessionActive) throw new InvalidOperationException("尚未开始卡牌选择会话。");
            if (isShuffling || isRevealing || cardSelectionSource != null) throw new InvalidOperationException("卡牌动画或选择尚未完成。");
            var selectable = selectableIndices == null ? new HashSet<int>() : new HashSet<int>(selectableIndices);
            if (selectableIndices == null)
                for (int index = 0; index < candidateCount; index++) selectable.Add(index);
            if (selectable.Count == 0) throw new ArgumentException("至少需要一个可选卡牌位置。", nameof(selectableIndices));
            foreach (int index in selectable)
                if (index < 0 || index >= candidateCount || revealedCards[index]) throw new ArgumentOutOfRangeException(nameof(selectableIndices), $"卡牌位置 {index} 无效或已翻开。");
            instructionText.text = instruction ?? "选择一张背面朝上的牌。";
            SetResultText(FormatCardSessionStatus());
            selectableCardIndices.Clear();
            selectableCardIndices.UnionWith(selectable);
            cardSelectionSource = new UniTaskCompletionSource<int>();
            if (IsCurrentPageVisible()) UpdateVisibleSelection();
            else BuildVisibleCards();
            try
            {
                return await cardSelectionSource.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                cardSelectionSource = null;
                selectableCardIndices.Clear();
                DisableVisibleSelection();
            }
        }

        public async UniTask RevealSelectionAsync(int index, string result, CancellationToken cancellationToken, string faceTitle = null)
        {
            using CancellationTokenSource linkedSource = CreateLinkedOperationToken(cancellationToken);
            cancellationToken = linkedSource.Token;
            EnsureUsable();
            cancellationToken.ThrowIfCancellationRequested();
            if (!cardSessionActive || index < 0 || index >= candidateCount) throw new ArgumentOutOfRangeException(nameof(index));
            if (isShuffling || isRevealing || revealedCards[index]) throw new InvalidOperationException("该卡牌当前不可翻开。");
            revealedCards[index] = true;
            revealedTitles[index] = faceTitle ?? string.Empty;
            revealedResults[index] = result ?? string.Empty;
            currentCardResult = result ?? string.Empty;
            int targetPage = index / cardsPerPage;
            if (currentPage != targetPage)
            {
                currentPage = targetPage;
                BuildVisibleCards();
            }
            PhysicalSelectionCard3D selectedCard = FindVisibleCard(index);
            if (selectedCard == null) throw new InvalidOperationException($"卡牌 {index} 未显示在当前分页。");
            isRevealing = true;
            DisableVisibleSelection();
            try
            {
                await selectedCard.RevealAsync(result ?? string.Empty, cancellationToken, faceTitle);
                SetResultText(FormatCardSessionStatus());
            }
            finally
            {
                isRevealing = false;
            }
        }

        public void EndCardSelection()
        {
            cardSessionActive = false;
            cardSelectionSource?.TrySetCanceled();
            cardSelectionSource = null;
            cardSelectionContext = string.Empty;
            currentCardResult = string.Empty;
            revealedCards.Clear();
            revealedTitles.Clear();
            revealedResults.Clear();
            selectableCardIndices.Clear();
            if (previousPageAction != null) previousPageButton.onClick.RemoveListener(previousPageAction);
            if (nextPageAction != null) nextPageButton.onClick.RemoveListener(nextPageAction);
            previousPageAction = null;
            nextPageAction = null;
            ClearVisibleCards();
            previousPageButton.gameObject.SetActive(false);
            nextPageButton.gameObject.SetActive(false);
        }

        public async UniTask ShowSelectedResultAsync(int selectedIndex, string result, CancellationToken cancellationToken)
        {
            await ShowSelectedResultAsync(selectedIndex, result, false, cancellationToken, null);
        }

        public async UniTask ShowSelectedResultAsync(int selectedIndex, string result, bool pauseForAcknowledgement, CancellationToken cancellationToken, string faceTitle = null)
        {
            if (cardSessionActive)
            {
                await RevealSelectionAsync(selectedIndex, result, cancellationToken, faceTitle);
                if (pauseForAcknowledgement) await WaitForContinueAsync("继续", cancellationToken);
                return;
            }
            using CancellationTokenSource linkedSource = CreateLinkedOperationToken(cancellationToken);
            cancellationToken = linkedSource.Token;
            EnsureUsable();
            cancellationToken.ThrowIfCancellationRequested();
            if (selectedIndex < 0 || selectedIndex >= candidateCount) throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            PhysicalSelectionCard3D selectedCard = FindVisibleCard(selectedIndex);
            if (selectedCard == null) throw new InvalidOperationException($"所选卡牌 {selectedIndex} 已从操作台移除。");
            foreach (PhysicalSelectionCard3D card in visibleCards)
                if (card != null && card != selectedCard)
                    Destroy(card.gameObject);
            visibleCards.Clear();
            selectedCard.transform.SetParent(cardAnchor, false);
            selectedCard.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            selectedCard.transform.localRotation = Quaternion.identity;
            stageCamera.orthographicSize = selectedCardOrthographicSize;
            stageCamera.transform.LookAt(selectedCard.transform.position + cardFocusOffset, stageRoot.up);
            await selectedCard.RevealAsync(result, cancellationToken, faceTitle);
            visibleCards.Add(selectedCard);
            titleText.text = "翻开命运";
            instructionText.text = $"你选择了第 {selectedIndex + 1} 张牌。";
            SetResultText(result);
            startButton.gameObject.SetActive(false);
            if (pauseForAcknowledgement) await WaitForContinueAsync("继续", cancellationToken);
            await UniTask.CompletedTask;
        }

        public async UniTask PresentResolvedFocusAsync(int firstColorIndex, int secondColorIndex, CancellationToken cancellationToken)
        {
            using CancellationTokenSource linkedSource = CreateLinkedOperationToken(cancellationToken);
            cancellationToken = linkedSource.Token;
            EnsureUsable();
            ValidateColorIndex(firstColorIndex);
            ValidateColorIndex(secondColorIndex);
            startButton.gameObject.SetActive(false);
            ClearVisibleCards();
            ClearStage();
            GameObject first = Instantiate(focusDiePrefab, runtimeContentRoot, false);
            GameObject second = Instantiate(focusDiePrefab, runtimeContentRoot, false);
            SetStageLayerRecursively(first);
            SetStageLayerRecursively(second);
            first.transform.localPosition = firstFocusDiePosition + Vector3.up * 1.8f;
            second.transform.localPosition = secondFocusDiePosition + Vector3.up * 1.8f;
            first.transform.localRotation = Quaternion.Euler(firstFocusDieSpawnRotation);
            second.transform.localRotation = Quaternion.Euler(secondFocusDieSpawnRotation);
            ApplyFocusColor(first, firstColorIndex);
            ApplyFocusColor(second, secondColorIndex);
            await AnimateFocusDieAsync(first.transform, firstFocusDiePosition, Quaternion.Euler(firstFocusDieLandingRotation), cancellationToken);
            await AnimateFocusDieAsync(second.transform, secondFocusDiePosition, Quaternion.Euler(secondFocusDieLandingRotation), cancellationToken);
            titleText.text = "灵感结果";
            PrepareDiceStage();
            instructionText.text = "两枚骰子的颜色由本次判定结果确定。";
            SetResultText($"{GetColorName(firstColorIndex)} + {GetColorName(secondColorIndex)}");
            await UniTask.Delay(TimeSpan.FromSeconds(resultDisplayDuration), cancellationToken: cancellationToken);
        }

        public async UniTask WaitForContinueAsync(string instruction, CancellationToken cancellationToken)
        {
            using CancellationTokenSource linkedSource = CreateLinkedOperationToken(cancellationToken);
            cancellationToken = linkedSource.Token;
            EnsureUsable();
            continueSource = new UniTaskCompletionSource();
            continueButton.gameObject.SetActive(true);
            instructionText.text = instruction ?? "继续";
            UnityAction continueAction = () => continueSource?.TrySetResult();
            continueButton.onClick.AddListener(continueAction);
            try
            {
                await continueSource.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                continueButton.onClick.RemoveListener(continueAction);
                continueSource = null;
                continueButton.gameObject.SetActive(false);
            }
        }

        public void Close()
        {
            CancelOperation();
            EndCardSelection();
            if (ownerWindow == null) return;
            PhysicalInteractionScreenWindow window = ownerWindow;
            ownerWindow = null;
            Current = null;
            window.CloseOwnedWindow();
        }

        public void NotifyWindowClosed()
        {
            CancelOperation();
            EndCardSelection();
            ownerWindow = null;
            if (Current == this) Current = null;
        }

        public void BindWindow(PhysicalInteractionScreenWindow window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            ValidateReferences();
            if (Current != null && Current != this) throw new InvalidOperationException("物理交互屏幕 Prefab 已存在另一项活动操作。");
            ownerWindow = window;
            DetachStageForStandalonePresentation();
            Current = this;
            inputLease?.Dispose();
            inputLease = ScreenModalInputGate.Acquire(this);
            continueButton.gameObject.SetActive(false);
            startButton.gameObject.SetActive(false);
            previousPageButton.gameObject.SetActive(false);
            nextPageButton.gameObject.SetActive(false);
        }

        private void Awake()
        {
            ValidateReferences();
            RenderTexture renderTexture = stageImage.texture as RenderTexture;
            if (renderTexture == null) throw new MissingReferenceException($"[{nameof(PhysicalInteractionScreenView)}] RawImage 必须绑定 RenderTexture。对象：{name}");
            stageCamera.targetTexture = renderTexture;
        }

        private void Update()
        {
            if (ownerWindow == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) return;
            TrackPointerGesture();
        }

        private void OnDestroy()
        {
            CancelOperation();
            EndCardSelection();
            inputLease?.Dispose();
            inputLease = null;
            if (Current == this) Current = null;
            if (ownerWindow != null) ownerWindow = null;
            if (stageRoot != null)
            {
                if (Application.isPlaying) Destroy(stageRoot.gameObject);
                else DestroyImmediate(stageRoot.gameObject);
            }
        }

        private void OnDisable()
        {
            if (ownerWindow == null) return;
            CancelOperation();
            EndCardSelection();
            ownerWindow = null;
            if (Current == this) Current = null;
        }

        private void TrackPointerGesture()
        {
            Vector2 pointer = Input.mousePosition;
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(stageImage.rectTransform, pointer, ResolveEventCamera());
            if (Input.GetMouseButtonDown(0) && inside)
            {
                pointerStartedOnStage = true;
                pointerDragged = false;
                pointerDownPosition = pointer;
            }
            if (!pointerStartedOnStage) return;
            if (Vector2.Distance(pointerDownPosition, pointer) >= dragStartThreshold) pointerDragged = true;
            if (!Input.GetMouseButtonUp(0)) return;
            pointerStartedOnStage = false;
            if (throwSource != null && pointerDragged && !inside)
            {
                RequestThrow();
                return;
            }
            if (inside && cardSelectionSource != null) SelectCardAt(pointer);
        }

        private void SelectCardAt(Vector2 pointer)
        {
            if (!TryCreateStageRay(pointer, out Ray ray)) return;
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f, stageLayerMask)) return;
            PhysicalSelectionCard3D card = hit.collider.GetComponentInParent<PhysicalSelectionCard3D>();
            if (card == null || !card.IsSelectable) return;
            int originalIndex = card.OriginalIndex;
            if (originalIndex < 0 || originalIndex >= candidateCount) return;
            cardSelectionSource.TrySetResult(originalIndex);
        }

        private bool TryCreateStageRay(Vector2 pointer, out Ray ray)
        {
            ray = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(stageImage.rectTransform, pointer, ResolveEventCamera(), out Vector2 local)) return false;
            Rect rect = stageImage.rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;
            Vector2 uv = new((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
            ray = stageCamera.ViewportPointToRay(uv);
            return true;
        }

        private void RequestThrow()
        {
            if (throwSource == null || throwSignalCount != 0) return;
            throwSignalCount++;
            startButton.interactable = false;
            startButton.gameObject.SetActive(false);
            try
            {
                pendingThrowAction?.Invoke();
                throwSource.TrySetResult();
            }
            catch (Exception exception)
            {
                throwSource.TrySetException(exception);
            }
        }

        private void ChangeCardPage(int delta)
        {
            if (!cardSessionActive || isShuffling || isRevealing) return;
            int pageCount = GetPageCount();
            currentPage = Mathf.Clamp(currentPage + delta, 0, pageCount - 1);
            SetResultText(FormatCardSessionStatus());
            BuildVisibleCards();
        }

        private void BuildVisibleCards()
        {
            ClearVisibleCards();
            int startIndex = currentPage * cardsPerPage;
            int endIndex = Mathf.Min(candidateCount, startIndex + cardsPerPage);
            int count = endIndex - startIndex;
            for (int offset = 0; offset < count; offset++)
            {
                int originalIndex = startIndex + offset;
                PhysicalSelectionCard3D card = Instantiate(selectionCardPrefab, cardAnchor, false);
                int columns = Mathf.Min(Mathf.Max(1, cardColumns), count);
                int row = offset / columns;
                int rowCount = Mathf.Min(columns, count - row * columns);
                int column = offset % columns;
                float horizontalSpacing = cardSpacing;
                int rowCountTotal = Mathf.CeilToInt(count / (float)columns);
                float verticalSpacing = cardRowSpacing;
                card.transform.localPosition = new Vector3((column - (rowCount - 1) * 0.5f) * horizontalSpacing, 0.22f, (row - (rowCountTotal - 1) * 0.5f) * verticalSpacing);
                card.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                bool isRevealed = originalIndex < revealedCards.Count && revealedCards[originalIndex];
                card.Configure(originalIndex, !isRevealed && cardSelectionSource != null && selectableCardIndices.Contains(originalIndex));
                SetStageLayerRecursively(card.gameObject);
                card.Selected = SelectCard;
                if (isRevealed) card.Reveal(revealedResults[originalIndex], revealedTitles[originalIndex]);
                visibleCards.Add(card);
            }
            int visibleColumns = Mathf.Min(Mathf.Max(1, cardColumns), count);
            int visibleRows = Mathf.CeilToInt(count / (float)Mathf.Max(1, cardColumns));
            FitCardPageToCamera(visibleColumns, visibleRows);
            previousPageButton.interactable = currentPage > 0;
            nextPageButton.interactable = currentPage + 1 < GetPageCount();
        }

        private void FitCardPageToCamera(int columns, int rows)
        {
            float width = (columns - 1) * cardSpacing + cardSize.x;
            float projectedHeight = ((rows - 1) * cardRowSpacing + cardSize.y) * Mathf.Abs(Vector3.Dot(stageCamera.transform.up, stageRoot.forward));
            float aspect = Mathf.Max(0.01f, stageCamera.aspect);
            stageCamera.orthographicSize = Mathf.Max(selectedCardOrthographicSize, projectedHeight * 0.5f, width / (2f * aspect)) * cardFramePadding;
        }

        private void SelectCard(PhysicalSelectionCard3D card)
        {
            if (card == null || cardSelectionSource == null || !card.IsSelectable || isShuffling || isRevealing) return;
            cardSelectionSource.TrySetResult(card.OriginalIndex);
        }

        private async UniTask AnimateBackShuffleAsync(float duration, CancellationToken cancellationToken)
        {
            List<Vector3> positions = new(visibleCards.Count);
            foreach (PhysicalSelectionCard3D card in visibleCards)
            {
                positions.Add(card.transform.localPosition);
                card.Configure(card.OriginalIndex, false);
            }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                for (int index = 0; index < visibleCards.Count; index++)
                {
                    PhysicalSelectionCard3D card = visibleCards[index];
                    if (card == null) continue;
                    Vector3 origin = positions[index];
                    float direction = index % 2 == 0 ? 1f : -1f;
                    Vector3 stackPosition = new(0f, 0.22f + index * 0.001f, index * 0.002f);
                    Vector3 mixedPosition = stackPosition + new Vector3(direction * (0.10f + index % 3 * 0.025f), 0f, 0f);
                    Vector3 currentPosition;
                    if (progress < 0.35f)
                        currentPosition = Vector3.Lerp(origin, stackPosition, progress / 0.35f);
                    else if (progress < 0.65f)
                        currentPosition = Vector3.Lerp(stackPosition, mixedPosition, (progress - 0.35f) / 0.30f);
                    else
                        currentPosition = Vector3.Lerp(mixedPosition, origin, (progress - 0.65f) / 0.35f);
                    card.transform.localPosition = currentPosition;
                }
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
            for (int index = 0; index < visibleCards.Count; index++)
                if (visibleCards[index] != null) visibleCards[index].transform.localPosition = positions[index];
        }

        private void DisableVisibleSelection()
        {
            foreach (PhysicalSelectionCard3D card in visibleCards)
                if (card != null) card.SetSelectable(false);
        }

        private bool IsCurrentPageVisible()
        {
            int startIndex = currentPage * cardsPerPage;
            int endIndex = Mathf.Min(candidateCount, startIndex + cardsPerPage);
            if (visibleCards.Count != endIndex - startIndex) return false;
            for (int index = 0; index < visibleCards.Count; index++)
                if (visibleCards[index] == null || visibleCards[index].OriginalIndex != startIndex + index) return false;
            return true;
        }

        private void UpdateVisibleSelection()
        {
            foreach (PhysicalSelectionCard3D card in visibleCards)
                if (card != null) card.SetSelectable(!revealedCards[card.OriginalIndex] && selectableCardIndices.Contains(card.OriginalIndex));
        }

        private void ClearVisibleCards()
        {
            foreach (PhysicalSelectionCard3D card in visibleCards)
                if (card != null) Destroy(card.gameObject);
            visibleCards.Clear();
        }

        private int GetPageCount() => Mathf.CeilToInt(candidateCount / (float)cardsPerPage);

        private string FormatCardSelectionContext()
        {
            string pageStatus = $"牌组 {candidateCount} 张 · 第 {currentPage + 1}/{GetPageCount()} 页";
            if (string.IsNullOrWhiteSpace(cardSelectionContext)) return pageStatus;
            return $"{cardSelectionContext}\n{pageStatus}";
        }

        private string FormatCardSessionStatus()
        {
            string pageStatus = FormatCardSelectionContext();
            return string.IsNullOrWhiteSpace(currentCardResult) ? pageStatus : $"{currentCardResult}\n\n{pageStatus}";
        }

        private void ApplyFocusColor(GameObject die, int colorIndex)
        {
            Renderer renderer = die.GetComponent<Renderer>();
            if (renderer == null) throw new MissingComponentException($"[{nameof(PhysicalInteractionScreenView)}] 灵感骰缺少 Renderer。");
            renderer.sharedMaterial = focusColorMaterials[colorIndex];
        }

        private void ValidateReferences()
        {
            if (referencesValidated) return;
            if (stageImage == null || stageCamera == null || stageRoot == null || runtimeContentRoot == null || cardAnchor == null || selectionCardPrefab == null || dieD6Prefab == null || dieD10Prefab == null || diceTrayPrefab == null || focusDiePrefab == null || titleText == null || instructionText == null || resultText == null || resultScrollRect == null || startButton == null || continueButton == null || previousPageButton == null || nextPageButton == null || focusColorMaterials == null || focusColorMaterials.Length != 3 || diePrefabBaseSize <= 0f || diceTrayBaseSize.x <= 0f || diceTrayBaseSize.y <= 0f)
                throw new MissingReferenceException($"[{nameof(PhysicalInteractionScreenView)}] 物理交互屏幕引用未完整绑定。对象：{name}");
            if (stageLayerMask.value == 0 || stageLayer < 0 || stageLayer > 31 || stageLayerMask.value != 1 << stageLayer)
                throw new InvalidOperationException($"[{nameof(PhysicalInteractionScreenView)}] 舞台 Layer 与 LayerMask 未配置一致。对象：{name}");
            if (runtimeContentRoot == stageRoot) throw new InvalidOperationException($"[{nameof(PhysicalInteractionScreenView)}] runtimeContentRoot 必须与镜头锚点分开。");
            if (stageImage.texture is not RenderTexture) throw new MissingReferenceException($"[{nameof(PhysicalInteractionScreenView)}] RawImage 未绑定 RenderTexture。对象：{name}");
            if (stageCamera.targetTexture != stageImage.texture) throw new InvalidOperationException($"[{nameof(PhysicalInteractionScreenView)}] 舞台相机和 RawImage 必须使用同一个 RenderTexture。");
            if (stageCamera.cullingMask != stageLayerMask.value) throw new InvalidOperationException($"[{nameof(PhysicalInteractionScreenView)}] 舞台相机必须只渲染舞台 Layer。");
            if (focusColorMaterials[0] == null || focusColorMaterials[1] == null || focusColorMaterials[2] == null) throw new MissingReferenceException($"[{nameof(PhysicalInteractionScreenView)}] 红/蓝/黄结果材质未完整绑定。");
            referencesValidated = true;
        }

        private void SetResultText(string value)
        {
            resultText.text = value ?? string.Empty;
            Canvas.ForceUpdateCanvases();
            resultScrollRect.verticalNormalizedPosition = 1f;
        }

        private void BeginOperation(CancellationToken cancellationToken)
        {
            if (operationCancellationSource != null) throw new InvalidOperationException("物理交互屏幕已在处理另一项操作。");
            operationCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.GetCancellationTokenOnDestroy());
        }

        private void CancelOperation()
        {
            operationCancellationSource?.Cancel();
            operationCancellationSource?.Dispose();
            operationCancellationSource = null;
            throwSource?.TrySetCanceled();
            continueSource?.TrySetCanceled();
            cardSelectionSource?.TrySetCanceled();
            ClearVisibleCards();
            ClearStage();
            inputLease?.Dispose();
            inputLease = null;
        }

        private CancellationTokenSource CreateLinkedOperationToken(CancellationToken cancellationToken)
        {
            EnsureUsable();
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, OperationToken, this.GetCancellationTokenOnDestroy());
        }

        private void EnsureUsable()
        {
            ValidateReferences();
            if (ownerWindow == null || Current != this) throw new InvalidOperationException("物理交互屏幕尚未打开。");
        }

        private static string GetColorName(int index) => index switch { 0 => "红色", 1 => "蓝色", 2 => "黄色", _ => throw new ArgumentOutOfRangeException(nameof(index)) };

        private static void ValidateColorIndex(int index)
        {
            if (index < 0 || index > 2) throw new ArgumentOutOfRangeException(nameof(index));
        }

        private PhysicalSelectionCard3D FindVisibleCard(int originalIndex)
        {
            foreach (PhysicalSelectionCard3D card in visibleCards)
                if (card != null && card.OriginalIndex == originalIndex)
                    return card;
            return null;
        }

        private async UniTask AnimateFocusDieAsync(Transform die, Vector3 landingPosition, Quaternion landingRotation, CancellationToken cancellationToken)
        {
            Vector3 spawnPosition = die.localPosition;
            Quaternion spawnRotation = die.localRotation;
            float startedAt = Time.unscaledTime;
            while (Time.unscaledTime - startedAt < focusDieAnimationDuration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / focusDieAnimationDuration);
                float easedProgress = 1f - (1f - progress) * (1f - progress);
                die.localPosition = Vector3.LerpUnclamped(spawnPosition, landingPosition, easedProgress);
                die.localRotation = Quaternion.SlerpUnclamped(spawnRotation, landingRotation, easedProgress);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
            die.localPosition = landingPosition;
            die.localRotation = landingRotation;
        }

        private UnityEngine.Camera ResolveEventCamera()
        {
            Canvas canvas = stageImage.canvas;
            if (canvas == null) throw new MissingComponentException($"[{nameof(PhysicalInteractionScreenView)}] RawImage 未处于 Canvas 中。");
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            if (canvas.worldCamera == null) throw new MissingReferenceException($"[{nameof(PhysicalInteractionScreenView)}] Screen Space Camera Canvas 未绑定相机。");
            return canvas.worldCamera;
        }

        private static void SetLayerRecursively(Transform node, int layer)
        {
            node.gameObject.layer = layer;
            foreach (Transform child in node)
                SetLayerRecursively(child, layer);
        }

    }
}
