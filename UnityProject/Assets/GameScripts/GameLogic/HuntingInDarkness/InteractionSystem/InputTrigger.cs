using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace InteractionSystem.Runtime
{
    [System.Serializable]
    public enum InputType
    {
        Pressed,     // 按下瞬间
        Released,    // 松开瞬间
        Held,        // 按住
        Tapped,      // 点击 (快速按下松开)
        DoubleTap,   // 双击
        HoldTime,    // 长按
        Axis         // 轴向输入
    }
    [System.Serializable]
    public enum AxisCompare
    {
        Greater,     // 大于阈值
        Less,        // 小于负阈值
        Absolute     // 绝对值大于阈值
    }

    [Serializable]
    [HideReferenceObjectPicker][InlineProperty]
    public class ReferencedInputTrigger
    {
    
        [SerializeField]
        [FoldoutGroup("Input Triggers"), GUIColor(1f, 0.95f, 0.85f)][InlineProperty,LabelText("Pressed")]
        private InputType press = InputType.Pressed;
        [SerializeField]
        [FoldoutGroup("Input Triggers"), GUIColor(0.85f, 1f, 0.85f)][InlineProperty,LabelText("Held")]
        private InputType held = InputType.Held;
        [SerializeField]
        [FoldoutGroup("Input Triggers"), GUIColor(0.85f, 0.95f, 1f)][InlineProperty,LabelText("Released")]
        private InputType release = InputType.Released;
    
        [Range(0, 1)] public float axisThreshold = 0.5f;
        private AxisCompare axisCompare = AxisCompare.Greater;
    
        public bool IsTriggered(KeyCode key, InteractableThreeDBehaviour.InputSetting.TriggerType triggerType)
        {
            InputType type = press;
            if (triggerType == InteractableThreeDBehaviour.InputSetting.TriggerType.Pressed)
                type = press;
            if(triggerType == InteractableThreeDBehaviour.InputSetting.TriggerType.Held)
                type = held; 
            if(triggerType == InteractableThreeDBehaviour.InputSetting.TriggerType.Released)
                type = release;
            switch (type)
            {
                case InputType.Pressed:
                    return Input.GetKeyDown(key);
                case InputType.Released:
                    return Input.GetKeyUp(key);
                case InputType.Held:
                    return Input.GetKey(key);
                case InputType.Axis:
                    float value = Input.GetKey(key) ? 1f : 0f;
                    return axisCompare == AxisCompare.Less
                        ? value < -axisThreshold
                        : Mathf.Abs(value) > axisThreshold;
                case InputType.DoubleTap:
                    return triggerType switch
                    {
                        InteractableThreeDBehaviour.InputSetting.TriggerType.Held => Input.GetKey(key),
                        InteractableThreeDBehaviour.InputSetting.TriggerType.Pressed => Input.GetKeyDown(key),
                        InteractableThreeDBehaviour.InputSetting.TriggerType.Released => Input.GetKeyUp(key),
                        _ => Input.GetKeyDown(key)
                    };
                case InputType.HoldTime:
                    return triggerType switch
                    {
                        InteractableThreeDBehaviour.InputSetting.TriggerType.Held => Input.GetKey(key),
                        InteractableThreeDBehaviour.InputSetting.TriggerType.Pressed => Input.GetKeyDown(key),
                        InteractableThreeDBehaviour.InputSetting.TriggerType.Released => Input.GetKeyUp(key),
                        _ => Input.GetKeyDown(key)
                    };
       
                default:
                    return false;
            }
        }
    }

// 在Inspector中配置：
// 1. 将Input Action拖拽到actionReference字段
// 2. 选择triggerType
// 3. 调用IsTriggered()检查
}
