using System.Collections;
using System.Linq;
using System.Threading;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
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

        private static void SetPrivateField(object target, string fieldName, object value) => target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
    }
}
