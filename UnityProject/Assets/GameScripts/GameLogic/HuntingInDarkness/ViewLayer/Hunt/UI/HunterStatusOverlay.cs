using System.Collections.Generic;
using HuntingInDarkness.Data;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hunt
{
    /// <summary>猎人状态叠加面板（左下角，显示 HP、收集物、临时状态）</summary>
    public class HunterStatusOverlay : MonoBehaviour
    {
        private List<HunterInstance> _hunters = new();
        private RectTransform        _content;

        private void Awake() => BuildLayout();

        private void BuildLayout()
        {
            HuntUIManager.FullStretch(gameObject);
            var titleGo = new GameObject("Title"); titleGo.transform.SetParent(transform, false);
            var trt = titleGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0.88f); trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            HuntUIManager.MakeText(titleGo, "T", "小队状态", 12, TextAnchor.MiddleCenter)
                .color = new Color(0.8f, 0.85f, 0.6f);

            var cGo = new GameObject("Content"); cGo.transform.SetParent(transform, false);
            _content = cGo.AddComponent<RectTransform>();
            _content.anchorMin = new Vector2(0, 0); _content.anchorMax = new Vector2(1, 0.88f);
            _content.offsetMin = new Vector2(4, 4); _content.offsetMax = new Vector2(-4, -4);
            var vlg = cGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        }

        public void Init(List<HunterInstance> hunters)
        {
            _hunters = hunters ?? new List<HunterInstance>();
            Refresh();
        }

        public void Refresh()
        {
            if (_content == null) return;
            foreach (Transform t in _content) Destroy(t.gameObject);

            foreach (var h in _hunters)
            {
                var go = new GameObject($"H_{h.InstanceId}");
                go.transform.SetParent(_content, false);
                var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 28;
                go.AddComponent<Image>().color = h.IsAlive
                    ? new Color(0.15f, 0.2f, 0.18f, 0.9f)
                    : new Color(0.2f, 0.1f, 0.1f, 0.9f);

                int collectibles = h.Collectibles?.Count ?? 0;
                HuntUIManager.MakeText(go, "T",
                    $"{h.Name}  HP{h.HP.body}/{h.MaxHP.body}  采{collectibles}",
                    10, TextAnchor.MiddleLeft).GetComponent<RectTransform>()
                    .offsetMin = new Vector2(4, 0);
            }
        }
    }
}
