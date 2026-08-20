using System.Collections.Generic;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class TabletopBackgroundInputBlockerTests
    {
        [Test]
        public void Capture_BlocksExistingCollidersAndLeavesLaterModalColliderInteractive()
        {
            var background = new GameObject("BackgroundCollider");
            var modal = new GameObject("ModalCollider");
            BoxCollider backgroundCollider = background.AddComponent<BoxCollider>();
            BoxCollider disabledCollider = background.AddComponent<BoxCollider>();
            disabledCollider.enabled = false;
            try
            {
                TabletopBackgroundInputBlocker blocker = TabletopBackgroundInputBlocker.Capture(new List<Collider> { backgroundCollider, disabledCollider });
                BoxCollider modalCollider = modal.AddComponent<BoxCollider>();

                Assert.That(blocker.BlockedCount, Is.EqualTo(1));
                Assert.That(backgroundCollider.enabled, Is.False);
                Assert.That(disabledCollider.enabled, Is.False);
                Assert.That(modalCollider.enabled, Is.True);

                blocker.Dispose();
                blocker.Dispose();

                Assert.That(backgroundCollider.enabled, Is.True);
                Assert.That(disabledCollider.enabled, Is.False);
                Assert.That(modalCollider.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(background);
                Object.DestroyImmediate(modal);
            }
        }

        [Test]
        public void Dispose_IgnoresBackgroundObjectsDestroyedDuringPrompt()
        {
            var background = new GameObject("DestroyedBackgroundCollider");
            BoxCollider collider = background.AddComponent<BoxCollider>();
            TabletopBackgroundInputBlocker blocker = TabletopBackgroundInputBlocker.Capture(new[] { collider });

            Object.DestroyImmediate(background);

            Assert.That(() => blocker.Dispose(), Throws.Nothing);
            Assert.That(blocker.BlockedCount, Is.Zero);
        }
    }
}
