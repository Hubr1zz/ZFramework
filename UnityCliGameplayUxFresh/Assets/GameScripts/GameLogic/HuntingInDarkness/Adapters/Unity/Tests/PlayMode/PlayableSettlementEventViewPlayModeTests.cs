using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.ViewLayer.Settlement;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementEventViewPlayModeTests
    {
        [UnityTest]
        public IEnumerator NarrativePrompt_UsesPhysicalChoiceAndReleasesInput() => UniTask.ToCoroutine(async () =>
        {
            var host = new GameObject("SettlementEventViewPlayModeTest");
            var managerHost = new GameObject("SettlementEventViewPlayModeManager");
            managerHost.SetActive(false);
            var settings = ScriptableObject.CreateInstance<PlayableBootstrapSettings>();
            var destinations = ScriptableObject.CreateInstance<PlayableHuntDestinationCatalog>();
            SetPrivateField(settings, "showSettlementHud", false);
            SetPrivateField(settings, "huntDestinations", destinations);
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.eventName = "桌面事件";
            gameEvent.displayText = "点击实体事件卡继续。";

            try
            {
                GameManager manager = managerHost.AddComponent<GameManager>();
                PlayableGameBootstrap.EnsureRequiredWorldSpacePorts(host, manager, settings);
                PlayableSettlementEventView eventView = host.GetComponent<PlayableSettlementEventView>();
                UniTask prompt = eventView.ConfirmNarrativeAsync(gameEvent, null, CancellationToken.None);
                await UniTask.Yield();

                Assert.That(eventView.IsPresenting, Is.True);
                Assert.That(eventView.ActivePanel, Is.Not.Null);
                Assert.That(eventView.ActivePanel.IsOpen, Is.True);
                Assert.That(eventView.ActivePanel.ChoiceCardCount, Is.EqualTo(1));
                Assert.That(eventView.ActivePanel.InteractableChoiceCount, Is.EqualTo(1));
                Assert.That(PlayableHuntInputGuard.IsBlocked, Is.True);

                TabletopEventChoiceCard3D choiceCard = eventView.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single();
                Assert.That(choiceCard.Clicked, Is.Not.Null);
                choiceCard.Clicked.Invoke();
                await prompt;

                Assert.That(eventView.IsPresenting, Is.False);
                Assert.That(eventView.ActivePanel.IsOpen, Is.False);
                Assert.That(PlayableHuntInputGuard.IsBlocked, Is.False);
            }
            finally
            {
                Object.Destroy(host);
                Object.Destroy(managerHost);
                Object.Destroy(gameEvent);
                Object.Destroy(destinations);
                Object.Destroy(settings);
                await UniTask.Yield();
            }
        });

        [UnityTest]
        public IEnumerator CarriedItemRequirement_EnablesOnlyMatchingPhysicalChoice() => UniTask.ToCoroutine(async () =>
        {
            var host = new GameObject("CarriedItemEventViewTest");
            var managerHost = new GameObject("CarriedItemEventViewManager");
            managerHost.SetActive(false);
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.eventName = "携带物事件";
            gameEvent.eventType = GameEventType.Choice;
            gameEvent.options.Add(new EventOption
            {
                optionText = "使用旧式包扎布",
                alwaysAvailable = false,
                conditions = new List<EventOptionCondition>
                {
                    new() { conditionKind = EventOptionConditionKind.MinimumCarriedItem, key = "weathered_field_dressing", displayName = "旧式包扎布", value = 1 }
                }
            });
            var hunter = new HunterInstance(null, 9201) { Name = "携带者" };

            try
            {
                GameManager manager = managerHost.AddComponent<GameManager>();
                PlayableSettlementEventView eventView = host.AddComponent<PlayableSettlementEventView>();
                eventView.Initialize(manager);
                UniTask<PlayableEventChoiceSelection> unavailablePrompt = eventView.SelectChoiceAsync(gameEvent, hunter, new[] { hunter }, new CarriedItemAvailability(hunter, 0), CancellationToken.None);
                await UniTask.Yield();

                TabletopEventChoiceCard3D unavailableCard = eventView.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single(card => card.DisplayName == "使用旧式包扎布");
                Assert.That(unavailableCard.IsInteractable, Is.False);
                eventView.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single(card => card.DisplayName == "接受沉默").Clicked.Invoke();
                Assert.That((await unavailablePrompt).IsValid, Is.False);

                UniTask<PlayableEventChoiceSelection> availablePrompt = eventView.SelectChoiceAsync(gameEvent, hunter, new[] { hunter }, new CarriedItemAvailability(hunter, 1), CancellationToken.None);
                await UniTask.Yield();
                TabletopEventChoiceCard3D availableCard = eventView.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single(card => card.DisplayName == "使用旧式包扎布");

                Assert.That(availableCard.IsInteractable, Is.True);
                Assert.That(availableCard.GetComponentsInChildren<TMPro.TextMeshPro>(true).Any(text => text.text.Contains("旧式包扎布 ×1")), Is.True);
                availableCard.Clicked.Invoke();
                PlayableEventChoiceSelection selection = await availablePrompt;
                Assert.That(selection.OptionIndex, Is.Zero);
                Assert.That(selection.Actor, Is.SameAs(hunter));
            }
            finally
            {
                Object.Destroy(host);
                Object.Destroy(managerHost);
                Object.Destroy(gameEvent);
                await UniTask.Yield();
            }
        });

        private sealed class CarriedItemAvailability : IPlayableEventResourceAvailability, IPlayableEventItemAvailability
        {
            private readonly HunterInstance owner;
            private readonly int amount;

            public CarriedItemAvailability(HunterInstance owner, int amount)
            {
                this.owner = owner;
                this.amount = amount;
            }

            public PlayableEventResourceScope Scope => PlayableEventResourceScope.HuntCollectibles;
            public int GetAvailableAmount(string resourceId) => 0;
            public int GetAvailableAmount(string itemId, HunterInstance actor) => ReferenceEquals(actor, owner) && itemId == "weathered_field_dressing" ? amount : 0;
        }

        private static void SetPrivateField(object target, string fieldName, object value) => target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
    }
}
