using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunt;
using HuntingInDarkness.Hunt;
using TMPro;
using UnityEngine;

namespace UI.Hunt
{
    /// <summary>资源点旁的世界空间采集桌面。逐张点击实体牌，命令仍由 Hunt ActionQueue 串行执行。</summary>
    public sealed class HuntHarvestPanel3D : MonoBehaviour
    {
        private static int nextInputOwnerId;
        private readonly List<HuntHarvestCard3D> cards = new();
        private HuntManager manager;
        private ResourcePointInstance point;
        private PlayableHarvestTransaction transaction;
        private TextMeshPro titleText;
        private TextMeshPro statusText;
        private HuntHarvestControlCard3D closeCard;
        private int inputOwnerId;
        private bool holdsInputGuard;
        private bool operationRunning;

        public bool IsOpen => gameObject.activeSelf;
        public int CardCount => cards.Count;
        public int RevealedCount => transaction?.RevealedCount ?? 0;

        public static HuntHarvestPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("HuntHarvestPanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<HuntHarvestPanel3D>();
            gameObject.SetActive(false);
            return panel;
        }

        private void Awake() => EnsureInputOwnerId();

        public void Show(ResourcePointInstance resourcePoint, HuntManager huntManager, Vector3 worldPosition)
        {
            DismissForSessionChange();
            point = resourcePoint;
            manager = huntManager;
            transaction = null;
            transform.position = worldPosition;
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);
            BuildLayout(HuntHarvestLayout.ClampCardCount(point?.DrawCount ?? 0));
            AcquireInputGuard();
            PresentState();
        }

        public void RequestClose()
        {
            if (operationRunning) return;
            if (transaction != null && !transaction.IsCommitted && !transaction.Cancel())
            {
                statusText.text = "已开始采集，请翻完剩余卡牌";
                PresentCloseCard();
                return;
            }
            CloseImmediately();
        }

        private void BuildLayout(int cardCount)
        {
            ClearLayout();
            titleText = CreateText("Title", new Vector3(0f, 0.04f, 1.10f), 0.17f, new Vector2(4.8f, 0.35f));
            statusText = CreateText("Status", new Vector3(0f, 0.04f, 0.74f), 0.105f, new Vector2(5.0f, 0.32f));
            for (int index = 0; index < cardCount; index++)
            {
                Vector3 cardPosition = HuntHarvestLayout.GetCardLocalPosition(index, cardCount);
                HuntHarvestCard3D card = HuntHarvestCard3D.Create(index, point?.ResourceName, transform, cardPosition);
                card.RevealRequested = RequestReveal;
                cards.Add(card);
            }
            closeCard = HuntHarvestControlCard3D.Create(transform, HuntHarvestLayout.GetCloseCardLocalPosition(cardCount));
            closeCard.Clicked = HandleControlCardClicked;
        }

        private TextMeshPro CreateText(string objectName, Vector3 localPosition, float fontSize, Vector2 size)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var text = textObject.AddComponent<TextMeshPro>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.rectTransform.sizeDelta = size;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private void RequestReveal(int cardIndex)
        {
            if (operationRunning || cardIndex != RevealedCount) return;
            AdvanceAsync().Forget();
        }

        private void HandleControlCardClicked()
        {
            if ((cards.Count == 0 && transaction == null) || (transaction?.IsComplete == true && !transaction.IsCommitted))
            {
                AdvanceAsync().Forget();
                return;
            }
            RequestClose();
        }

        private async UniTaskVoid AdvanceAsync()
        {
            operationRunning = true;
            SetActiveCard(-1);
            PresentCloseCard();
            try
            {
                if (transaction == null)
                {
                    transaction = await manager.PrepareHarvestAsync(point);
                    if (transaction == null)
                    {
                        statusText.text = "资源点状态已经改变，无法采集";
                        return;
                    }
                    if (transaction.CardCount != cards.Count)
                        BuildLayout(transaction.CardCount);
                }

                PlayableHarvestStepResult result = await manager.AdvanceHarvestAsync(transaction);
                if (result.HasRevealedCard && result.RevealedCard.CardIndex >= 0 && result.RevealedCard.CardIndex < cards.Count)
                    await cards[result.RevealedCard.CardIndex].RevealAsync(result.RevealedCard);
                if (!result.Succeeded)
                {
                    statusText.text = string.IsNullOrWhiteSpace(result.Reason) ? "采集未能推进，请重试" : result.Reason;
                    return;
                }
                if (!result.IsCompleted)
                {
                    statusText.text = $"已翻开 {transaction.RevealedCount}/{transaction.CardCount} · 命中 {transaction.RevealedHitCount}";
                    return;
                }

                int obtainedCount = result.Obtained.Count;
                statusText.text = obtainedCount > 0 ? $"采集完成 · 获得 {transaction.ResourceName} ×{obtainedCount}" : "采集完成 · 全部落空";
                ReleaseInputGuard();
            }
            catch (System.OperationCanceledException)
            {
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                if (statusText != null)
                    statusText.text = "采集流程中断，请重试";
            }
            finally
            {
                operationRunning = false;
                PresentState();
            }
        }

        private void PresentState()
        {
            if (titleText == null || statusText == null) return;
            titleText.text = $"{point?.ResourceName ?? "资源点"} · 素材池";
            if (cards.Count == 0)
                statusText.text = "素材池为空，点击下方卡牌确认";
            else if (transaction == null && string.IsNullOrWhiteSpace(statusText.text))
                statusText.text = "依次点击发光卡牌翻开素材";
            int activeIndex = operationRunning || transaction?.IsCommitted == true ? -1 : transaction?.RevealedCount ?? 0;
            SetActiveCard(activeIndex < cards.Count ? activeIndex : -1);
            PresentCloseCard();
        }

        private void SetActiveCard(int index)
        {
            for (int i = 0; i < cards.Count; i++)
                cards[i].SetActiveCard(i == index);
        }

        private void PresentCloseCard()
        {
            if (closeCard == null) return;
            bool canRetryCommit = !operationRunning && transaction?.IsComplete == true && !transaction.IsCommitted;
            bool canClose = !operationRunning && (transaction == null || transaction.IsCommitted || transaction.RevealedCount == 0);
            string label = transaction?.IsCommitted == true ? "收起卡牌" : canRetryCommit ? "重试提交" : cards.Count == 0 && transaction == null ? "确认空素材池" : canClose ? "离开资源点" : "请翻完剩余卡牌";
            closeCard.Present(label, canClose || canRetryCommit);
        }

        private void ClearLayout()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);
            cards.Clear();
            titleText = null;
            statusText = null;
            closeCard = null;
        }

        private void CloseImmediately()
        {
            if (transaction != null && !transaction.IsCommitted)
                transaction.Cancel();
            ReleaseInputGuard();
            transaction = null;
            point = null;
            manager = null;
            operationRunning = false;
            ClearLayout();
            gameObject.SetActive(false);
        }

        public void DismissForSessionChange()
        {
            ReleaseInputGuard();
            transaction = null;
            point = null;
            manager = null;
            operationRunning = false;
            ClearLayout();
            gameObject.SetActive(false);
        }

        private void AcquireInputGuard()
        {
            if (holdsInputGuard) return;
            EnsureInputOwnerId();
            PlayableHuntInputGuard.Acquire(inputOwnerId);
            holdsInputGuard = true;
        }

        private void EnsureInputOwnerId()
        {
            if (inputOwnerId == 0)
                inputOwnerId = ++nextInputOwnerId;
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
