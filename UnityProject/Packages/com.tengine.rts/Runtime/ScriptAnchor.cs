using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace TEngine.RTS
{
    [Serializable]
    public sealed class RtsParameterValue
    {
        [SerializeField] private string key = string.Empty;
        [SerializeField] private RtsParameterType type;
        [SerializeField] private string value = string.Empty;
        public string Key => key;
        public RtsParameterType Type => type;
        public string Value => value;
    }

    [DisallowMultipleComponent]
    public sealed class ScriptAnchor : MonoBehaviour
    {
        [SerializeField] private string scriptId = string.Empty;
        [SerializeField, TextArea] private string initialConfig = string.Empty;
        [SerializeField] private List<RtsParameterValue> parameters = new List<RtsParameterValue>();
        private IScriptRuntimeModule _runtime;

        public string ScriptId => scriptId;
        public string InitialConfig
        {
            get
            {
                if (parameters == null || parameters.Count == 0) return initialConfig;
                var result = new StringBuilder();
                foreach (RtsParameterValue parameter in parameters)
                {
                    if (parameter == null || string.IsNullOrWhiteSpace(parameter.Key)) continue;
                    if (result.Length > 0) result.Append(';');
                    result.Append(parameter.Key.Trim()).Append('=').Append(Uri.EscapeDataString(parameter.Value ?? string.Empty));
                }
                return result.ToString();
            }
        }

        private void OnEnable()
        {
            _runtime = ModuleSystem.GetModule<IScriptRuntimeModule>();
            _runtime.Attach(this);
        }

        private void OnDisable()
        {
            _runtime?.Detach(this);
            _runtime = null;
        }
    }
}
