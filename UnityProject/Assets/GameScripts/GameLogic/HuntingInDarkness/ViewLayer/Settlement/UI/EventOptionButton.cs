using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Settlement
{
    /// <summary>
    /// 事件弹窗的「选项按钮」模板：Prefab 预先搭好（按钮 + 文字），
    /// 由 <see cref="EventPopup"/> 实例化后调用 <see cref="Bind"/> 注入文案与回调。
    /// </summary>
    public class EventOptionButton : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Button          _button;

        public void Bind(string text, System.Action onClick)
        {
            if (_label != null) _label.text = text;
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => onClick?.Invoke());
            }
        }
    }
}
