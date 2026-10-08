using System;
using TMPro;
using UI;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>持久化屏幕表现资产注册表；只负责引用，不拥有事件/结算流程。</summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class TabletopPresentationAssets : MonoBehaviour
    {
        [SerializeField] private TabletopEventPanel3D eventPanelPrefab;
        [SerializeField] private CampLedgerPanel3D ledgerPanelPrefab;
        [SerializeField] private GameObject settlementLayoutPrefab;
        [SerializeField] private TMP_FontAsset worldFont;

        private static TabletopPresentationAssets instance;

        public static TabletopEventPanel3D EventPanelPrefab => Require().eventPanelPrefab;
        public static CampLedgerPanel3D LedgerPanelPrefab => Require().ledgerPanelPrefab;
        public static GameObject SettlementLayoutPrefab => Require().settlementLayoutPrefab;
        public static TMP_FontAsset WorldFont => Require().worldFont;
        internal static Transform InstanceRoot => Require().transform;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        private void Awake()
        {
            ValidateReferences();
        }

        private void OnEnable()
        {
            if (instance != null && instance != this)
                throw new InvalidOperationException($"[{nameof(TabletopPresentationAssets)}] 场景中存在多个注册宿主。\n当前对象：{name}");
            instance = this;
        }

        private void OnDisable()
        {
            if (instance == this) instance = null;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void ValidateReferences()
        {
            if (eventPanelPrefab == null) throw new MissingReferenceException($"[{nameof(TabletopPresentationAssets)}] eventPanelPrefab 未绑定。\n宿主：{name}");
            if (ledgerPanelPrefab == null) throw new MissingReferenceException($"[{nameof(TabletopPresentationAssets)}] ledgerPanelPrefab 未绑定。\n宿主：{name}");
            if (settlementLayoutPrefab == null) throw new MissingReferenceException($"[{nameof(TabletopPresentationAssets)}] settlementLayoutPrefab 未绑定。\n宿主：{name}");
            if (worldFont == null) throw new MissingReferenceException($"[{nameof(TabletopPresentationAssets)}] worldFont 未绑定。\n宿主：{name}");
        }

        private static TabletopPresentationAssets Require()
        {
            if (instance == null) throw new InvalidOperationException($"[{nameof(TabletopPresentationAssets)}] 尚未注册持久化屏幕表现资产宿主。");
            return instance;
        }
    }
}
