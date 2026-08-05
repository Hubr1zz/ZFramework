using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TEngine.RTS.Editor
{
    [InitializeOnLoad]
    internal static class RtsTestFlow
    {
        private const string TEST_SCENE_DIRECTORY = "Assets/AssetRaw/Scenes";
        private const string TEST_SCENE_PATH = TEST_SCENE_DIRECTORY + "/RTSTest.unity";
        private const string TEST_SCENE_MARKER_PATH = TEST_SCENE_DIRECTORY + "/RTSTest.rts-generated.txt";
        private const string TEST_SCENE_LOCATION = "RTSTest";
        private const string PENDING_KEY = "TEngine.RTS.TestFlow.Pending";
        private const string ASSEMBLY_PATH_KEY = "TEngine.RTS.TestFlow.AssemblyPath";
        private const string PROFILE_KEY = "TEngine.RTS.TestFlow.Profile";
        private const string SESSION_ID_KEY = "TEngine.RTS.TestFlow.SessionId";
        private const string BOOTSTRAP_REQUEST_PATH = "Library/TEngineRTS/recreate-bootstrap.request";
        private const string START_REQUEST_PATH = "RTSWorkspace/start-test.request";

        private static double _startedAt;

        static RtsTestFlow()
        {
            EditorApplication.update += UpdatePendingTestFlow;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += ProcessBootstrapRequest;
        }

        internal static string TestScenePath => TEST_SCENE_PATH;

        internal static void StartTestFlow()
        {
            StartSession(RtsSessionCatalog.Active, RtsSessionLaunchProfile.Sandbox);
        }

        internal static void StartActiveSession()
        {
            RtsSessionInfo session = RtsSessionCatalog.Active;
            if (session == null)
            {
                Log.Error("[RTS] No active Session is available.");
                return;
            }
            StartSession(session, session.Descriptor.launchProfile);
        }

        private static void StartSession(RtsSessionInfo session, RtsSessionLaunchProfile profile)
        {
            if (session == null)
            {
                Log.Error("[RTS] Cannot start without an active Session.");
                return;
            }
            if (string.IsNullOrWhiteSpace(session.Descriptor.entryScriptId))
            {
                Log.Error("[RTS] Session '{0}' has no entry ScriptId.", session.Id);
                return;
            }
            if (!Enum.IsDefined(typeof(RtsSessionLaunchProfile), profile))
            {
                Log.Error("[RTS] Session '{0}' has an invalid Launch Profile: {1}.", session.Id, profile);
                return;
            }
            string startupScene = string.IsNullOrWhiteSpace(session.Descriptor.startupScene)
                ? RtsProjectSettings.instance.MainScene
                : session.Descriptor.startupScene;
            if (!File.Exists(Path.Combine(RtsProjectSettings.instance.ProjectRoot, startupScene)))
            {
                Log.Error("[RTS] Session startup scene does not exist: {0}.", startupScene);
                return;
            }
            RtsControlCenter.Open();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Log.Warning("[RTS] Exit Play Mode before starting the test flow.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (profile == RtsSessionLaunchProfile.Sandbox)
            {
                EnsureRequiredAssetFolders();
                EnsureTestScene();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            SessionState.EraseBool(PENDING_KEY);
            SessionState.EraseString(ASSEMBLY_PATH_KEY);
            RtsCompilationService.RequestCompile(result =>
            {
                if (!result.Succeeded)
                {
                    Log.Error("[RTS] Test flow compile failed:\n{0}", result.Diagnostics);
                    return;
                }
                SessionState.SetBool(PENDING_KEY, true);
                SessionState.SetString(ASSEMBLY_PATH_KEY, result.AssemblyPath);
                SessionState.SetInt(PROFILE_KEY, (int)profile);
                SessionState.SetString(SESSION_ID_KEY, session.Id);
                _startedAt = EditorApplication.timeSinceStartup;
                EditorSceneManager.OpenScene(startupScene, OpenSceneMode.Single);
                EditorApplication.EnterPlaymode();
            });
        }

        internal static void RecreateTestScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureRequiredAssetFolders();
            CreateTestScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Log.Info("[RTS] Recreated test scene at {0}.", TEST_SCENE_PATH);
        }

        public static void PrepareTestAssetsForBatch()
        {
            EnsureRequiredAssetFolders();
            EnsureTestScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Log.Info("[RTS] Test assets prepared.");
        }

        public static void RecreateTestSceneForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Cannot recreate RTSTest while entering or running Play Mode.");
            EnsureRequiredAssetFolders();
            CreateTestScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Log.Info("[RTS] Stable RTSTest bootstrap recreated for batch validation.");
        }

        private static void UpdatePendingTestFlow()
        {
            string startRequest = Path.Combine(RtsProjectSettings.instance.ProjectRoot, START_REQUEST_PATH);
            if (File.Exists(startRequest) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(startRequest);
                StartTestFlow();
                return;
            }
            if (!SessionState.GetBool(PENDING_KEY, false)) return;
            if (!EditorApplication.isPlaying) return;
            if (_startedAt <= 0d) _startedAt = EditorApplication.timeSinceStartup;

            string pendingSessionId = SessionState.GetString(SESSION_ID_KEY, RtsProjectSettings.instance.ActiveSessionId);
            RtsSessionInfo pendingSession = RtsSessionCatalog.ReadAll().FirstOrDefault(value =>
                value.Id.Equals(pendingSessionId, StringComparison.OrdinalIgnoreCase));
            var pendingProfile = (RtsSessionLaunchProfile)SessionState.GetInt(PROFILE_KEY, (int)RtsSessionLaunchProfile.Sandbox);
            double timeout = Math.Min(1800, Math.Max(10, pendingSession?.Descriptor.startupTimeoutSeconds ?? 300));
            if (EditorApplication.timeSinceStartup - _startedAt > timeout)
            {
                SessionState.EraseBool(PENDING_KEY);
                SessionState.EraseString(ASSEMBLY_PATH_KEY);
                Log.Error("[RTS] Test flow timed out while waiting for ProcedureStartGame. Check the earlier startup error.");
                return;
            }

            try
            {
                IProcedureModule procedureModule = ModuleSystem.GetModule<IProcedureModule>();
                ProcedureBase current = procedureModule.CurrentProcedure;
                string requiredProcedure = pendingSession?.Descriptor.activationProcedure;
                if (!string.IsNullOrWhiteSpace(requiredProcedure) &&
                    (current == null || current.GetType().FullName != requiredProcedure)) return;
                string requiredScene = pendingSession?.Descriptor.activationScene;
                Scene activeScene = SceneManager.GetActiveScene();
                if (pendingProfile == RtsSessionLaunchProfile.InContext && !string.IsNullOrWhiteSpace(requiredScene) &&
                    !activeScene.name.Equals(requiredScene, StringComparison.OrdinalIgnoreCase) &&
                    !activeScene.path.Equals(requiredScene, StringComparison.OrdinalIgnoreCase)) return;

                SessionState.EraseBool(PENDING_KEY);
                string assemblyPath = SessionState.GetString(ASSEMBLY_PATH_KEY, string.Empty);
                SessionState.EraseString(ASSEMBLY_PATH_KEY);
                RtsSessionLaunchProfile profile = pendingProfile;
                string sessionId = SessionState.GetString(SESSION_ID_KEY, RtsProjectSettings.instance.ActiveSessionId);

                if (profile == RtsSessionLaunchProfile.InContext)
                {
                    ActivateSession(SceneManager.GetActiveScene(), sessionId, assemblyPath, requireWorldHost: false);
                    return;
                }

                IResourceModule resourceModule = ModuleSystem.GetModule<IResourceModule>();
                if (!resourceModule.CheckLocationValid(TEST_SCENE_LOCATION))
                    throw new InvalidOperationException($"YooAsset cannot resolve '{TEST_SCENE_LOCATION}'. Recreate the test scene and restart the flow.");
                ISceneModule sceneModule = ModuleSystem.GetModule<ISceneModule>();
                sceneModule.LoadScene(
                    TEST_SCENE_LOCATION,
                    LoadSceneMode.Single,
                    gcCollect: false,
                    callBack: scene => ActivateSession(scene, sessionId, assemblyPath, requireWorldHost: true));
            }
            catch (GameFrameworkException)
            {
                // Procedure FSM is not initialized yet; try again next Editor update.
            }
            catch (Exception exception)
            {
                SessionState.EraseBool(PENDING_KEY);
                Log.Error("[RTS] Failed to enter test scene:\n{0}", exception);
            }
        }

        private static void EnsureRequiredAssetFolders()
        {
            EnsureAssetFolder("Assets/AssetRaw", "DLL");
            EnsureAssetFolder("Assets/AssetRaw", "Scenes");
        }

        private static void EnsureAssetFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        private static void EnsureTestScene()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!File.Exists(Path.Combine(projectRoot, TEST_SCENE_PATH))) CreateTestScene();
            EnsureTestSceneMarker(projectRoot);
        }

        private static void EnsureTestSceneMarker(string projectRoot)
        {
            string markerPath = Path.Combine(projectRoot, TEST_SCENE_MARKER_PATH);
            if (File.Exists(markerPath)) return;

            File.WriteAllText(markerPath, "Generated by the TEngine RTS test flow. Safe for Zero-RTS export to remove.\n");
            AssetDatabase.ImportAsset(TEST_SCENE_MARKER_PATH, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.EraseBool(PENDING_KEY);
            SessionState.EraseString(ASSEMBLY_PATH_KEY);
            SessionState.EraseString(SESSION_ID_KEY);
            _startedAt = 0d;
        }

        private static void ActivateSession(Scene scene, string sessionId, string assemblyPath, bool requireWorldHost)
        {
            RtsSessionInfo session = RtsSessionCatalog.ReadAll().FirstOrDefault(value =>
                value.Id.Equals(sessionId, StringComparison.OrdinalIgnoreCase));
            if (session == null) throw new InvalidOperationException("RTS Session no longer exists: " + sessionId);
            if (string.IsNullOrWhiteSpace(session.Descriptor.entryScriptId))
                throw new InvalidOperationException("RTS Session has no entry ScriptId: " + sessionId);

            foreach (GameObject candidate in scene.GetRootGameObjects())
            {
                Transform legacy = candidate.transform.Find("RTS Gameplay Anchor");
                if (legacy != null)
                {
                    legacy.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(legacy.gameObject);
                }
                Transform existingOverlay = candidate.transform.Find("RTS Session Overlay");
                if (existingOverlay != null)
                {
                    existingOverlay.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(existingOverlay.gameObject);
                }
                if (candidate.name == "RTS Session Overlay")
                {
                    candidate.SetActive(false);
                    UnityEngine.Object.Destroy(candidate);
                }
            }

            RtsWorldHost worldHost = UnityEngine.Object.FindObjectsOfType<RtsWorldHost>(true)
                .FirstOrDefault(host => host.gameObject.scene == scene);
            if (requireWorldHost && worldHost == null)
                throw new InvalidOperationException("Sandbox Session requires the stable RtsWorldHost in RTSTest.");
            if (!requireWorldHost && worldHost == null)
                Log.Warning("[RTS] InContext Session is using the production startup without RtsWorldHost; scripts requiring world capabilities will fail explicitly.");

            var overlay = new GameObject("RTS Session Overlay");
            overlay.SetActive(false);
            if (worldHost != null) overlay.transform.SetParent(worldHost.transform, false);
            ScriptAnchor anchor = overlay.AddComponent<ScriptAnchor>();
            var serializedAnchor = new SerializedObject(anchor);
            serializedAnchor.FindProperty("scriptId").stringValue = session.Descriptor.entryScriptId;
            serializedAnchor.FindProperty("initialConfig").stringValue = string.Empty;
            serializedAnchor.ApplyModifiedPropertiesWithoutUndo();

            if (string.IsNullOrEmpty(assemblyPath) || !ScriptAssemblyLoader.TryLoadCompiledAssembly(assemblyPath))
            {
                UnityEngine.Object.Destroy(overlay);
                return;
            }
            overlay.SetActive(true);
            Log.Info("[RTS] Session '{0}' activated with {1} at scene '{2}'.", session.Id,
                session.Descriptor.launchProfile, scene.name);
            RtsRuntimeStatus.Write();
        }

        private static void CreateTestScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = TEST_SCENE_LOCATION;

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 13f, -12f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 0.5f));
            camera.orthographic = true;
            camera.orthographicSize = 7.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            new GameObject("UIRoot");

            GameObject bootstrap = new GameObject("RTS Stable Bootstrap");
            bootstrap.AddComponent<RtsWorldHost>();

            EditorSceneManager.SaveScene(scene, TEST_SCENE_PATH);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            EnsureTestSceneMarker(projectRoot);
        }

        private static void ProcessBootstrapRequest()
        {
            string requestPath = Path.Combine(RtsProjectSettings.instance.ProjectRoot, BOOTSTRAP_REQUEST_PATH);
            if (!File.Exists(requestPath)) return;
            File.Delete(requestPath);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Log.Warning("[RTS] Bootstrap request ignored while Play Mode is active.");
                return;
            }
            CreateTestScene();
            AssetDatabase.SaveAssets();
            Log.Info("[RTS] Stable RTSTest bootstrap created from explicit request.");
        }

    }
}
