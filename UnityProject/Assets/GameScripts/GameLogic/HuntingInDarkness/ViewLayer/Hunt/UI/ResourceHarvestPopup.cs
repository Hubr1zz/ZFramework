using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunt;
using HuntingInDarkness.Hunt;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hunt
{
    /// <summary>
    /// 资源采集弹窗（卡牌式翻牌效果）。
    /// 显示 DrawCount 张背面卡 → 玩家点击「翻牌」→ 揭示结果。
    /// </summary>
    public class ResourceHarvestPopup : MonoBehaviour
    {
        public System.Action OnClose;

        private ResourcePointInstance _point;
        private HuntManager           _huntMgr;
        private RectTransform         _cardsContainer;
        private Text                  _resultText;
        private Button                _harvestBtn;
        private Button                _closeBtn;
        private Text                  harvestButtonText;
        private readonly List<Image> cardImages = new();
        private readonly List<Text> cardTexts = new();
        private PlayableHarvestTransaction transaction;
        private static int nextInputOwnerId;
        private int inputOwnerId;
        private bool holdsInputGuard;
        private bool harvestOperationRunning;

        private bool layoutBuilt;

        private void Awake()
        {
            inputOwnerId = ++nextInputOwnerId;
            EnsureLayout();
        }

        private void EnsureLayout()
        {
            if (layoutBuilt) return;
            BuildLayout();
            layoutBuilt = true;
        }

        private void BuildLayout()
        {
            var background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.1f, 0.14f, 0.98f);

            // 标题
            var tGo = new GameObject("Title", typeof(RectTransform)); tGo.transform.SetParent(transform, false);
            var trt = tGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0.85f); trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            tGo.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.2f, 1f);
            HuntUIManager.MakeText(tGo, "T", "资源点采集", 18, TextAnchor.MiddleCenter);

            // 卡牌区
            var cardsGo = new GameObject("Cards", typeof(RectTransform)); cardsGo.transform.SetParent(transform, false);
            _cardsContainer = cardsGo.GetComponent<RectTransform>();
            _cardsContainer.anchorMin = new Vector2(0.05f, 0.35f);
            _cardsContainer.anchorMax = new Vector2(0.95f, 0.84f);
            _cardsContainer.offsetMin = _cardsContainer.offsetMax = Vector2.zero;
            cardsGo.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.1f, 0.9f);
            var hlg = cardsGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8; hlg.padding = new RectOffset(12, 12, 12, 12);
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            // 结果文字
            var resGo = new GameObject("Result", typeof(RectTransform)); resGo.transform.SetParent(transform, false);
            var rrt = resGo.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.05f, 0.22f); rrt.anchorMax = new Vector2(0.95f, 0.34f);
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            _resultText = resGo.AddComponent<Text>();
            _resultText.text      = "";
            _resultText.fontSize  = 13;
            _resultText.alignment = TextAnchor.MiddleCenter;
            _resultText.color     = new Color(0.7f, 1f, 0.7f);
            _resultText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 翻牌按钮
            var hGo = new GameObject("HarvestBtn", typeof(RectTransform)); hGo.transform.SetParent(transform, false);
            var hrt = hGo.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.1f, 0.08f); hrt.anchorMax = new Vector2(0.5f, 0.21f);
            hrt.offsetMin = hrt.offsetMax = Vector2.zero;
            hGo.AddComponent<Image>().color = new Color(0.2f, 0.45f, 0.2f, 1f);
            _harvestBtn = hGo.AddComponent<Button>();
            _harvestBtn.onClick.AddListener(OnClickHarvest);
            harvestButtonText = HuntUIManager.MakeText(hGo, "T", "翻开第一张", 14, TextAnchor.MiddleCenter);

            // 关闭按钮
            var cGo = new GameObject("CloseBtn", typeof(RectTransform)); cGo.transform.SetParent(transform, false);
            var crt = cGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.55f, 0.08f); crt.anchorMax = new Vector2(0.9f, 0.21f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            cGo.AddComponent<Image>().color = new Color(0.3f, 0.18f, 0.15f, 1f);
            _closeBtn = cGo.AddComponent<Button>();
            _closeBtn.onClick.AddListener(Close);
            HuntUIManager.MakeText(cGo, "T", "离开", 14, TextAnchor.MiddleCenter);
        }

        public void Show(ResourcePointInstance point, HunterInstance hunter, HuntManager huntMgr)
        {
            EnsureLayout();
            if (transaction != null && !transaction.IsCommitted && !transaction.Cancel()) return;
            _point    = point;
            _huntMgr  = huntMgr;
            transaction = null;
            _resultText.text = "";
            bool canHarvest = point != null && !point.IsExhausted && point.Resource != null && _huntMgr != null;
            int cardCount = canHarvest ? Mathf.Clamp(point.DrawCount, 0, HarvestDrawPlan.MaximumCardCount) : 0;
            _harvestBtn.interactable = canHarvest;
            _closeBtn.interactable = true;
            harvestButtonText.text = cardCount > 0 ? "翻开第一张" : "确认空素材池";
            AcquireInputGuard();

            BuildCards(cardCount);
            if (!canHarvest)
                _resultText.text = point?.IsExhausted == true ? "这个资源点已经耗尽。" : "无法开始采集。";
        }

        private void BuildCards(int count)
        {
            foreach (Transform t in _cardsContainer) Destroy(t.gameObject);
            cardImages.Clear();
            cardTexts.Clear();

            for (int i = 0; i < count; i++)
            {
                var cardGo = new GameObject($"Card_{i}", typeof(RectTransform));
                cardGo.transform.SetParent(_cardsContainer, false);
                var le = cardGo.AddComponent<LayoutElement>();
                le.preferredWidth = 72; le.preferredHeight = 100;

                var img = cardGo.AddComponent<Image>();
                img.color = new Color(0.15f, 0.2f, 0.28f, 1f);
                Text text = HuntUIManager.MakeText(cardGo, "T", "?", 12, TextAnchor.MiddleCenter);
                cardImages.Add(img);
                cardTexts.Add(text);
            }
        }

        private void OnClickHarvest()
        {
            if (harvestOperationRunning || transaction?.IsCommitted == true) return;
            AdvanceHarvestAsync().Forget();
        }

        private async UniTaskVoid AdvanceHarvestAsync()
        {
            harvestOperationRunning = true;
            _harvestBtn.interactable = false;
            _closeBtn.interactable = false;
            try
            {
                if (transaction == null)
                {
                    transaction = await _huntMgr.PrepareHarvestAsync(_point);
                    if (transaction != null)
                        BuildCards(transaction.CardCount);
                }
                if (transaction == null)
                {
                    _closeBtn.interactable = true;
                    _resultText.text = "资源点状态已经改变，无法继续采集。";
                    return;
                }

                PlayableHarvestStepResult result = await _huntMgr.AdvanceHarvestAsync(transaction);
                if (!result.Succeeded)
                {
                    if (result.HasRevealedCard)
                        RevealCard(result.RevealedCard);
                    if (transaction.IsCancelled)
                    {
                        _harvestBtn.interactable = false;
                        _closeBtn.interactable = true;
                        harvestButtonText.text = "采集已结束";
                    }
                    else
                    {
                        _harvestBtn.interactable = true;
                        _closeBtn.interactable = transaction.RevealedCount == 0;
                        harvestButtonText.text = transaction.IsComplete ? "重试提交" : "重试翻牌";
                    }
                    _resultText.text = string.IsNullOrWhiteSpace(result.Reason) ? "采集未完成，请重试。" : result.Reason;
                    return;
                }
                if (result.HasRevealedCard)
                    RevealCard(result.RevealedCard);

                if (!result.IsCompleted)
                {
                    _harvestBtn.interactable = true;
                    harvestButtonText.text = $"翻开下一张（{transaction.RevealedCount}/{transaction.CardCount}）";
                    _resultText.text = $"已翻开 {transaction.RevealedCount}/{transaction.CardCount} · 命中 {transaction.RevealedHitCount}";
                    return;
                }

                _closeBtn.interactable = true;
                harvestButtonText.text = "采集完成";
                _resultText.text = result.Obtained.Count > 0 ? $"✓ 获得：{_point.ResourceName} ×{result.Obtained.Count}" : "全部落空（很不走运）";
            }
            catch (System.OperationCanceledException)
            {
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                if (_resultText != null)
                    _resultText.text = "采集流程发生异常，请重试。";
                if (_harvestBtn != null)
                    _harvestBtn.interactable = transaction?.IsCommitted != true;
                if (_closeBtn != null)
                    _closeBtn.interactable = transaction == null || transaction.RevealedCount == 0 || transaction.IsCommitted;
            }
            finally
            {
                harvestOperationRunning = false;
            }
        }

        private void RevealCard(HarvestCardResult card)
        {
            if (card.CardIndex < 0 || card.CardIndex >= cardImages.Count) return;
            cardImages[card.CardIndex].color = card.IsHit ? new Color(0.2f, 0.5f, 0.3f, 1f) : new Color(0.42f, 0.18f, 0.16f, 1f);
            cardTexts[card.CardIndex].text = card.IsHit ? _point.ResourceName : "落空";
        }

        private void Close()
        {
            if (transaction != null && !transaction.IsCommitted && !transaction.IsCancelled && !transaction.Cancel()) return;
            ReleaseInputGuard();
            OnClose?.Invoke();
        }

        private void AcquireInputGuard()
        {
            if (holdsInputGuard) return;
            PlayableHuntInputGuard.Acquire(inputOwnerId);
            holdsInputGuard = true;
        }

        private void ReleaseInputGuard()
        {
            if (!holdsInputGuard) return;
            PlayableHuntInputGuard.Release(inputOwnerId);
            holdsInputGuard = false;
        }

        private void OnDisable() => ReleaseInputGuard();
        private void OnDestroy() => ReleaseInputGuard();
    }
}
