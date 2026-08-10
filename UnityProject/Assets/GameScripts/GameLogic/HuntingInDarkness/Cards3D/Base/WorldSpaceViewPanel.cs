using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>
    /// 可复用的"查看类"世界空间面板基类。平铺在桌面上（XZ 平面，文字朝 +Y），
    /// 与项目里所有 3D 卡牌/文字的朝向一致——因此面板上的卡牌可正常被俯视相机查看、拖拽。
    ///
    /// 提供：深色背板 + 标题 + ContentRoot（子类往里塞内容）+ 显示/隐藏/定位。
    /// 子类示例：StackPreviewPanel（列文字）、WorkshopCraftPanel（摆配方卡）。
    /// </summary>
    public abstract class WorldSpaceViewPanel : MonoBehaviour
    {
        protected Transform   ContentRoot { get; private set; }
        protected TextMeshPro Title       { get; private set; }

        Renderer _backdrop;

        // ─── 基础构建（子类在自己的 Build 里调用一次）────────────────────────

        protected void BuildBase()
        {
            // 背板：Quad 绕 X 轴转 90° 平铺，法线朝 +Y
            var bgGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgGo.name = "Backdrop";
            bgGo.transform.SetParent(transform, false);
            bgGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            bgGo.transform.localPosition = Vector3.zero;
            Destroy(bgGo.GetComponent<Collider>());
            _backdrop = bgGo.GetComponent<Renderer>();
            _backdrop.material = new Material(_backdrop.sharedMaterial)
            {
                color = new Color(0.05f, 0.05f, 0.08f, 0.95f)
            };

            // 标题（平铺文字）
            var tGo = new GameObject("Title");
            tGo.transform.SetParent(transform, false);
            tGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Title = tGo.AddComponent<TextMeshPro>();
            Title.fontSize  = 0.20f;
            Title.fontStyle = FontStyles.Bold;
            Title.alignment = TextAlignmentOptions.Center;
            Title.color     = new Color(0.95f, 0.88f, 0.62f);

            // 内容根
            var cGo = new GameObject("Content");
            cGo.transform.SetParent(transform, false);
            cGo.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            ContentRoot = cGo.transform;
        }

        /// <summary>设置背板尺寸（w 沿世界 X，h 沿世界 Z），并把标题摆到顶部。</summary>
        protected void SetSize(float w, float h)
        {
            if (_backdrop != null)
                _backdrop.transform.localScale = new Vector3(w, h, 1f);
            if (Title != null)
            {
                Title.transform.localPosition = new Vector3(0f, 0.02f, h * 0.5f - 0.18f);
                Title.rectTransform.sizeDelta = new Vector2(w - 0.1f, 0.3f);
            }
        }

        // ─── 显示 / 隐藏 ──────────────────────────────────────────────────────

        public void ShowAt(Vector3 worldPos)
        {
            transform.position = worldPos;
            transform.rotation = Quaternion.identity; // 平铺，不 billboard
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (this != null) gameObject.SetActive(false);
        }
    }
}
