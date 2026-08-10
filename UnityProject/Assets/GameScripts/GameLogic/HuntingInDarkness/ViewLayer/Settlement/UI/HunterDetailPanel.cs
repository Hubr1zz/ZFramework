using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Settlement
{
    /// <summary>
    /// 猎人详情面板：查看属性、管理装备。
    ///
    /// 面板骨架（背景 / 头部 / 属性文本 / 装备滚动容器 / 关闭按钮）在 Prefab 中预先搭好，
    /// 引用通过 Inspector 注入；装备行按数据实例化 <see cref="HunterEquipmentSlotView"/> 模板。
    /// 由 SettlementUIManager 持有 Prefab 引用并 Instantiate。
    /// </summary>
    public class HunterDetailPanel : MonoBehaviour
    {
        public System.Action OnClose;

        // ─── 引用（Inspector 注入）────────────────────────────────
        [Header("骨架引用")]
        [SerializeField] private TextMeshProUGUI _mainText;      // 左侧属性文本
        [SerializeField] private RectTransform _equipContent;  // 装备栏 ScrollView 的 Content
        [SerializeField] private Button _closeButton;   // 头部关闭按钮

        [Header("列表项模板")]
        [SerializeField] private HunterEquipmentSlotView _equipSlotPrefab; // 装备槽行模板
        [SerializeField] private int _equipmentSlotCount = 10;              // 装备格数量上限

        private HunterInstance    _hunter;
        private SettlementManager _mgr;

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(() => OnClose?.Invoke());
        }

        public void Show(HunterInstance hunter, SettlementManager mgr)
        {
            _hunter = hunter;
            _mgr    = mgr;
            Refresh();
        }

        private void Refresh()
        {
            if (_hunter == null) return;

            var h = _hunter;
            _mainText.text =
                $"【{h.Name}】  年龄 {h.Age}\n\n" +
                $"意志点：{h.Willpower}/{h.WillpowerMax}   命运值：{h.Luck}\n" +
                $"压抑值：{h.Insanity}\n\n" +
                $"力量 {h.Stats.strength}  精准 {h.Stats.accuracy}  敏捷 {h.Stats.evasion}\n" +
                $"移动 {h.Stats.movement}  速度 {h.Stats.speed}\n\n" +
                $"血量：\n" +
                $"  头部 {h.HP.head}/{h.MaxHP.head}  躯干 {h.HP.body}/{h.MaxHP.body}\n" +
                $"  手臂 {h.HP.arms}/{h.MaxHP.arms}  腿部 {h.HP.legs}/{h.MaxHP.legs}\n\n" +
                $"胆识 {h.Courage}  阅历 {h.Understanding}  熟练 {h.WeaponProficiency}\n\n" +
                $"特性：{(h.Traits.Count == 0 ? "无" : string.Join(", ", h.Traits))}\n" +
                $"症状：{(h.Ailments.Count == 0 ? "无" : string.Join(", ", h.Ailments))}\n\n" +
                $"死亡牌堆：存活 {h.SurvivalCards}  死亡 {h.DeathCards}";

            RefreshEquipment();
        }

        private void RefreshEquipment()
        {
            foreach (Transform t in _equipContent) Destroy(t.gameObject);

            for (int i = 0; i < _equipmentSlotCount; i++)
            {
                var slot = Instantiate(_equipSlotPrefab, _equipContent);
                slot.Bind(i, _hunter, _mgr, RefreshEquipment);
            }
        }
    }
}
