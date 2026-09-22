#if !ODIN_INSPECTOR
using System;
using UnityEngine;

namespace Sirenix.OdinInspector
{
    // Inspector-only compatibility for migrated content. These attributes intentionally
    // carry no runtime behavior; Unity serialization remains the authoritative format.
    public abstract class OdinCompatibilityAttribute : Attribute { }

    public sealed class BoxGroupAttribute : OdinCompatibilityAttribute { public BoxGroupAttribute(string name) { } }
    public sealed class ButtonAttribute : OdinCompatibilityAttribute { public ButtonAttribute(string name = null) { } }
    public sealed class DictionaryDrawerSettingsAttribute : OdinCompatibilityAttribute { public bool IsReadOnly { get; set; } }
    public sealed class EnumToggleButtonsAttribute : OdinCompatibilityAttribute { }
    public sealed class FoldoutGroupAttribute : OdinCompatibilityAttribute { public FoldoutGroupAttribute(string name) { } }
    public sealed class GUIColorAttribute : OdinCompatibilityAttribute { public GUIColorAttribute(float r, float g, float b) { } }
    public sealed class HideReferenceObjectPickerAttribute : OdinCompatibilityAttribute { }
    public sealed class InfoBoxAttribute : OdinCompatibilityAttribute { public InfoBoxAttribute(string message) { } }
    public sealed class InlineEditorAttribute : OdinCompatibilityAttribute { }
    public sealed class InlinePropertyAttribute : OdinCompatibilityAttribute { }
    public sealed class LabelTextAttribute : OdinCompatibilityAttribute { public LabelTextAttribute(string text) { } }
    public sealed class LabelWidthAttribute : OdinCompatibilityAttribute { public LabelWidthAttribute(float width) { } }
    public sealed class ListDrawerSettingsAttribute : OdinCompatibilityAttribute
    {
        public string OnBeginListElementGUI { get; set; }
        public bool ShowItemCount { get; set; }
        public bool DraggableItems { get; set; }
    }
    public sealed class MinValueAttribute : OdinCompatibilityAttribute { public MinValueAttribute(double value) { } }
    public sealed class OnInspectorGUIAttribute : OdinCompatibilityAttribute { }
    public sealed class OnValueChangedAttribute : OdinCompatibilityAttribute { public OnValueChangedAttribute(string method) { } }
    public sealed class PropertyOrderAttribute : OdinCompatibilityAttribute { public PropertyOrderAttribute(int order) { } }
    public sealed class ReadOnlyAttribute : OdinCompatibilityAttribute { }
    public sealed class RequiredAttribute : OdinCompatibilityAttribute { }
    public sealed class ShowIfAttribute : OdinCompatibilityAttribute { public ShowIfAttribute(string condition) { } }
    public sealed class ShowInInspectorAttribute : OdinCompatibilityAttribute { }
    public sealed class TableListAttribute : OdinCompatibilityAttribute { }
    public sealed class ValidateInputAttribute : OdinCompatibilityAttribute
    {
        public ValidateInputAttribute(string condition, string message, InfoMessageType type) { }
    }

    public enum InfoMessageType { None, Info, Warning, Error }
    public abstract class SerializedMonoBehaviour : MonoBehaviour { }
    public abstract class SerializedScriptableObject : ScriptableObject { }
}

namespace Sirenix.Serialization
{
    public sealed class OdinSerializeAttribute : Attribute { }
}
#endif
