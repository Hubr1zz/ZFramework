using Core;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Testing;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.Combat;
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

        private void Start()
        {
            if (GameManager.Instance != null) return;

            var settings = Resources.Load<PlayableBootstrapSettings>(SettingsPath);
            if (settings == null)
            {
                Debug.LogError($"[PlayableGameBootstrap] 缺少 Resources/{SettingsPath}.asset，游戏无法启动。", this);
                return;
            }

            if (!settings.CanCreateGame)
            {
                Debug.LogError("[PlayableGameBootstrap] 启动配置需要战斗、狩猎地图和营地开局内容。", settings);
                return;
            }

            PlayableHuntContentRuntime.Configure(settings.HuntContent);
            PlayableHuntDestinationRuntime.Configure(settings.HuntDestinations, settings.HuntContent);
            PlayableSettlementContentRuntime.Configure(settings.SettlementContent);
            PlayableHunterCombatAdapter.Configure(settings.CombatEquipment);
            PlayableSurvivalEventRuntime.Configure(settings.SurvivalEvents);
            PlayablePermanentInjuryRuntime.Configure(settings.PermanentInjuries);
            PlayableSymptomRuntime.Configure(settings.Symptoms);
            PlayableGrowthMilestoneRuntime.Configure(settings.GrowthMilestones);
            PlayableWeaponMasteryRuntime.Configure(settings.WeaponMastery);
            BattleSetup defaultBattleSetup = settings.CreateBattleSetup();
            PlayableEncounterRuntime.Configure(settings.EncounterCatalog, settings.DefaultEncounterId, defaultBattleSetup);

            var managerObject = new GameObject("GameManager (Playable)");
            managerObject.SetActive(false);
            var manager = managerObject.AddComponent<GameManager>();
            manager.ConfigureForStandaloneTest(defaultBattleSetup, settings.InitialPhase, settings.CellSize);
            manager.ConfigureSettlementContent(settings.SettlementContent);
            manager.ConfigureWorkshopContent(settings.WorkshopContent);
            managerObject.SetActive(true);

            var mainCamera = Camera.main;
            if (mainCamera != null)
                mainCamera.gameObject.AddComponent<PlayablePhaseCameraRig>().Initialize(manager, settings.BossCameraPosition, settings.BossCameraEulerAngles, settings.KeyLightColor, settings.KeyLightIntensity);
            gameObject.AddComponent<PlayableBossVitalityView>().Initialize(manager);

            if (settings.HideFrameworkDebugger && TEngine.Debugger.Instance != null)
                TEngine.Debugger.Instance.ActiveWindow = false;

            if (settings.ShowStartMenu)
                gameObject.AddComponent<PlayableStartMenu>().Initialize(manager, settings.GameTitle, settings.TitleTagline);

            if (settings.ShowFlowGuide)
                gameObject.AddComponent<PlayableFlowGuide>().Initialize(manager, settings);

            if (settings.ShowSettlementHud)
            {
                var settlementHud = gameObject.AddComponent<PlayableSettlementHud>();
                settlementHud.Initialize(manager, settings.SettlementHudWidth);
                gameObject.AddComponent<PlayableHuntDestinationView>().Initialize(manager, settlementHud, settings.HuntDestinations);
                gameObject.AddComponent<PlayableSettlementEventView>().Initialize(manager);
                gameObject.AddComponent<PlayableRecruitmentView>().Initialize(manager, settings.SettlementContent);
                gameObject.AddComponent<PlayableSymptomGrowthView>().Initialize(manager, settings.Symptoms);
                gameObject.AddComponent<PlayableGrowthMilestoneToast>().Initialize(manager);
            }

            Debug.Log("[PlayableGameBootstrap] 可游玩流程已接入 ZFramework 启动场景。", this);
        }
    }
}
