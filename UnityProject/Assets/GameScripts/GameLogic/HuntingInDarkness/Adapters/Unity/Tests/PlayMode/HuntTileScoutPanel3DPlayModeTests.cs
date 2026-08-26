using System.Collections;
using System.Linq;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class HuntTileScoutPanel3DPlayModeTests
    {
        [UnityTest]
        public IEnumerator PhysicalCards_CancelConfirmAndDisableReleaseInput()
        {
            GameObject host = new("HuntTileScoutPanel3DPlayModeTests");
            HuntTileScoutPanel3D panel = HuntTileScoutPanel3D.Create(host.transform);
            Vector2Int coordinate = new(2, -1);
            Vector2Int confirmedCoordinate = default;
            Vector2Int cancelledCoordinate = default;
            bool confirmed = false;
            bool cancelled = false;

            panel.Present(Vector3.zero, coordinate, "蘑菇森林", value =>
            {
                confirmed = true;
                confirmedCoordinate = value;
            }, value =>
            {
                cancelled = true;
                cancelledCoordinate = value;
            });
            Assert.That(panel.IsOpen, Is.True);
            Assert.That(PlayableHuntInputGuard.IsBlocked, Is.True);
            TabletopEventChoiceCard3D[] choices = panel.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Where(card => card.gameObject.activeSelf).ToArray();
            Assert.That(choices.Select(choice => choice.DisplayName), Is.EqualTo(new[] { "翻开地块", "取消" }));

            choices[1].Clicked.Invoke();
            Assert.That(cancelled, Is.True);
            Assert.That(cancelledCoordinate, Is.EqualTo(coordinate));
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(PlayableHuntInputGuard.IsBlocked, Is.False);
            yield return null;

            panel.Present(Vector3.zero, coordinate, "蘑菇森林", value =>
            {
                confirmed = true;
                confirmedCoordinate = value;
            }, null);
            choices = panel.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Where(card => card.gameObject.activeSelf).ToArray();
            choices.Single(choice => choice.DisplayName == "翻开地块").Clicked.Invoke();
            Assert.That(confirmed, Is.True);
            Assert.That(confirmedCoordinate, Is.EqualTo(coordinate));
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(PlayableHuntInputGuard.IsBlocked, Is.False);
            yield return null;

            panel.Present(Vector3.zero, coordinate, "蘑菇森林", null, null);
            Assert.That(PlayableHuntInputGuard.IsBlocked, Is.True);
            host.SetActive(false);
            Assert.That(PlayableHuntInputGuard.IsBlocked, Is.False);

            Object.Destroy(host);
            yield return null;
        }
    }
}
