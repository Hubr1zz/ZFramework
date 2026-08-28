using System.Collections;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ViewLayer.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class CampaignSaveStatusPresenter3DPlayModeTests
    {
        private GameObject host;
        private GameObject settlementRoot;
        private GameObject huntRoot;
        private CampaignSaveStatusPresenter3D presenter;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("CampaignSaveStatusPresenterTest");
            settlementRoot = new GameObject("SettlementRoot");
            huntRoot = new GameObject("HuntRoot");
            presenter = host.AddComponent<CampaignSaveStatusPresenter3D>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(settlementRoot);
            Object.DestroyImmediate(huntRoot);
        }

        [UnityTest]
        public IEnumerator FailedSave_ShowsRetryAndHidesAfterSuccessfulRetry()
        {
            CampaignSaveStatus status = new(CampaignSaveState.Failed, "写入失败", 1, true);
            GamePhase phase = GamePhase.Settlement;
            bool retryCalled = false;
            presenter.Initialize(() => status, () =>
            {
                retryCalled = true;
                status = CampaignSaveStatus.Idle(2);
                return UniTask.FromResult(true);
            }, () => phase, () => phase == GamePhase.Hunt ? huntRoot.transform : settlementRoot.transform);

            presenter.Refresh();
            Assert.That(presenter.IsPresenting, Is.True);
            Assert.That(presenter.ActiveTitle, Is.EqualTo("战役记录未保存"));
            Assert.That(presenter.ActiveBody, Does.Contain("写入失败"));
            Assert.That(presenter.IsRetryInteractable, Is.True);

            presenter.Retry();
            yield return null;

            Assert.That(retryCalled, Is.True);
            Assert.That(presenter.IsPresenting, Is.False);
        }

        [UnityTest]
        public IEnumerator FailedSave_ReparentsPresentationWhenPhaseRootChanges()
        {
            CampaignSaveStatus status = new(CampaignSaveState.Failed, "需要重试", 3, true);
            GamePhase phase = GamePhase.Settlement;
            presenter.Initialize(() => status, () => UniTask.FromResult(false), () => phase, () => phase == GamePhase.Hunt ? huntRoot.transform : settlementRoot.transform);

            presenter.Refresh();
            Assert.That(presenter.PresentationParent, Is.SameAs(settlementRoot.transform));

            phase = GamePhase.Hunt;
            presenter.Refresh();

            Assert.That(presenter.PresentationParent, Is.SameAs(huntRoot.transform));
            Assert.That(presenter.IsPresenting, Is.True);
            yield return null;
        }
    }
}
