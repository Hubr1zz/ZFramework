using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    /// <summary>
    /// 营地桌面上固定的「出发」卡：点击触发出发确认。
    /// 视觉（卡面 + “出发”文字）在场景/Prefab 中搭好；本脚本只负责点击上报。
    /// 需有 Collider 用于点击命中。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DepartureCard : MonoBehaviour
    {
        /// <summary>被点击时触发（由 SquadZone 订阅）。</summary>
        public System.Action OnDepart;

        private void OnMouseUpAsButton()
        {
            // 不穿透 UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            OnDepart?.Invoke();
        }
    }
}
