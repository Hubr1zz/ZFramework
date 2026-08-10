using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Board;
using GameplayBase.CombatSystem;
using SO.Character;
using UnityEngine;
using UnityEngine.UI;

namespace GameplayBase.CombatSystem
{
    /// <summary>
    /// IPlayerInputProvider 的 UGUI 实现。
    /// 纯 C# 类，由 GameManager 构造并注入 BoardManager / HexBoardVisualizer 引用。
    /// </summary>
    public class UIPlayerInputProvider : IPlayerInputProvider
    {
        private readonly BoardManager _boardManager;
        private readonly HexBoardVisualizer _boardVisualizer;

        // ─── 懒初始化 UI 元素 ───
        private Canvas _canvas;
        private GameObject _panelRoot;
        private Text _promptText;
        private Transform _buttonContainer;
        private bool _initialized;

        public UIPlayerInputProvider(BoardManager boardManager, HexBoardVisualizer boardVisualizer)
        {
            _boardManager    = boardManager;
            _boardVisualizer = boardVisualizer;
        }

        // ═══════════════════════════════════════════
        // 懒初始化
        // ═══════════════════════════════════════════

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            _canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
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
            panelRt.anchorMin = new Vector2(0.25f, 0.3f);
            panelRt.anchorMax = new Vector2(0.75f, 0.7f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            var bg = _panelRoot.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.92f);

            var textGo = new GameObject("Prompt", typeof(RectTransform));
            textGo.transform.SetParent(_panelRoot.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0, 0.35f);
            textRt.anchorMax = new Vector2(1, 1);
            textRt.offsetMin = new Vector2(15, 0);
            textRt.offsetMax = new Vector2(-15, -10);
            _promptText = textGo.AddComponent<Text>();
            _promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _promptText.fontSize = 16;
            _promptText.color = Color.white;
            _promptText.alignment = TextAnchor.MiddleCenter;
            _promptText.supportRichText = true;
            _promptText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var containerGo = new GameObject("Buttons", typeof(RectTransform));
            containerGo.transform.SetParent(_panelRoot.transform, false);
            var containerRt = containerGo.GetComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0.1f, 0.05f);
            containerRt.anchorMax = new Vector2(0.9f, 0.33f);
            containerRt.offsetMin = Vector2.zero;
            containerRt.offsetMax = Vector2.zero;
            _buttonContainer = containerGo.transform;

            _panelRoot.SetActive(false);
        }

        // ═══════════════════════════════════════════
        // IPlayerInputProvider 实现
        // ═══════════════════════════════════════════

        public async UniTask<int> RequestRoll(string prompt, int maxExclusive)
        {
            EnsureInitialized();
            var tcs = new UniTaskCompletionSource<int>();

            ShowPanel(prompt, new ButtonConfig[]
            {
                new("掷骰！", () =>
                {
                    int result = Random.Range(0, maxExclusive);
                    tcs.TrySetResult(result);
                })
            });

            int roll = await tcs.Task;
            HidePanel();
            return roll;
        }

        public async UniTask ShowResult(string message)
        {
            EnsureInitialized();
            var tcs = new UniTaskCompletionSource();

            ShowPanel(message, new ButtonConfig[]
            {
                new("确认", () => tcs.TrySetResult())
            });

            await tcs.Task;
            HidePanel();
        }

        public UniTask<int> RequestSelectTarget(string prompt, List<int> validTargetIds)
        {
            return new UniTask<int>();
        }

        /// <summary>
        /// 玩家在棋盘上点击格子以选择移动目标。左键确认，右键取消（返回 null）。
        /// </summary>
        public async UniTask<Vector2Int?> RequestSelectTile(
            string prompt, List<Vector2Int> validTiles)
        {
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

            while (selected == null)
            {
                await UniTask.NextFrame();

                if (Input.GetMouseButtonDown(1))
                    break;

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

            _boardVisualizer?.ClearHighlights();
            HideHint();
            return selected;
        }

        public async UniTask<int> RequestSelectCard(string prompt, List<int> validCardIds)
        {
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

        public async UniTask PlayShuffleAndReveal(
            List<HitLocationRuntimeState> allCards,
            List<HitLocationRuntimeState> toReveal)
        {
            EventBus.Publish(new HitLocationShuffleStartedEvent());
            await UniTask.Delay(500);

            foreach (var state in toReveal)
            {
                state.Reveal();
                EventBus.Publish(new HitLocationFlippedFaceUpEvent { CardData = state.Data });
            }

            await UniTask.Delay(300);
        }

        public async UniTask<HitLocationRuntimeState> RequestSelectRevealedCard(
            string prompt, List<HitLocationRuntimeState> revealedCards)
        {
            EnsureInitialized();
            var tcs = new UniTaskCompletionSource<HitLocationRuntimeState>();

            var buttons = new ButtonConfig[revealedCards.Count];
            for (int i = 0; i < revealedCards.Count; i++)
            {
                var card = revealedCards[i];
                buttons[i] = new ButtonConfig(
                    $"{card.Data.locationName}\n韧性:{card.Data.toughness}  HP:{card.CurrentHp}",
                    () => tcs.TrySetResult(card));
            }

            ShowPanel(prompt, buttons);

            var result = await tcs.Task;
            HidePanel();
            return result;
        }

        public async UniTask<WeaponData> RequestSelectWeapon(string prompt, List<WeaponData> candidates)
        {
            EnsureInitialized();
            var tcs = new UniTaskCompletionSource<WeaponData>();

            var buttons = new ButtonConfig[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                var weapon = candidates[i];
                string label = $"{weapon.weaponName}";
                if (weapon.strengthBonus != 0) label += $"\n力量+{weapon.strengthBonus}";
                if (weapon.speedBonus != 0) label += $"  速度+{weapon.speedBonus}";
                buttons[i] = new ButtonConfig(label, () => tcs.TrySetResult(weapon));
            }

            ShowPanel(prompt, buttons);

            var result = await tcs.Task;
            HidePanel();
            return result;
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
