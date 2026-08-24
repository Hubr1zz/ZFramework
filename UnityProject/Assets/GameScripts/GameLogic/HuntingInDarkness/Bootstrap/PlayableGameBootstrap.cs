using Core;
using HuntingInDarkness.Testing;
using HuntingInDarkness.ViewLayer.Camera;
using HuntingInDarkness.ViewLayer.Combat;
using HuntingInDarkness.ViewLayer.Flow;
using HuntingInDarkness.ViewLayer.Settlement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HuntingInDarkness.Bootstrap
{
    /// <summary>
    /// 将现有 GameManager 接入正式启动场景，不要求修改场景或复制玩法系统。
    /// </summary>
    public sealed class PlayableGameBootstrap : MonoBehaviour
    {
        private const string SettingsPath = "HuntingInDarkness/PlayableBootstrapSettings";
        private static bool installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            installed = false;
        }

        /// <summary>ZFramework Procedure 的正式组合根入口。</summary>
        public static bool EnsureInstalled()
        {
            if (installed)
            {
                if (FindAnyObjectByType<GameManager>() != null || FindAnyObjectByType<PlayableGameBootstrap>() != null || FindAnyObjectByType<StandaloneGameTestEntry>() != null) return true;
                installed = false;
            }

            if (FindAnyObjectByType<GameManager>() != null || FindAnyObjectByType<StandaloneGameTestEntry>() != null)
            {
                installed = true;
                return true;
            }

            var settings = Resources.Load<PlayableBootstrapSettings>(SettingsPath);
            if (settings == null || SceneManager.GetActiveScene().name != settings.EntrySceneName) return false;

            installed = true;
            var installerObject = new GameObject("HuntingInDarkness Runtime Bootstrap");
            installerObject.AddComponent<PlayableGameBootstrap>();
            return true;
        }

        /// <summary>安装可游玩流程必需的世界空间输入端口；不受旧屏幕 HUD 可见性控制。</summary>
        public static void EnsureRequiredWorldSpacePorts(GameObject host, GameManager manager, PlayableBootstrapSettings settings) => PlayableSettlementWorldSpacePortInstaller.EnsureInstalled(host, manager, settings);

        private void Start()
        {
            if (GameManager.Instance != null) return;

            var settings = Resources.Load<PlayableBootstrapSettings>(SettingsPath);
            if (settings == null)
            {
                Debug.LogError($"[PlayableGameBootstrap] 缺少 Resources/{SettingsPath}.asset，游戏无法启动。", this);
                return;
            }

            if (!PlayableCampaignContentAssembler.TryBuild(settings, out PlayableCampaignContentCandidate contentCandidate, out PlayableContentDiagnosticReport buildReport))
            {
                Debug.LogError($"[PlayableGameBootstrap] 内容装配失败：{buildReport}", settings);
                return;
            }

            if (!PlayableCampaignContentAssembler.Install(contentCandidate, out PlayableContentDiagnosticReport installReport))
            {
                Debug.LogError($"[PlayableGameBootstrap] 内容安装失败：{installReport}", settings);
                return;
            }

            var managerObject = new GameObject("GameManager (Playable)");
            managerObject.SetActive(false);
            var manager = managerObject.AddComponent<GameManager>();
            manager.ConfigurePlayableRuntime(contentCandidate.DefaultBattleSetup, contentCandidate.CellSize);
            manager.ConfigureSettlementContent(contentCandidate.SettlementContent);
            manager.ConfigureWorkshopContent(contentCandidate.WorkshopContent);
            manager.ConfigurePlayableStartup(settings.ShowStartMenu);
            EnsureRequiredWorldSpacePorts(gameObject, manager, settings);
            managerObject.SetActive(true);

            var mainCamera = Camera.main;
            if (mainCamera != null)
                mainCamera.gameObject.AddComponent<PlayablePhaseCameraRig>().Initialize(manager, settings.BossCameraPosition, settings.BossCameraEulerAngles, settings.KeyLightColor, settings.KeyLightIntensity);
            gameObject.AddComponent<PlayableBossVitalityView>().Initialize(manager);

            if (settings.HideFrameworkDebugger && TEngine.Debugger.Instance != null)
                TEngine.Debugger.Instance.ActiveWindow = false;

            if (settings.ShowStartMenu || settings.ShowFlowGuide && settings.ShowOpeningNarrative)
                gameObject.AddComponent<PlayableOpeningSequence3D>().Initialize(manager, settings);

            Debug.Log("[PlayableGameBootstrap] 可游玩流程已接入 ZFramework 启动场景。", this);
        }
    }
}
