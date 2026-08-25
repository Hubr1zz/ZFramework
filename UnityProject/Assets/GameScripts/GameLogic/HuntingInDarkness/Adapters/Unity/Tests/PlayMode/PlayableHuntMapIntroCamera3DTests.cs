using System.Collections;
using System.Reflection;
using HuntingInDarkness.Hunt;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class PlayableHuntMapIntroCamera3DTests
    {
        [UnityTest]
        public IEnumerator Present_InvalidReplacementSettlesPreviousPlanAndReleasesInput()
        {
            var cameraObject = new GameObject("HuntIntroCamera");
            Camera presentationCamera = cameraObject.AddComponent<Camera>();
            var introObject = new GameObject("HuntIntro");
            var intro = introObject.AddComponent<PlayableHuntMapIntroCamera3D>();
            intro.Present(presentationCamera, CreateTilePositions());
            PlayableHuntMapIntroPlan previousPlan = intro.Plan;
            Assert.That(intro.IsPresenting, Is.True);
            Assert.That(PlayableHuntInputGuard.IsBlocked, Is.True);

            intro.Present(presentationCamera, System.Array.Empty<Vector3>());

            Assert.That(intro.IsPresenting, Is.False);
            Assert.That(PlayableHuntInputGuard.IsBlocked, Is.False);
            Assert.That(Vector3.Distance(presentationCamera.transform.position, previousPlan.PlayPosition), Is.LessThan(0.001f));
            Object.Destroy(introObject);
            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Presentation_CompletesWithPausedGameplayTime()
        {
            var cameraObject = new GameObject("PausedHuntIntroCamera");
            Camera presentationCamera = cameraObject.AddComponent<Camera>();
            var introObject = new GameObject("PausedHuntIntro");
            var intro = introObject.AddComponent<PlayableHuntMapIntroCamera3D>();
            typeof(PlayableHuntMapIntroCamera3D).GetField("duration", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(intro, 0.01f);
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            try
            {
                intro.Present(presentationCamera, CreateTilePositions());
                PlayableHuntMapIntroPlan plan = intro.Plan;
                for (int frame = 0; frame < 10 && intro.IsPresenting; frame++)
                    yield return null;

                Assert.That(intro.IsPresenting, Is.False);
                Assert.That(PlayableHuntInputGuard.IsBlocked, Is.False);
                Assert.That(Vector3.Distance(presentationCamera.transform.position, plan.PlayPosition), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(presentationCamera.transform.rotation, plan.Rotation), Is.LessThan(0.01f));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                Object.Destroy(introObject);
                Object.Destroy(cameraObject);
            }
        }

        [UnityTest]
        public IEnumerator Disable_ReleasesOnlyIntroInputOwnership()
        {
            const int externalOwnerId = 2147483001;
            var cameraObject = new GameObject("DisabledHuntIntroCamera");
            Camera presentationCamera = cameraObject.AddComponent<Camera>();
            var introObject = new GameObject("DisabledHuntIntro");
            var intro = introObject.AddComponent<PlayableHuntMapIntroCamera3D>();
            PlayableHuntInputGuard.Acquire(externalOwnerId);
            try
            {
                intro.Present(presentationCamera, CreateTilePositions());
                PlayableHuntMapIntroPlan plan = intro.Plan;
                introObject.SetActive(false);

                Assert.That(intro.IsPresenting, Is.False);
                Assert.That(PlayableHuntInputGuard.IsBlocked, Is.True, "禁用入场 View 不得释放其他流程的输入租约。");
                Assert.That(Vector3.Distance(presentationCamera.transform.position, plan.PlayPosition), Is.LessThan(0.001f));
            }
            finally
            {
                PlayableHuntInputGuard.Release(externalOwnerId);
                Object.Destroy(introObject);
                Object.Destroy(cameraObject);
            }
            Assert.That(PlayableHuntInputGuard.IsBlocked, Is.False);
            yield return null;
        }

        private static Vector3[] CreateTilePositions() => new[]
        {
            new Vector3(-4f, 0f, -3f),
            new Vector3(5f, 0f, 4f)
        };
    }
}
