using Core;
using GameplayBase;
using HuntingInDarkness.Hunt;
using UI;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Camera
{
    /// <summary>
    /// 为可游玩组合根装配现有三阶段相机控制器，不复制各阶段的输入逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayablePhaseCameraRig : MonoBehaviour
    {
        private GameManager manager;
        private SettlementCameraController settlementController;
        private HuntCameraController huntController;
        private CameraController bossFightController;
        private GamePhase activePhase = (GamePhase)(-1);
        private Vector3 bossCameraPosition;
        private Vector3 bossCameraEulerAngles;

        public void Initialize(GameManager gameManager, Vector3 bossPosition, Vector3 bossEulerAngles, Color lightColor, float lightIntensity)
        {
            manager = gameManager;
            bossCameraPosition = bossPosition;
            bossCameraEulerAngles = bossEulerAngles;

            settlementController = GetComponent<SettlementCameraController>() ?? gameObject.AddComponent<SettlementCameraController>();
            huntController = GetComponent<HuntCameraController>() ?? gameObject.AddComponent<HuntCameraController>();
            bossFightController = GetComponent<CameraController>() ?? gameObject.AddComponent<CameraController>();
            var lightObject = new GameObject("Playable Key Light");
            lightObject.transform.SetParent(transform, false);
            var keyLight = lightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = lightColor;
            keyLight.intensity = lightIntensity;

            settlementController.enabled = false;
            huntController.enabled = false;
            bossFightController.enabled = false;
            ApplyPhase();
        }

        private void LateUpdate()
        {
            if (manager == null || activePhase == manager.CurrentGamePhase) return;
            ApplyPhase();
        }

        private void ApplyPhase()
        {
            if (manager == null) return;

            activePhase = manager.CurrentGamePhase;
            settlementController.enabled = activePhase == GamePhase.Settlement;
            huntController.enabled = activePhase == GamePhase.Hunt;

            if (activePhase == GamePhase.BossFight)
            {
                transform.SetPositionAndRotation(bossCameraPosition, Quaternion.Euler(bossCameraEulerAngles));
                bossFightController.enabled = true;
                return;
            }

            bossFightController.enabled = false;
        }
    }
}
