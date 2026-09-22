using System.Collections;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ViewLayer.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class TabletopGameOverRestartPlayModeTests
    {
        [UnityTest]
        public IEnumerator Restart_KeepsDefeatCardOnFailureAndClosesAfterSuccess()
        {
            var root = new GameObject("TabletopGameOverRestartPlayModeTests");
            var view = root.AddComponent<TabletopGameOverView3D>();
            var backgroundObject = new GameObject("BackgroundCollider");
            BoxCollider backgroundCollider = backgroundObject.AddComponent<BoxCollider>();
            bool inputReleasedBeforeRestart = false;
            view.RestartCommand = () =>
            {
                inputReleasedBeforeRestart = backgroundCollider.enabled;
                return UniTask.FromResult(CampaignRestartResult.Failed("删除失败"));
            };
            view.Show("全部猎人倒下");
            Assert.That(backgroundCollider.enabled, Is.False, "终局卡打开时必须冻结背景实体交互。");

            view.Restart();
            yield return null;

            Assert.That(inputReleasedBeforeRestart, Is.True, "调用重开事务前必须先释放桌面输入。");
            Assert.That(view.IsOpen, Is.True, "失败时必须保留可重试的实体终局卡。");
            Assert.That(backgroundCollider.enabled, Is.False, "重开失败后恢复终局卡时必须重新冻结背景交互。");
            view.RestartCommand = () => UniTask.FromResult(CampaignRestartResult.Success());
            view.Restart();
            yield return null;
            Assert.That(view.IsOpen, Is.False, "权威重启成功后才关闭终局卡。");
            Assert.That(backgroundCollider.enabled, Is.True, "重开成功后必须保持桌面输入已释放。");
            Object.Destroy(root);
            Object.Destroy(backgroundObject);
        }

        [UnityTest]
        public IEnumerator Open_CapturesBackgroundColliderCreatedAfterPresentation()
        {
            var root = new GameObject("TabletopGameOverDynamicColliderTests");
            var view = root.AddComponent<TabletopGameOverView3D>();
            view.Show("全部猎人倒下");

            var backgroundObject = new GameObject("LateBackgroundCollider");
            BoxCollider backgroundCollider = backgroundObject.AddComponent<BoxCollider>();
            Assert.That(backgroundCollider.enabled, Is.True);

            yield return null;

            Assert.That(backgroundCollider.enabled, Is.False, "终局展示期间新出现的背景 Collider 也必须被冻结。");
            view.Hide();
            Assert.That(backgroundCollider.enabled, Is.True, "终局展示关闭后必须恢复动态捕获的 Collider。");
            Object.Destroy(root);
            Object.Destroy(backgroundObject);
        }
    }
}
