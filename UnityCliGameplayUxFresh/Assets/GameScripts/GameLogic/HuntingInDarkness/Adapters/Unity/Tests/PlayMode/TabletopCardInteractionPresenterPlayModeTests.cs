using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class TabletopCardInteractionPresenterPlayModeTests
    {
        private const int FrameTimeout = 120;
        private GameObject presenterObject;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (presenterObject != null)
                UnityEngine.Object.Destroy(presenterObject);
            foreach (TabletopRandomCard3D card in UnityEngine.Object.FindObjectsByType<TabletopRandomCard3D>(FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(card.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OldMaid_UsesPhysicalCardViewAndReturnsTheSelectedStableCard()
        {
            TabletopCardInteractionPresenter presenter = CreatePresenter();
            var request = new TabletopRandomInteractionRequest("old-maid-physical", TabletopRandomInteractionKind.OldMaid, "hunter-1", "event", sides: 6, deckId: "faceless-hand");
            UniTask<TabletopRandomInteractionResult>.Awaiter awaiter = presenter.PresentAsync(request, default).GetAwaiter();
            yield return WaitUntil(() => FindCards().Length == 6 && FindCards().All(card => card.IsSelectable), "实体抽鬼牌没有生成可选择的 Cards3D 牌组。");

            TabletopRandomCard3D oldMaid = FindCards().Single(card => card.IsOldMaid);
            Click(oldMaid);
            yield return WaitUntil(() => awaiter.IsCompleted, "选择实体鬼牌后交互没有完成。");
            TabletopRandomInteractionResult result = awaiter.GetResult();

            Assert.That(result.InteractionId, Is.EqualTo(request.InteractionId));
            Assert.That(result.CardIds, Is.EqualTo(new[] { oldMaid.CardId }));
            Assert.That(result.Values, Is.EqualTo(new[] { 1 }));
            Assert.That(oldMaid.IsFaceUp, Is.True);
            yield return null;
            Assert.That(FindCards(), Is.Empty);
            Assert.That(presenter.IsPresenting, Is.False);
        }

        [UnityTest]
        public IEnumerator DragGesture_DoesNotSelectNonDraggableRandomCard()
        {
            TabletopCardInteractionPresenter presenter = CreatePresenter();
            var request = new TabletopRandomInteractionRequest("drag-guard", TabletopRandomInteractionKind.FlipCards, "hunter-1", "event", sides: 4, deckId: "bone-omens");
            UniTask<TabletopRandomInteractionResult>.Awaiter awaiter = presenter.PresentAsync(request, default).GetAwaiter();
            yield return WaitUntil(() => FindCards().Length == 4, "翻牌交互没有生成实体牌组。");
            TabletopRandomCard3D card = FindCards().First(item => item.IsSelectable);

            card.HandlePointerDown(Vector2.zero);
            card.HandlePointerDrag(Vector2.right * 20f, card.transform.position);
            card.HandlePointerUp();
            yield return null;
            Assert.That(awaiter.IsCompleted, Is.False, "超过拖动阈值的手势不应被解释为选择。 ");

            Click(card);
            yield return WaitUntil(() => awaiter.IsCompleted, "短按实体牌后翻牌交互没有完成。");
            Assert.That(awaiter.GetResult().CardIds, Is.EqualTo(new[] { card.CardId }));
        }

        [UnityTest]
        public IEnumerator DrawCards_ExposesOnlyDeckTopAndNeverSelectsTheSameCardTwice()
        {
            TabletopCardInteractionPresenter presenter = CreatePresenter();
            var request = new TabletopRandomInteractionRequest("draw-top", TabletopRandomInteractionKind.DrawCards, "hunter-1", "event", count: 2, sides: 5, deckId: "supply-deck");
            UniTask<TabletopRandomInteractionResult>.Awaiter awaiter = presenter.PresentAsync(request, default).GetAwaiter();
            yield return WaitUntil(() => FindCards().Length == 5 && FindCards().Count(card => card.IsSelectable) == 1, "抽牌交互没有只开放牌堆顶。");
            TabletopRandomCard3D first = FindCards().Single(card => card.IsSelectable);
            Click(first);
            yield return WaitUntil(() => FindCards().Count(card => card.IsSelectable) == 1, "抽取第一张后没有开放新的牌堆顶。");
            TabletopRandomCard3D second = FindCards().Single(card => card.IsSelectable);
            Assert.That(second.CardId, Is.Not.EqualTo(first.CardId));
            Click(second);
            yield return WaitUntil(() => awaiter.IsCompleted, "完成抽牌数量后交互没有结束。");

            TabletopRandomInteractionResult result = awaiter.GetResult();
            Assert.That(result.CardIds, Has.Count.EqualTo(2));
            Assert.That(result.CardIds.Distinct().Count(), Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator DeathDeck_SingleCardRevealsTheStableCardFaceLabel()
        {
            TabletopCardInteractionPresenter presenter = CreatePresenter();
            var request = new TabletopRandomInteractionRequest("death-single", TabletopRandomInteractionKind.DeathDeck, "hunter-1", "event", sides: 1, deckId: "hunter-death", instruction: "牌堆构成：0存活/1死亡；翻面后选择", cardFaceLabels: new[] { "死亡" });
            UniTask<TabletopRandomInteractionResult>.Awaiter awaiter = presenter.PresentAsync(request, default).GetAwaiter();
            yield return WaitUntil(() => FindCards().Length == 1 && FindCards()[0].IsSelectable, "单卡死亡牌堆没有生成可选择的实体牌。");

            TabletopRandomCard3D card = FindCards()[0];
            Assert.That(card.CardId, Is.EqualTo("hunter-death:position-0"));
            Click(card);
            yield return WaitUntil(() => awaiter.IsCompleted, "单卡死亡牌堆选择后交互没有完成。");

            Assert.That(awaiter.GetResult().CardIds, Is.EqualTo(new[] { "hunter-death:position-0" }));
            Assert.That(card.IsFaceUp, Is.True);
            Assert.That(card.DisplayName, Is.EqualTo("死亡"));
        }

        [UnityTest]
        public IEnumerator DeathDeck_MultipleCardsPreserveStablePositionsAndRevealTrueFace()
        {
            TabletopCardInteractionPresenter presenter = CreatePresenter();
            var request = new TabletopRandomInteractionRequest("death-multi", TabletopRandomInteractionKind.DeathDeck, "hunter-1", "event", sides: 3, deckId: "hunter-death", instruction: "牌堆构成：2存活/1死亡；翻面后选择", cardFaceLabels: new[] { "存活", "死亡", "存活" });
            UniTask<TabletopRandomInteractionResult>.Awaiter awaiter = presenter.PresentAsync(request, default).GetAwaiter();
            yield return WaitUntil(() => FindCards().Length == 3 && FindCards().All(card => card.IsSelectable), "多卡死亡牌堆没有生成完整可选择牌组。");

            TabletopRandomCard3D selected = FindCards().Single(card => card.CardId == "hunter-death:position-1");
            Click(selected);
            yield return WaitUntil(() => awaiter.IsCompleted, "多卡死亡牌堆选择后交互没有完成。");

            TabletopRandomInteractionResult result = awaiter.GetResult();
            Assert.That(result.CardIds, Is.EqualTo(new[] { "hunter-death:position-1" }));
            Assert.That(selected.IsFaceUp, Is.True);
            Assert.That(selected.DisplayName, Is.EqualTo("死亡"));
        }

        [UnityTest]
        public IEnumerator Disable_CancelsAndCleansInteractionBeforeAReusableNextRequest()
        {
            var background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "BackgroundInput";
            TabletopCardInteractionPresenter presenter = CreatePresenter();
            UniTask<TabletopRandomInteractionResult>.Awaiter cancelledAwaiter = presenter.PresentAsync(new TabletopRandomInteractionRequest("cancelled", TabletopRandomInteractionKind.FlipCards, "", "", sides: 3, deckId: "omens"), default).GetAwaiter();
            yield return WaitUntil(() => FindCards().Length == 3, "取消测试没有开始实体卡牌交互。");
            Assert.That(background.GetComponent<Collider>().enabled, Is.False);

            presenterObject.SetActive(false);
            yield return WaitUntil(() => cancelledAwaiter.IsCompleted, "禁用表现器后交互没有取消。");
            Assert.Throws<OperationCanceledException>(() => cancelledAwaiter.GetResult());
            Assert.That(background.GetComponent<Collider>().enabled, Is.True);
            yield return null;
            Assert.That(FindCards(), Is.Empty);

            presenterObject.SetActive(true);
            UniTask<TabletopRandomInteractionResult>.Awaiter nextAwaiter = presenter.PresentAsync(new TabletopRandomInteractionRequest("next", TabletopRandomInteractionKind.FlipCards, "", "", sides: 2, deckId: "omens"), default).GetAwaiter();
            yield return WaitUntil(() => FindCards().Length == 2, "取消后的下一次交互没有重新开始。");
            Click(FindCards().First(card => card.IsSelectable));
            yield return WaitUntil(() => nextAwaiter.IsCompleted, "取消后的下一次交互没有完成。");
            Assert.That(nextAwaiter.GetResult().InteractionId, Is.EqualTo("next"));
            UnityEngine.Object.Destroy(background);
        }

        private TabletopCardInteractionPresenter CreatePresenter()
        {
            presenterObject = new GameObject("Tabletop Card Presenter Test");
            TabletopCardInteractionPresenter presenter = presenterObject.AddComponent<TabletopCardInteractionPresenter>();
            SetDuration(presenter, "revealDuration", 0f);
            SetDuration(presenter, "resultDisplayDuration", 0f);
            return presenter;
        }

        private static void SetDuration(TabletopCardInteractionPresenter presenter, string fieldName, float value) => typeof(TabletopCardInteractionPresenter).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(presenter, value);

        private static TabletopRandomCard3D[] FindCards() => UnityEngine.Object.FindObjectsByType<TabletopRandomCard3D>(FindObjectsSortMode.None);

        private static void Click(TabletopRandomCard3D card)
        {
            card.HandlePointerDown(Vector2.zero);
            card.HandlePointerUp();
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string message)
        {
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                if (condition())
                    yield break;
                yield return null;
            }
            Assert.Fail(message);
        }
    }
}
