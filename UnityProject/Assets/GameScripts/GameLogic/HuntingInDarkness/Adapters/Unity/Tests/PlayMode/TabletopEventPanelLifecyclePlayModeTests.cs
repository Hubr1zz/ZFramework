using System.Collections;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class TabletopEventPanelLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator TemporaryParentDisable_PreservesChoiceAndRestoresModalLease() => UniTask.ToCoroutine(async () =>
        {
            GameObject presentationAssets = TabletopUsabilityPlayModeTests.CreatePresentationAssets();
            var parent = new GameObject("EventPanelLifecycleTestParent");
            int selectedCount = 0;
            TabletopEventPanel3D panel = TabletopEventPanel3D.Create(parent.transform);

            try
            {
                panel.Present(Vector3.zero, "事件", "正文", "选择一项继续", TabletopEventPrimaryTone.Narrative, new[]
                {
                    new TabletopEventChoicePresentation("继续", "", true, "", () => selectedCount++)
                });

                Assert.That(ScreenModalInputGate.IsBlocked, Is.True);
                parent.SetActive(false);
                await UniTask.NextFrame();
                Assert.That(ScreenModalInputGate.IsBlocked, Is.False);

                parent.SetActive(true);
                await UniTask.NextFrame();
                Assert.That(panel.IsOpen, Is.True);
                Assert.That(panel.Choices, Has.Count.EqualTo(1));
                Assert.That(ScreenModalInputGate.IsBlocked, Is.True);
                panel.Choices[0].Click();
                panel.Choices[0].Click();
                Assert.That(selectedCount, Is.EqualTo(1));

                panel.Close();
                await UniTask.NextFrame();
                Assert.That(ScreenModalInputGate.IsBlocked, Is.False);
            }
            finally
            {
                Object.Destroy(parent);
                TabletopUsabilityPlayModeTests.DestroyPresentationAssets(presentationAssets);
                await UniTask.NextFrame();
            }
        });
    }
}
