using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace TEngine.RTS.Editor
{
    internal sealed class RtsScriptDescriptor
    {
        internal string Id, File;
        internal readonly List<RtsParameterDescriptor> Parameters = new List<RtsParameterDescriptor>();
    }

    internal sealed class RtsParameterDescriptor
    {
        internal string Key, DefaultValue;
        internal RtsParameterType Type;
        internal double Min = double.NaN, Max = double.NaN;
    }

    internal static class RtsScriptCatalog
    {
        private static readonly Regex IdPattern = new Regex(@"ScriptId\s*\(\s*\""([^\""\r\n]+)\""", RegexOptions.Compiled);
        private static readonly Regex ParameterPattern = new Regex(@"RtsParameter\s*\(\s*\""([^\""\r\n]+)\""\s*,\s*(?:global::TEngine\.RTS\.)?RtsParameterType\.(\w+)\s*,\s*\""([^\""\r\n]*)\""([^\)]*)\)", RegexOptions.Compiled);
        private static readonly Regex MinPattern = new Regex(@"Min\s*=\s*(-?[\d\.]+)", RegexOptions.Compiled);
        private static readonly Regex MaxPattern = new Regex(@"Max\s*=\s*(-?[\d\.]+)", RegexOptions.Compiled);

        internal static List<RtsScriptDescriptor> Read(out List<string> errors)
        {
            errors = new List<string>();
            var result = new List<RtsScriptDescriptor>();
            foreach (string root in RtsProjectSettings.instance.ResolveSourceRoots())
            {
                if (!Directory.Exists(root)) continue;
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(file);
                    foreach (Match id in IdPattern.Matches(text))
                    {
                        var descriptor = new RtsScriptDescriptor { Id = id.Groups[1].Value, File = file };
                        foreach (Match parameter in ParameterPattern.Matches(text.Substring(0, id.Index)))
                        {
                            if (!Enum.TryParse(parameter.Groups[2].Value, out RtsParameterType type)) continue;
                            var item = new RtsParameterDescriptor { Key = parameter.Groups[1].Value, Type = type, DefaultValue = parameter.Groups[3].Value };
                            Match min = MinPattern.Match(parameter.Groups[4].Value); Match max = MaxPattern.Match(parameter.Groups[4].Value);
                            if (min.Success) double.TryParse(min.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out item.Min);
                            if (max.Success) double.TryParse(max.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out item.Max);
                            descriptor.Parameters.Add(item);
                        }
                        result.Add(descriptor);
                    }
                }
            }
            foreach (IGrouping<string, RtsScriptDescriptor> duplicate in result.GroupBy(x => x.Id).Where(x => x.Count() > 1))
                errors.Add("重复 ScriptId: " + duplicate.Key);
            return result.OrderBy(x => x.Id).ToList();
        }
    }

    internal sealed class RtsScriptDropdown : AdvancedDropdown
    {
        private readonly IReadOnlyList<RtsScriptDescriptor> _scripts;
        private readonly Action<string> _selected;
        internal RtsScriptDropdown(AdvancedDropdownState state, IReadOnlyList<RtsScriptDescriptor> scripts, Action<string> selected) : base(state)
        { _scripts = scripts; _selected = selected; minimumSize = new Vector2(360, 280); }
        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("RTS Scripts");
            foreach (RtsScriptDescriptor script in _scripts) root.AddChild(new AdvancedDropdownItem(script.Id));
            return root;
        }
        protected override void ItemSelected(AdvancedDropdownItem item) => _selected(item.name);
    }

    [CustomEditor(typeof(ScriptAnchor))]
    internal sealed class ScriptAnchorEditor : UnityEditor.Editor
    {
        private AdvancedDropdownState _dropdownState;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty id = serializedObject.FindProperty("scriptId");
            List<RtsScriptDescriptor> scripts = RtsScriptCatalog.Read(out List<string> errors);
            RtsScriptDescriptor selected = scripts.FirstOrDefault(x => x.Id == id.stringValue);
            Rect rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent("Script Id"));
            if (EditorGUI.DropdownButton(rect, new GUIContent(string.IsNullOrEmpty(id.stringValue) ? "选择脚本…" : id.stringValue), FocusType.Keyboard))
            {
                _dropdownState ??= new AdvancedDropdownState();
                new RtsScriptDropdown(_dropdownState, scripts, value => { id.stringValue = value; serializedObject.ApplyModifiedProperties(); SyncSchema(); }).Show(rect);
            }
            if (errors.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
            else if (!string.IsNullOrEmpty(id.stringValue) && selected == null) EditorGUILayout.HelpBox("ScriptId 不存在；旧健康代会继续运行。", MessageType.Error);
            if (selected != null && GUILayout.Button("同步参数 Schema")) SyncSchema();
            SerializedProperty values = serializedObject.FindProperty("parameters");
            if (selected != null && values.arraySize == selected.Parameters.Count)
            {
                EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
                for (int i = 0; i < selected.Parameters.Count; i++) DrawParameter(values.GetArrayElementAtIndex(i), selected.Parameters[i]);
            }
            else EditorGUILayout.PropertyField(values, true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("initialConfig"), new GUIContent("Legacy Config"), true);
            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawParameter(SerializedProperty element, RtsParameterDescriptor schema)
        {
            SerializedProperty raw = element.FindPropertyRelative("value");
            switch (schema.Type)
            {
                case RtsParameterType.Integer:
                    int.TryParse(raw.stringValue, out int integer);
                    int nextInt = EditorGUILayout.IntField(schema.Key, integer);
                    if (!double.IsNaN(schema.Min)) nextInt = Math.Max(nextInt, (int)schema.Min);
                    if (!double.IsNaN(schema.Max)) nextInt = Math.Min(nextInt, (int)schema.Max);
                    raw.stringValue = nextInt.ToString(CultureInfo.InvariantCulture); break;
                case RtsParameterType.Float:
                    float.TryParse(raw.stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float number);
                    float nextFloat = EditorGUILayout.FloatField(schema.Key, number);
                    if (!double.IsNaN(schema.Min)) nextFloat = Mathf.Max(nextFloat, (float)schema.Min);
                    if (!double.IsNaN(schema.Max)) nextFloat = Mathf.Min(nextFloat, (float)schema.Max);
                    raw.stringValue = nextFloat.ToString(CultureInfo.InvariantCulture); break;
                case RtsParameterType.Boolean:
                    bool.TryParse(raw.stringValue, out bool flag); raw.stringValue = EditorGUILayout.Toggle(schema.Key, flag).ToString(); break;
                default: raw.stringValue = EditorGUILayout.TextField(schema.Key, raw.stringValue); break;
            }
        }

        private void SyncSchema()
        {
            serializedObject.Update();
            string id = serializedObject.FindProperty("scriptId").stringValue;
            RtsScriptDescriptor script = RtsScriptCatalog.Read(out _).FirstOrDefault(x => x.Id == id);
            if (script == null) return;
            SerializedProperty list = serializedObject.FindProperty("parameters");
            var old = new Dictionary<string, string>();
            for (int i = 0; i < list.arraySize; i++) old[list.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue] = list.GetArrayElementAtIndex(i).FindPropertyRelative("value").stringValue;
            list.arraySize = script.Parameters.Count;
            for (int i = 0; i < script.Parameters.Count; i++)
            {
                RtsParameterDescriptor schema = script.Parameters[i]; SerializedProperty value = list.GetArrayElementAtIndex(i);
                value.FindPropertyRelative("key").stringValue = schema.Key;
                value.FindPropertyRelative("type").enumValueIndex = (int)schema.Type;
                value.FindPropertyRelative("value").stringValue = old.TryGetValue(schema.Key, out string retained) ? retained : schema.DefaultValue;
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

    internal sealed class RtsGameplayWizard : EditorWindow
    {
        private string _className = "NewGameplay";
        private string _scriptId = "game.new-gameplay";
        internal static void Open() => GetWindow<RtsGameplayWizard>(true, "Create RTS Gameplay").Show();
        private void OnGUI()
        {
            _className = EditorGUILayout.TextField("Class", _className);
            _scriptId = EditorGUILayout.TextField("Script Id", _scriptId);
            EditorGUILayout.HelpBox("生成共享 Data、RTS/Production 双端 Adaptor 与 Unity View。不会生成 Bootstrap；已有启动流程必须显式初始化正式 Adaptor。", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Regex.IsMatch(_className, @"^[A-Za-z_]\w*$") || string.IsNullOrWhiteSpace(_scriptId)))
                if (GUILayout.Button("Create")) CreateFiles();
        }
        private void CreateFiles()
        {
            string sourceRoot = RtsProjectSettings.instance.ResolveSourceRoots().First();
            string root = Path.Combine(sourceRoot, _className);
            if (Directory.Exists(root) && Directory.GetFiles(root).Length > 0 &&
                !EditorUtility.DisplayDialog("RTS", "目标目录已有文件，覆盖同名骨架？", "覆盖", "取消")) return;
            Directory.CreateDirectory(root);

            string dataPath = Path.Combine(root, _className + "Data.cs");
            string rtsPath = Path.Combine(root, _className + "RtsAdaptor.cs");
            string productionAdaptorPath = Path.Combine(root, _className + "ProductionAdaptor.cs");
            string viewPath = Path.Combine(root, _className + "View.cs");
            File.WriteAllText(dataPath, $@"using System;

namespace Project.Gameplay.{_className}
{{
    // Authoritative rules and state. This file has no Unity, TEngine, or RTS dependency.
    public sealed class {_className}Data
    {{
        public float ElapsedSeconds {{ get; private set; }}
        public void Tick(float deltaTime) => ElapsedSeconds += Math.Max(0f, deltaTime);
        public string CaptureState() => ElapsedSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public void RestoreState(string value)
        {{
            if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float elapsed)) ElapsedSeconds = elapsed;
        }}
    }}
}}
");
            File.WriteAllText(rtsPath, $@"#if TENGINE_RTS
using Project.Gameplay.{_className};
using TEngine.RTS;

[ScriptId(""{_scriptId}"")]
public sealed class {_className}RtsAdaptor : IScript
{{
    private readonly {_className}Data _data = new {_className}Data();
    public void Bind(IScriptContext context, IWorldObject owner, string initialConfig) {{ }}
    public void RestoreState(ScriptState state) => _data.RestoreState(state.Payload);
    public void Start() {{ }}
    public void Tick(in ScriptTime time) => _data.Tick(time.DeltaTime);
    public ScriptState CaptureState() => new ScriptState(1, _data.CaptureState());
    public void Dispose() {{ }}
}}
#endif
");
            File.WriteAllText(productionAdaptorPath, $@"#if UNITY_5_3_OR_NEWER
using Project.Gameplay.{_className};
using UnityEngine;

// Production lifecycle bridge. Existing Procedure/Module/scene startup calls Initialize explicitly.
public sealed class {_className}ProductionAdaptor : MonoBehaviour
{{
    [SerializeField] private {_className}View view;
    private {_className}Data _data;

    public void Initialize({_className}Data data)
    {{
        _data = data;
        enabled = true;
    }}

    public void Shutdown()
    {{
        enabled = false;
        _data = null;
    }}

    private void Awake() => enabled = false;
    private void Update()
    {{
        if (_data == null) return;
        _data.Tick(Time.deltaTime);
        view?.Render(_data);
    }}
}}
#endif
");
            File.WriteAllText(viewPath, $@"#if UNITY_5_3_OR_NEWER
using System;
using Project.Gameplay.{_className};
using UnityEngine;

// Presentation and asset mapping only. Existing project startup calls Initialize explicitly.
public sealed class {_className}View : MonoBehaviour
{{
    [Serializable]
    private struct AssetBinding {{ public string stableAssetKey; public GameObject prefab; }}

    [SerializeField] private AssetBinding[] assets;
    public void Render({_className}Data data) {{ /* Presentation only; do not add gameplay rules here. */ }}

    public bool TryGetPrefab(string stableAssetKey, out GameObject prefab)
    {{
        foreach (AssetBinding binding in assets)
            if (string.Equals(binding.stableAssetKey, stableAssetKey, StringComparison.Ordinal))
            {{
                prefab = binding.prefab;
                return prefab != null;
            }}
        prefab = null;
        return false;
    }}
}}
#endif
");
            File.WriteAllText(Path.Combine(root, "INTEGRATION.md"), $"# {_className} integration\n\n- ScriptId: `{_scriptId}`\n- Data is the only rule owner.\n- Register stable asset keys on `{_className}View`.\n- Existing scene/procedure/module startup must create `{_className}Data` and call `{_className}ProductionAdaptor.Initialize`; no Bootstrap is generated.\n");
            UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(dataPath, 1);
        }
    }
}
