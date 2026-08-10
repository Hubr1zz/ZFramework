using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Settlement
{
    /// <summary>
    /// 事件弹窗 UI（叙事/抉择/战斗事件展示）。
    /// 叙事事件：展示文本 → 确认按钮 → 结算效果。
    /// 抉择事件：展示文本 → 选项按钮 → 骰子判定 → 结果文本 → 确认。
    /// 战斗事件：展示文本 → 进入Boss战按钮。
    ///
    /// 骨架在 Prefab 中预先搭好，引用通过 Inspector 连线；选项按钮按数据实例化
    /// <see cref="EventOptionButton"/> 模板并 Bind。由 SettlementUIManager 持有引用并调用 Show。
    /// </summary>
    public class EventPopup : MonoBehaviour
    {
        public System.Action OnResolved;

        [Header("骨架引用")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _bodyText;
        [SerializeField] private TextMeshProUGUI _resultText;
        [SerializeField] private RectTransform   _optionsContainer; // 选项按钮容器（VerticalLayoutGroup）
        [SerializeField] private Button          _rerollButton;
        [SerializeField] private Button          _confirmButton;
        [SerializeField] private TextMeshProUGUI _confirmLabel;     // 确认按钮上的文字（确认/接受结果/继续）

        [Header("列表项模板")]
        [SerializeField] private EventOptionButton _optionTemplate;

        private EventData      _currentEvent;
        private HunterInstance _currentHunter;
        private EventSystem    _eventSystem;

        // 临时存储判定数据（用于重投）
        private int _lastRoll;
        private int _lastOptionIndex;
        private int _lastCheckDice  = 1;
        private int _lastCheckSides = 10;

        private void Awake()
        {
            if (_rerollButton != null)  _rerollButton.onClick.AddListener(OnClickReroll);
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnClickConfirm);
        }

        // ─── 显示 ─────────────────────────────────────────────────

        public void Show(EventData evt, HunterInstance hunter, EventSystem eventSystem)
        {
            _currentEvent  = evt;
            _currentHunter = hunter;
            _eventSystem   = eventSystem;

            _titleText.text = $"【{evt.eventName}】";
            _bodyText.text  = evt.displayText;
            _resultText.gameObject.SetActive(false);
            _rerollButton.gameObject.SetActive(false);

            ClearOptions();

            if (evt.eventType == GameEventType.Choice && evt.options.Count > 0)
            {
                BuildOptions(evt);
                _confirmButton.gameObject.SetActive(false);
            }
            else
            {
                _confirmButton.gameObject.SetActive(true);
            }
        }

        private void ClearOptions()
        {
            foreach (Transform t in _optionsContainer) Destroy(t.gameObject);
        }

        private void BuildOptions(EventData evt)
        {
            for (int i = 0; i < evt.options.Count; i++)
            {
                var idx = i;
                var btn = Instantiate(_optionTemplate, _optionsContainer);
                btn.Bind(evt.options[i].optionText, () => OnClickOption(idx));
            }
        }

        // ─── 按钮回调 ─────────────────────────────────────────────

        private void OnClickOption(int index)
        {
            if (_currentEvent == null || _eventSystem == null) return;

            _lastOptionIndex = index;
            var opt = _currentEvent.options[index];

            if (opt.checkType != CheckType.None)
            {
                // 先投骰，显示结果后等玩家确认（或重投）
                _lastRoll = _eventSystem.RollDice(_lastCheckDice, _lastCheckSides);
                int bonus  = GetActorBonus(opt.checkType);
                int total  = _lastRoll + bonus;
                bool pass  = total >= opt.checkTarget;

                _resultText.text = $"骰值：{_lastRoll}（+{bonus} {opt.checkType}）= {total}  " +
                                   $"目标：{opt.checkTarget}  " +
                                   $"→ {(pass ? "✓ 成功" : "✗ 失败")}";
                _resultText.color = pass ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f);
                _resultText.gameObject.SetActive(true);

                // 显示重投按钮（若猎人有意志点）
                bool canReroll = _currentHunter != null && _currentHunter.Willpower > 0;
                _rerollButton.gameObject.SetActive(canReroll);

                // 选项区替换为「接受结果」按钮
                ClearOptions();
                _confirmButton.gameObject.SetActive(true);
                SetConfirmLabel("接受结果");
            }
            else
            {
                // 无判定直接结算
                Resolve(index);
            }
        }

        private void OnClickReroll()
        {
            if (_currentHunter == null || _eventSystem == null) return;
            var result = _eventSystem.TryReroll(_currentHunter, _lastRoll,
                _lastCheckDice, _lastCheckSides);
            if (!result.Success)
            {
                _resultText.text += "\n（意志点不足，无法重投）";
                return;
            }

            _lastRoll = result.FinalRoll;
            var opt  = _currentEvent.options[_lastOptionIndex];
            int bonus = GetActorBonus(opt.checkType);
            bool pass = (_lastRoll + bonus) >= opt.checkTarget;
            _resultText.text = $"重投后：{_lastRoll}（+{bonus}）= {_lastRoll + bonus}  " +
                               $"→ {(pass ? "✓ 成功" : "✗ 失败")}";
            _resultText.color = pass ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f);
            _rerollButton.gameObject.SetActive(false); // 只能重投一次
        }

        private void OnClickConfirm()
        {
            if (_currentEvent == null) return;

            if (_currentEvent.eventType == GameEventType.Narrative)
            {
                _eventSystem?.ResolveNarrative(_currentEvent);
            }
            else if (_currentEvent.eventType == GameEventType.Choice && _lastOptionIndex >= 0)
            {
                Resolve(_lastOptionIndex);
            }

            OnResolved?.Invoke();
        }

        private void Resolve(int optionIndex)
        {
            var result = _eventSystem?.ResolveChoice(_currentEvent, optionIndex, _currentHunter);
            if (result.HasValue)
            {
                _resultText.text = result.Value.ResultText;
                _resultText.gameObject.SetActive(true);
                ClearOptions();
                _confirmButton.gameObject.SetActive(true);
                SetConfirmLabel("继续");
            }
        }

        private int GetActorBonus(CheckType checkType)
        {
            if (_currentHunter == null) return 0;
            return checkType switch
            {
                CheckType.Courage       => _currentHunter.Courage,
                CheckType.Luck          => _currentHunter.Luck,
                CheckType.Strength      => _currentHunter.Stats.strength,
                CheckType.Evasion       => _currentHunter.Stats.evasion,
                CheckType.Understanding => _currentHunter.Understanding,
                _ => 0
            };
        }

        private void SetConfirmLabel(string text)
        {
            if (_confirmLabel != null) _confirmLabel.text = text;
        }
    }
}
