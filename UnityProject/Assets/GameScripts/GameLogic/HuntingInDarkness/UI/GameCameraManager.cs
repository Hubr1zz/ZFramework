using GameplayBase;
using HuntingInDarkness.Hunt;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 相机阶段调度器。挂在 Main Camera 上（与三个控制器同节点）。
    /// 由 GameManager 在阶段切换时调用 SetPhase()，负责启用对应控制器并禁用其余两个。
    ///
    /// Inspector 拖入三个控制器引用即可；若某阶段暂无控制器则留空（安全跳过）。
    /// </summary>
    [AddComponentMenu("CardTactics/Game Camera Manager")]
    [DisallowMultipleComponent]
    public class GameCameraManager : MonoBehaviour
    {
        [Header("各阶段相机控制器（挂在同一相机上）")]
        [SerializeField] private SettlementCameraController settlementController;
        [SerializeField] private HuntCameraController       huntController;
        [SerializeField] private CameraController           bossFightController;

        private void Awake()
        {
            // 初始全部禁用，等 GameManager 调 SetPhase 后激活正确的一个
            EnableOnly(null);
        }

        /// <summary>切换到指定阶段的相机控制器</summary>
        public void SetPhase(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Settlement: EnableOnly(settlementController); break;
                case GamePhase.Hunt:       EnableOnly(huntController);       break;
                case GamePhase.BossFight:  EnableOnly(bossFightController);  break;
                default:                   EnableOnly(null);                 break;
            }
        }

        // ─── 工具 ───────────────────────────────────────────────────────

        private void EnableOnly(MonoBehaviour active)
        {
            SetEnabled(settlementController, active == settlementController);
            SetEnabled(huntController,       active == huntController);
            SetEnabled(bossFightController,  active == bossFightController);
        }

        private static void SetEnabled(MonoBehaviour comp, bool value)
        {
            if (comp != null) comp.enabled = value;
        }
    }
}
