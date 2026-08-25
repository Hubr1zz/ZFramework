using Core;
using NUnit.Framework;

namespace HuntingInDarkness.Tests.EditMode
{
    public sealed class CampaignStartupLifecycleTests
    {
        [Test]
        public void TryBegin_RejectsWhenPlayableEntryIsNotEnabled()
        {
            var lifecycle = new CampaignStartupLifecycle();

            Assert.That(lifecycle.TryBegin(CampaignStartupState.StartingNew, out string reason), Is.False);
            Assert.That(reason, Does.Contain("未启用正式开场入口"));
            Assert.That(lifecycle.State, Is.EqualTo(CampaignStartupState.Active));
        }

        [Test]
        public void CompleteAttempt_ReturnsFailedLoadToAwaitingChoice()
        {
            var lifecycle = new CampaignStartupLifecycle();
            Assert.That(lifecycle.Configure(true), Is.True);
            Assert.That(lifecycle.TryBegin(CampaignStartupState.Loading, out string reason), Is.True, reason);
            Assert.That(lifecycle.State, Is.EqualTo(CampaignStartupState.Loading));
            Assert.That(lifecycle.TryBegin(CampaignStartupState.StartingNew, out reason), Is.False);
            Assert.That(reason, Does.Contain("正在处理中"));

            lifecycle.CompleteAttempt();

            Assert.That(lifecycle.IsRuntimeActive, Is.False);
            Assert.That(lifecycle.State, Is.EqualTo(CampaignStartupState.AwaitingChoice));
            Assert.That(lifecycle.TryBegin(CampaignStartupState.StartingNew, out reason), Is.True, reason);
        }

        [Test]
        public void ActivateRuntime_CompletesEntryAndRejectsDuplicateStart()
        {
            var lifecycle = new CampaignStartupLifecycle();
            Assert.That(lifecycle.Configure(true), Is.True);
            Assert.That(lifecycle.TryBegin(CampaignStartupState.StartingNew, out string reason), Is.True, reason);

            lifecycle.ActivateRuntime();
            lifecycle.CompleteAttempt();

            Assert.That(lifecycle.IsRuntimeActive, Is.True);
            Assert.That(lifecycle.State, Is.EqualTo(CampaignStartupState.Active));
            Assert.That(lifecycle.TryBegin(CampaignStartupState.Loading, out reason), Is.False);
            Assert.That(reason, Does.Contain("已经启动"));
            Assert.That(lifecycle.Configure(false), Is.False);
        }

        [Test]
        public void TryBegin_RejectsNonTransitionalState()
        {
            var lifecycle = new CampaignStartupLifecycle();
            Assert.That(lifecycle.Configure(true), Is.True);

            Assert.That(lifecycle.TryBegin(CampaignStartupState.Active, out string reason), Is.False);
            Assert.That(reason, Does.Contain("状态无效"));
            Assert.That(lifecycle.State, Is.EqualTo(CampaignStartupState.AwaitingChoice));
        }
    }
}
