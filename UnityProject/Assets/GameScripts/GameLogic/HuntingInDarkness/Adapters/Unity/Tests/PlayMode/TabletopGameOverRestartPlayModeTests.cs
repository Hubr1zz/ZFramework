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
            view.RestartCommand = () => UniTask.FromResult(CampaignRestartResult.Failed("删除失败"));
            view.Show("全部猎人倒下");

            view.Restart();
            yield return null;

            Assert.That(view.IsOpen, Is.True, "失败时必须保留可重试的实体终局卡。");
            view.RestartCommand = () => UniTask.FromResult(CampaignRestartResult.Success());
            view.Restart();
            yield return null;
            Assert.That(view.IsOpen, Is.False, "权威重启成功后才关闭终局卡。");
            Object.Destroy(root);
        }
    }
}
