using System.Collections.Generic;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// UI 代码生成器使用的组件绑定容器。
    /// 这是编辑器工具所需的最小运行时组件，不依赖示例 GameLogic。
    /// </summary>
    [DisallowMultipleComponent]
    public partial class UIBindComponent : MonoBehaviour
    {
        [SerializeField] private List<Component> m_components = new List<Component>();

        public T GetComponent<T>(int index) where T : Component
        {
            if (index < 0 || index >= m_components.Count)
            {
                Log.Error("索引超出范围");
                return null;
            }

            var component = m_components[index] as T;
            if (component == null)
            {
                Log.Error($"没有找到对应类型: {typeof(T).FullName}");
            }

            return component;
        }
    }
}
