using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace TEngine.RTS
{
    internal sealed class ScriptRuntimeModule : Module, IUpdateModule, IScriptRuntimeModule
    {
        private readonly Dictionary<ulong, ScriptAnchor> _anchors = new Dictionary<ulong, ScriptAnchor>();
        private readonly Dictionary<ulong, UnityWorldObject> _worldObjects = new Dictionary<ulong, UnityWorldObject>();
        private readonly UnityScriptContext _context = new UnityScriptContext();
        private ScriptRuntimeKernel _kernel;
        private long _frameIndex;

        private static ulong GetObjectId(UnityEngine.Object target)
        {
#if UNITY_6000_5_OR_NEWER
            return UnityEngine.EntityId.ToULong(target.GetEntityId());
#else
            return unchecked((uint)target.GetInstanceID());
#endif
        }
        private bool _isRestartingScene;

        public override int Priority => -10;
        public string ActiveGeneration => _kernel?.ActiveGeneration ?? string.Empty;
        public int ActiveInstanceCount => _kernel?.ActiveInstanceCount ?? 0;
        public bool IsRestartingScene => _isRestartingScene;

        public override void OnInit()
        {
            RtsServiceRegistry.Clear();
            _kernel = new ScriptRuntimeKernel();
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            var time = new ScriptTime(elapseSeconds, realElapseSeconds, _frameIndex++);
            _kernel.Tick(in time);
        }

        public void Attach(ScriptAnchor anchor)
        {
            if (anchor == null || string.IsNullOrWhiteSpace(anchor.ScriptId)) return;
            ulong instanceId = GetObjectId(anchor);
            _anchors[instanceId] = anchor;
            if (!_worldObjects.TryGetValue(instanceId, out UnityWorldObject worldObject))
            {
                worldObject = new UnityWorldObject(anchor);
                _worldObjects.Add(instanceId, worldObject);
            }

            if (!string.IsNullOrEmpty(_kernel.ActiveGeneration) &&
                !_kernel.Attach(instanceId, anchor.ScriptId, anchor.InitialConfig, _context, worldObject, out string error))
                _context.LogError(error);
        }

        public void Detach(ScriptAnchor anchor)
        {
            if (anchor == null) return;
            ulong instanceId = GetObjectId(anchor);
            _kernel.Detach(instanceId);
            _anchors.Remove(instanceId);
            _worldObjects.Remove(instanceId);
        }

        public ScriptSwapResult ReplaceProvider(IScriptProvider provider,
            ScriptStateMigrationPolicy statePolicy = ScriptStateMigrationPolicy.PreserveWhenCompatible)
        {
            ScriptSwapResult result = _kernel.ReplaceProvider(provider, statePolicy);
            if (!result.Succeeded) return result;
            foreach (KeyValuePair<ulong, ScriptAnchor> pair in _anchors)
            {
                if (_worldObjects.TryGetValue(pair.Key, out UnityWorldObject worldObject) &&
                    !_kernel.Attach(pair.Key, pair.Value.ScriptId, pair.Value.InitialConfig, _context, worldObject, out string error))
                    _context.LogError(error);
            }
            return ScriptSwapResult.Success(_kernel.ActiveInstanceCount);
        }

        public bool RestartCurrentScene(Action<float> progress = null, Action<bool, string> completed = null)
        {
            if (_isRestartingScene)
            {
                SafeComplete(completed, false, "A scene restart is already in progress.");
                return false;
            }

            ISceneModule sceneModule = ModuleSystem.GetModule<ISceneModule>();
            string location = sceneModule.CurrentMainSceneName;
            if (string.IsNullOrWhiteSpace(location))
            {
                SafeComplete(completed, false, "The current main scene location is empty.");
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.buildIndex == 0)
            {
                SafeComplete(completed, false,
                    "Restarting the bootstrap scene is blocked to avoid duplicating persistent GameEntry objects.");
                return false;
            }

            _isRestartingScene = true;
            RestartCurrentSceneAsync(sceneModule, location, progress, completed).Forget();
            return true;
        }

        private async UniTaskVoid RestartCurrentSceneAsync(ISceneModule sceneModule, string location,
            Action<float> progress, Action<bool, string> completed)
        {
            try
            {
                Scene scene = await sceneModule.LoadSceneAsync(
                    location,
                    LoadSceneMode.Single,
                    suspendLoad: false,
                    priority: 100,
                    gcCollect: false,
                    progressCallBack: value => SafeReportProgress(progress, value));
                bool succeeded = scene.IsValid() && scene.isLoaded;
                string message = succeeded ? string.Empty : $"Scene '{location}' did not finish loading.";
                SafeComplete(completed, succeeded, message);
            }
            catch (Exception exception)
            {
                _context.LogError($"Failed to restart scene '{location}':\n{exception}");
                SafeComplete(completed, false, exception.Message);
            }
            finally
            {
                _isRestartingScene = false;
            }
        }

        private void SafeReportProgress(Action<float> progress, float value)
        {
            try { progress?.Invoke(value); }
            catch (Exception exception) { _context.LogError($"Scene restart progress callback failed:\n{exception}"); }
        }

        private void SafeComplete(Action<bool, string> completed, bool succeeded, string error)
        {
            try { completed?.Invoke(succeeded, error); }
            catch (Exception exception) { _context.LogError($"Scene restart completion callback failed:\n{exception}"); }
        }

        public override void Shutdown()
        {
            _kernel?.Dispose();
            _kernel = null;
            _anchors.Clear();
            _worldObjects.Clear();
            _isRestartingScene = false;
            RtsServiceRegistry.Clear();
        }
    }
}
