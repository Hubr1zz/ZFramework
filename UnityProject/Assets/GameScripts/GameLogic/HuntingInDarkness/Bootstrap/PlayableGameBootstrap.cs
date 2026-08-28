using Core;
using GameplayBase;
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

            PlayableContentSourceBundle sourceBundle = PlayableContentSourceSystem.CurrentBundle;
            PlayableBootstrapSettings settings = sourceBundle?.Settings;
            if (settings == null || SceneManager.GetActiveScene().name != settings.EntrySceneName) return false;

            var installerObject = new GameObject("HuntingInDarkness Runtime Bootstrap");
            PlayableGameBootstrap bootstrap = installerObject.AddComponent<PlayableGameBootstrap>();
            if (!bootstrap.Initialize(sourceBundle, settings))
            {
                if (Application.isPlaying) Destroy(installerObject);
                else DestroyImmediate(installerObject);
                return false;
            }
            installed = true;
            return true;
        }

        /// <summary>安装可游玩流程必需的世界空间输入端口；不受旧屏幕 HUD 可见性控制。</summary>
        public static void EnsureRequiredWorldSpacePorts(GameObject host, GameManager manager, PlayableBootstrapSettings settings) => PlayableSettlementWorldSpacePortInstaller.EnsureInstalled(host, manager, settings);

        private bool Initialize(PlayableContentSourceBundle sourceBundle, PlayableBootstrapSettings settings)
        {
            if (sourceBundle == null || settings == null)
            {
                Debug.LogError("[PlayableGameBootstrap] 缺少已准备的 Hunting in Darkness 内容源 Manifest，游戏无法启动。", this);
                return false;
            }

            Core.LocalizationManager.ConfigureBundledFont(sourceBundle.ChineseFont);
            if (!PlayableCampaignContentAssembler.TryBuild(sourceBundle, out PlayableCampaignContentCandidate contentCandidate, out PlayableContentDiagnosticReport buildReport))
            {
                Debug.LogError($"[PlayableGameBootstrap] 内容装配失败：{buildReport}", settings);
                return false;
            }

            var managerObject = new GameObject("GameManager (Playable)");
            managerObject.SetActive(false);
            var manager = managerObject.AddComponent<GameManager>();
            if (!manager.ConfigureCampaign(new CampaignBootstrapRequest
                {
                    BattleSetup = contentCandidate.DefaultBattleSetup,
                    CellSize = contentCandidate.CellSize,
                    SettlementContent = contentCandidate.SettlementContent,
                    WorkshopContent = contentCandidate.WorkshopContent,
                    WaitForEntrySelection = settings.ShowStartMenu
                }))
            {
                Debug.LogError("[PlayableGameBootstrap] GameManager 战役配置被拒绝。", manager);
                Destroy(managerObject);
                return false;
            }
            if (!PlayableCampaignContentAssembler.Install(contentCandidate, out PlayableContentDiagnosticReport installReport))
            {
                Debug.LogError($"[PlayableGameBootstrap] 内容安装失败：{installReport}", settings);
                Destroy(managerObject);
                return false;
            }
            EnsureRequiredWorldSpacePorts(gameObject, manager, settings);
            managerObject.SetActive(true);

            var mainCamera = Camera.main;
            if (mainCamera != null)
                mainCamera.gameObject.AddComponent<PlayablePhaseCameraRig>().Initialize(manager, settings.BossCameraPosition, settings.BossCameraEulerAngles, settings.KeyLightColor, settings.KeyLightIntensity);
            gameObject.AddComponent<PlayableBossVitalityView>().Initialize(() => manager != null ? manager.CurrentGamePhase : GamePhase.Settlement, () => manager != null ? manager.ShowdownGameplay?.Boss : null);

            if (settings.HideFrameworkDebugger && ZFramework.Debugger.Instance != null)
                ZFramework.Debugger.Instance.ActiveWindow = false;

            if (settings.ShowStartMenu || settings.ShowFlowGuide && settings.ShowOpeningNarrative)
                gameObject.AddComponent<PlayableOpeningSequence3D>().Initialize(manager, settings);

            Debug.Log("[PlayableGameBootstrap] 可游玩流程已接入 ZFramework 启动场景。", this);
            return true;
        }

    }
}
