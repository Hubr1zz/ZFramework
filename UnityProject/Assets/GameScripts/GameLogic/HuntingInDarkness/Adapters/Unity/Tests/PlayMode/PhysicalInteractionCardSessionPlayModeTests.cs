using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ViewLayer.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class PhysicalInteractionCardSessionPlayModeTests
    {
        private GameObject screenObject;
        private PhysicalInteractionScreenView screen;

        [UnityTest]
        public IEnumerator SessionKeepsRevealedSlotsAcrossSelectionsAndPages()
        {
            UniTask<int>.Awaiter pendingSelection = default;
            bool hasPendingSelection = false;
            bool selectionConsumed = false;
            try
            {
                screenObject = TabletopUsabilityPlayModeTests.CreatePhysicalInteractionScreen();
                screen = screenObject.GetComponent<PhysicalInteractionScreenView>();
                screen.BindWindow(new PhysicalInteractionScreenWindow());
                SetFloat(screen, "shuffleDuration", 0f);

                UniTask begin = screen.BeginCardSelectionAsync("命运牌", "选择一张背牌", 13, CancellationToken.None);
                UniTask.Awaiter beginAwaiter = begin.GetAwaiter();
                yield return WaitUntil(() => beginAwaiter.IsCompleted, "牌台没有完成固定牌位初始化。");
                beginAwaiter.GetResult();

                pendingSelection = screen.WaitForCardSelectionAsync("选择第一张", new[] { 0, 1 }, CancellationToken.None).GetAwaiter();
                hasPendingSelection = true;
                selectionConsumed = false;
                yield return null;
                PhysicalSelectionCard3D firstBack = FindCard(0);
                PhysicalSelectionCard3D otherSelectableBack = FindCard(1);
                Assert.That(GetFaceText(firstBack).text, Is.EqualTo("◆"));
                Assert.That(firstBack.IsSelectable, Is.True);
                Assert.That(otherSelectableBack.IsSelectable, Is.True);
                FindCard(2).Selected?.Invoke(FindCard(2));
                Assert.That(pendingSelection.IsCompleted, Is.False, "未列入当前可选集合的slot不应被选择。");

                firstBack.Selected?.Invoke(firstBack);
                yield return WaitUntil(() => pendingSelection.IsCompleted, "选择第一张牌后没有返回原始slot。");
                int firstIndex = pendingSelection.GetResult();
                selectionConsumed = true;
                Assert.That(firstIndex, Is.Zero);

                UniTask reveal = screen.RevealSelectionAsync(firstIndex, "命运结果", CancellationToken.None, "存活");
                UniTask.Awaiter revealAwaiter = reveal.GetAwaiter();
                yield return WaitUntil(() => revealAwaiter.IsCompleted, "第一张牌没有完成翻面。");
                revealAwaiter.GetResult();
                Assert.That(GetFaceText(FindCard(0)).text, Is.EqualTo("存活"));

                pendingSelection = screen.WaitForCardSelectionAsync("选择第二张", new[] { 1, 2 }, CancellationToken.None).GetAwaiter();
                hasPendingSelection = true;
                selectionConsumed = false;
                yield return null;
                Assert.That(FindCard(0).IsSelectable, Is.False);
                Assert.That(GetFaceText(FindCard(0)).text, Is.EqualTo("存活"));
                FindButton("nextPageButton").onClick.Invoke();
                yield return null;
                Assert.That(FindVisibleCards().Select(card => card.OriginalIndex), Is.EqualTo(new[] { 12 }));
                FindButton("previousPageButton").onClick.Invoke();
                yield return null;
                Assert.That(GetFaceText(FindCard(0)).text, Is.EqualTo("存活"), "分页返回后已翻开的卡必须恢复原牌位和正面。");
                Assert.That(FindCard(0).IsSelectable, Is.False);
                Assert.That(FindCard(1).IsSelectable, Is.True);
                Assert.That(FindCard(2).IsSelectable, Is.True);

                screen.EndCardSelection();
                try
                {
                    pendingSelection.GetResult();
                    Assert.Fail("结束牌台会话应取消尚未完成的选择。");
                }
                catch (OperationCanceledException)
                {
                    selectionConsumed = true;
                }
            }
            finally
            {
                if (screen != null)
                {
                    screen.EndCardSelection();
                    screen.NotifyWindowClosed();
                }
                if (hasPendingSelection && !selectionConsumed && pendingSelection.IsCompleted)
                {
                    try { pendingSelection.GetResult(); }
                    catch (OperationCanceledException) { }
                    catch (Exception) { }
                }
                if (screenObject != null) UnityEngine.Object.Destroy(screenObject);
            }
            yield return null;
        }

        private PhysicalSelectionCard3D FindCard(int originalIndex) => FindVisibleCards().Single(card => card.OriginalIndex == originalIndex);

        private PhysicalSelectionCard3D[] FindVisibleCards() => screen.StageRoot.GetComponentsInChildren<PhysicalSelectionCard3D>().Where(card => card.gameObject.activeInHierarchy).ToArray();

        private static TMP_Text GetFaceText(PhysicalSelectionCard3D card) => (TMP_Text)typeof(PhysicalSelectionCard3D).GetField("faceText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(card);

        private static Button FindButton(string fieldName) => (Button)typeof(PhysicalInteractionScreenView).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(PhysicalInteractionScreenView.Current);

        private static void SetFloat(PhysicalInteractionScreenView view, string fieldName, float value) => typeof(PhysicalInteractionScreenView).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, value);

        private static IEnumerator WaitUntil(Func<bool> condition, string message)
        {
            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt < 5f)
            {
                if (condition()) yield break;
                yield return null;
            }
            Assert.Fail(message);
        }
    }
}
