using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameLogic;

namespace HuntingInDarkness.Bootstrap
{
    internal interface IPlayableContentSourceLoader
    {
        UniTask<PlayableContentSourceManifest> LoadAsync(string address, CancellationToken cancellationToken);
        void Unload(PlayableContentSourceManifest manifest);
    }

    public readonly struct PlayableContentSourcePrepareResult
    {
        internal PlayableContentSourcePrepareResult(bool succeeded, string diagnostic)
        {
            Succeeded = succeeded;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Diagnostic { get; }
    }

    public sealed class PlayableContentSourceSystem : Singleton<PlayableContentSourceSystem>
    {
        public const string ManifestAddress = "HuntingInDarknessContentSources";

        private static IPlayableContentSourceLoader loaderOverride;
        private IPlayableContentSourceLoader loader;
        private IPlayableContentSourceLoader loadedManifestLoader;
        private PlayableContentSourceManifest loadedManifest;
        private PlayableContentSourceBundle currentBundle;
        private CancellationTokenSource lifetimeCancellation;
        private UniTask<PlayableContentSourcePrepareResult> prepareTask;
        private bool prepareInFlight;
        private int generation;

        public static PlayableContentSourceBundle CurrentBundle => IsValid ? Instance.currentBundle : null;
        public PlayableContentSourceBundle Bundle => currentBundle;

        internal static void ConfigureLoaderForTests(IPlayableContentSourceLoader replacement)
        {
            loaderOverride = replacement;
            if (IsValid) Instance.loader = replacement ?? new YooAssetPlayableContentSourceLoader();
        }

        public UniTask<PlayableContentSourcePrepareResult> PrepareAsync(CancellationToken cancellationToken = default)
        {
            if (currentBundle != null) return UniTask.FromResult(new PlayableContentSourcePrepareResult(true, string.Empty));
            if (prepareInFlight) return prepareTask;
            prepareInFlight = true;
            prepareTask = PrepareCoreAsync(cancellationToken).Preserve();
            return prepareTask;
        }

        protected override void OnInit()
        {
            loader = loaderOverride ?? new YooAssetPlayableContentSourceLoader();
            lifetimeCancellation = new CancellationTokenSource();
        }

        protected override void OnRelease()
        {
            generation++;
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
            if (loadedManifest != null)
            {
                loadedManifestLoader?.Unload(loadedManifest);
                loadedManifest = null;
                loadedManifestLoader = null;
            }
            currentBundle = null;
            prepareInFlight = false;
            prepareTask = default;
            loader = null;
        }

        private async UniTask<PlayableContentSourcePrepareResult> PrepareCoreAsync(CancellationToken requestCancellation)
        {
            int requestGeneration = generation;
            IPlayableContentSourceLoader requestLoader = loader;
            CancellationTokenSource requestLifetime = lifetimeCancellation;
            PlayableContentSourceManifest manifest = null;
            try
            {
                if (requestLoader == null || requestLifetime == null)
                    return new PlayableContentSourcePrepareResult(false, "内容源加载器尚未初始化。");
                CancellationToken lifetimeToken = requestLifetime.Token;
                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(requestCancellation, lifetimeToken);
                manifest = await requestLoader.LoadAsync(ManifestAddress, linkedCancellation.Token);
                if (requestGeneration != generation)
                {
                    if (manifest != null) requestLoader.Unload(manifest);
                    return new PlayableContentSourcePrepareResult(false, "内容源加载世代已经失效。");
                }
                if (manifest == null) return new PlayableContentSourcePrepareResult(false, "内容源 Manifest 加载失败。");
                if (!manifest.TryCreateBundle(out PlayableContentSourceBundle bundle, out PlayableContentDiagnosticReport report))
                {
                    requestLoader.Unload(manifest);
                    return new PlayableContentSourcePrepareResult(false, report.ToString());
                }
                loadedManifest = manifest;
                loadedManifestLoader = requestLoader;
                currentBundle = bundle;
                return new PlayableContentSourcePrepareResult(true, string.Empty);
            }
            catch (OperationCanceledException)
            {
                if (manifest != null) requestLoader?.Unload(manifest);
                return new PlayableContentSourcePrepareResult(false, "内容源加载已取消。");
            }
            catch (Exception exception)
            {
                if (manifest != null) requestLoader?.Unload(manifest);
                return new PlayableContentSourcePrepareResult(false, exception.Message);
            }
            finally
            {
                prepareInFlight = false;
                prepareTask = default;
            }
        }

        private sealed class YooAssetPlayableContentSourceLoader : IPlayableContentSourceLoader
        {
            public UniTask<PlayableContentSourceManifest> LoadAsync(string address, CancellationToken cancellationToken) => GameModule.Resource.LoadAssetAsync<PlayableContentSourceManifest>(address, cancellationToken);

            public void Unload(PlayableContentSourceManifest manifest)
            {
                if (manifest != null) GameModule.Resource.UnloadAsset(manifest);
            }
        }
    }
}
