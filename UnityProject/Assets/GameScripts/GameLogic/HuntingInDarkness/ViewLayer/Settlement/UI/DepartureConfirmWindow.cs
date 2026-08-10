using System.Collections.Generic;
using System.Text;
using HuntingInDarkness.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Settlement
{
    /// <summary>
    /// 出发确认窗（2D）。展示小队成员（及后续物资携带信息），询问是否确认出发。
    /// 骨架在 Prefab 中搭好并 [SerializeField] 连线；由 SettlementUIManager 持有引用并调用 Show。
    /// 注：后续会接入统一 UIWindow 框架，此处先独立实现。
    /// </summary>
    public class DepartureConfirmWindow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _bodyText;      // 小队 / 物资信息
        [SerializeField] private Button          _confirmButton;
        [SerializeField] private Button          _cancelButton;

        private System.Action _onConfirm;
        private System.Action _onCancel;

        private void Awake()
        {
            if (_confirmButton != null) _confirmButton.onClick.AddListener(() => _onConfirm?.Invoke());
            if (_cancelButton != null)  _cancelButton.onClick.AddListener(() => _onCancel?.Invoke());
        }

        public void Show(List<HunterInstance> squad, System.Action onConfirm, System.Action onCancel)
        {
            _onConfirm = onConfirm;
            _onCancel  = onCancel;

            if (_bodyText != null) _bodyText.text = BuildInfo(squad);
            if (_confirmButton != null) _confirmButton.interactable = squad != null && squad.Count > 0;
        }

        private static string BuildInfo(List<HunterInstance> squad)
        {
            var sb = new StringBuilder();
            sb.AppendLine("出发小队：");
            if (squad == null || squad.Count == 0)
            {
                sb.AppendLine("  （未选择任何猎人）");
            }
            else
            {
                foreach (var h in squad)
                    sb.AppendLine($"  · {h.Name}  (意志 {h.Willpower}/{h.WillpowerMax})");
            }
            // TODO: 小队携带物资信息（待物资携带数据模型确定后补充）
            return sb.ToString();
        }
    }
}
