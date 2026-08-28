using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Bootstrap;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableContentSourceSystemTests
    {
        private const string ManifestPath = "Assets/AssetRaw/Configs/HuntingInDarkness/HuntingInDarknessContentSources.asset";
        private PlayableContentSourceSystem system;

        [TearDown]
        public void TearDown()
        {
            if (system != null)
                ReleaseSystem(system);
            PlayableContentSourceSystem.ConfigureLoaderForTests(null);
        }

        [Test]
        public void Manifest_AggregatesExplicitSources()
        {
            PlayableContentSourceManifest manifest = AssetDatabase.LoadAssetAtPath<PlayableContentSourceManifest>(ManifestPath);

            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.TryCreateBundle(out PlayableContentSourceBundle bundle, out PlayableContentDiagnosticReport report), Is.True, report.ToString());
            Assert.That(bundle.Settings, Is.Not.Null);
            Assert.That(bundle.EventsTable, Is.Not.Null);
            Assert.That(bundle.SettlementExtensions, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Prepare_IsIdempotentAndReleasesLoadedManifestOnce()
        {
            PlayableContentSourceManifest manifest = AssetDatabase.LoadAssetAtPath<PlayableContentSourceManifest>(ManifestPath);
            var loader = new FakeLoader(manifest);
            system = CreateSystem(loader);

            PlayableContentSourcePrepareResult result = await system.PrepareAsync();
            PlayableContentSourcePrepareResult second = await system.PrepareAsync();

            Assert.That(result.Succeeded, Is.True, result.Diagnostic);
            Assert.That(second.Succeeded, Is.True);
            Assert.That(loader.LoadCount, Is.EqualTo(1));
            Assert.That(system.Bundle, Is.Not.Null);

            ReleaseSystem(system);
            system = null;

            Assert.That(loader.UnloadCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Prepare_RejectsMissingManifest()
        {
            var loader = new FakeLoader(null);
            system = CreateSystem(loader);

            PlayableContentSourcePrepareResult result = await system.PrepareAsync();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(loader.LoadCount, Is.EqualTo(1));
            Assert.That(loader.UnloadCount, Is.Zero);
        }

        [Test]
        public async Task Prepare_ConcurrentWaitersShareLoadAndReleaseDoesNotLoseLoader()
        {
            PlayableContentSourceManifest manifest = AssetDatabase.LoadAssetAtPath<PlayableContentSourceManifest>(ManifestPath);
            var loader = new DeferredLoader();
            system = CreateSystem(loader);

            UniTask<PlayableContentSourcePrepareResult> first = system.PrepareAsync();
            UniTask<PlayableContentSourcePrepareResult> second = system.PrepareAsync();
            Assert.That(loader.LoadCount, Is.EqualTo(1));

            ReleaseSystem(system);
            system = null;
            loader.Complete(manifest);
            PlayableContentSourcePrepareResult firstResult = await first;
            PlayableContentSourcePrepareResult secondResult = await second;

            Assert.That(firstResult.Succeeded, Is.False);
            Assert.That(secondResult.Succeeded, Is.False);
            Assert.That(loader.UnloadCount, Is.EqualTo(1));
        }

        private sealed class FakeLoader : IPlayableContentSourceLoader
        {
            private readonly PlayableContentSourceManifest manifest;

            public FakeLoader(PlayableContentSourceManifest manifest)
            {
                this.manifest = manifest;
            }

            public int LoadCount { get; private set; }
            public int UnloadCount { get; private set; }

            public UniTask<PlayableContentSourceManifest> LoadAsync(string address, CancellationToken cancellationToken)
            {
                LoadCount++;
                return UniTask.FromResult(manifest);
            }

            public void Unload(PlayableContentSourceManifest loadedManifest)
            {
                UnloadCount++;
            }
        }

        private static PlayableContentSourceSystem CreateSystem(IPlayableContentSourceLoader loader)
        {
            PlayableContentSourceSystem.ConfigureLoaderForTests(loader);
            LogAssert.Expect(LogType.Error, "请必须通过Instance方法来实例化HuntingInDarkness.Bootstrap.PlayableContentSourceSystem类");
            var created = new PlayableContentSourceSystem();
            typeof(PlayableContentSourceSystem).GetMethod("OnInit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(created, null);
            return created;
        }

        private static void ReleaseSystem(PlayableContentSourceSystem created)
        {
            typeof(PlayableContentSourceSystem).GetMethod("OnRelease", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(created, null);
        }

        private sealed class DeferredLoader : IPlayableContentSourceLoader
        {
            private readonly UniTaskCompletionSource<PlayableContentSourceManifest> completion = new();

            public int LoadCount { get; private set; }
            public int UnloadCount { get; private set; }

            public UniTask<PlayableContentSourceManifest> LoadAsync(string address, CancellationToken cancellationToken)
            {
                LoadCount++;
                return completion.Task;
            }

            public void Complete(PlayableContentSourceManifest manifest) => completion.TrySetResult(manifest);

            public void Unload(PlayableContentSourceManifest loadedManifest)
            {
                UnloadCount++;
            }
        }
    }
}
