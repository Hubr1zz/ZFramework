using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hunt
{
    /// <summary>
    /// 资源采集弹窗（卡牌式翻牌效果）。
    /// 显示 DrawCount 张背面卡 → 玩家点击「翻牌」→ 揭示结果。
    /// </summary>
    public class ResourceHarvestPopup : MonoBehaviour
    {
        public System.Action OnClose;

        private ResourcePointInstance _point;
        private HunterInstance        _hunter;
        private HuntManager           _huntMgr;
        private RectTransform         _cardsContainer;
        private Text                  _resultText;
        private Button                _harvestBtn;
        private Button                _closeBtn;

        private bool _harvested = false;

        private void Awake() => BuildLayout();

        private void BuildLayout()
        {
            gameObject.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.98f);

            // 标题
            var tGo = new GameObject("Title"); tGo.transform.SetParent(transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0.85f); trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            tGo.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.2f, 1f);
            HuntUIManager.MakeText(tGo, "T", "资源点采集", 18, TextAnchor.MiddleCenter);

            // 卡牌区
            var cardsGo = new GameObject("Cards"); cardsGo.transform.SetParent(transform, false);
            _cardsContainer = cardsGo.AddComponent<RectTransform>();
            _cardsContainer.anchorMin = new Vector2(0.05f, 0.35f);
            _cardsContainer.anchorMax = new Vector2(0.95f, 0.84f);
            _cardsContainer.offsetMin = _cardsContainer.offsetMax = Vector2.zero;
            cardsGo.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.1f, 0.9f);
            var hlg = cardsGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8; hlg.padding = new RectOffset(12, 12, 12, 12);
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            // 结果文字
            var resGo = new GameObject("Result"); resGo.transform.SetParent(transform, false);
            var rrt = resGo.AddComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.05f, 0.22f); rrt.anchorMax = new Vector2(0.95f, 0.34f);
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            _resultText = resGo.AddComponent<Text>();
            _resultText.text      = "";
            _resultText.fontSize  = 13;
            _resultText.alignment = TextAnchor.MiddleCenter;
            _resultText.color     = new Color(0.7f, 1f, 0.7f);
            _resultText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 翻牌按钮
            var hGo = new GameObject("HarvestBtn"); hGo.transform.SetParent(transform, false);
            var hrt = hGo.AddComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.1f, 0.08f); hrt.anchorMax = new Vector2(0.5f, 0.21f);
            hrt.offsetMin = hrt.offsetMax = Vector2.zero;
            hGo.AddComponent<Image>().color = new Color(0.2f, 0.45f, 0.2f, 1f);
            _harvestBtn = hGo.AddComponent<Button>();
            _harvestBtn.onClick.AddListener(OnClickHarvest);
            HuntUIManager.MakeText(hGo, "T", "翻牌采集", 14, TextAnchor.MiddleCenter);

            // 关闭按钮
            var cGo = new GameObject("CloseBtn"); cGo.transform.SetParent(transform, false);
            var crt = cGo.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.55f, 0.08f); crt.anchorMax = new Vector2(0.9f, 0.21f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            cGo.AddComponent<Image>().color = new Color(0.3f, 0.18f, 0.15f, 1f);
            _closeBtn = cGo.AddComponent<Button>();
            _closeBtn.onClick.AddListener(() => OnClose?.Invoke());
            HuntUIManager.MakeText(cGo, "T", "离开", 14, TextAnchor.MiddleCenter);
        }

        public void Show(ResourcePointInstance point, HunterInstance hunter, HuntManager huntMgr)
        {
            _point    = point;
            _hunter   = hunter;
            _huntMgr  = huntMgr;
            _harvested = false;
            _resultText.text = "";
            _harvestBtn.interactable = !point.IsExhausted;

            BuildCards(point.DrawCount, false);
        }

        private void BuildCards(int count, bool revealed)
        {
            foreach (Transform t in _cardsContainer) Destroy(t.gameObject);

            for (int i = 0; i < count; i++)
            {
                var cardGo = new GameObject($"Card_{i}");
                cardGo.transform.SetParent(_cardsContainer, false);
                var le = cardGo.AddComponent<LayoutElement>();
                le.preferredWidth = 72; le.preferredHeight = 100;

                var img = cardGo.AddComponent<Image>();
                img.color = revealed
                    ? new Color(0.2f, 0.5f, 0.3f, 1f)
                    : new Color(0.15f, 0.2f, 0.28f, 1f);

                HuntUIManager.MakeText(cardGo, "T",
                    revealed ? $"{_point.ResourceName}" : "?",
                    12, TextAnchor.MiddleCenter);
            }
        }

        private void OnClickHarvest()
        {
            if (_harvested || _point == null || _point.IsExhausted) return;
            var obtained = _huntMgr.ExecuteHarvest(_point);
            _harvested = true;
            _harvestBtn.interactable = false;

            BuildCards(_point.DrawCount, true);

            if (obtained.Count > 0)
                _resultText.text = $"✓ 获得：{_point.ResourceName} ×{obtained.Count}";
            else
                _resultText.text = "空手而归（很不走运）";
        }
    }
}
