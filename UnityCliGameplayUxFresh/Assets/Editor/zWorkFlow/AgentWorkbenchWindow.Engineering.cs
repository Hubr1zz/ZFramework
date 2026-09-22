#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AgentWorkflow.Editor
{
    public sealed partial class AgentWorkbenchWindow
    {
        private void ReloadEngineeringCatalog()
        {
            _engineeringCatalogError = string.Empty;
            _engineeringCatalog = new EngineeringCapabilityCatalog
            {
                layers = Array.Empty<EngineeringLayerDefinition>(),
                entries = Array.Empty<EngineeringCapabilityEntry>()
            };
            if (string.IsNullOrWhiteSpace(_engineeringCatalogPath) || !File.Exists(_engineeringCatalogPath))
            {
                _selectedEngineeringCapability = null;
                return;
            }

            try
            {
                var catalog = JsonUtility.FromJson<EngineeringCapabilityCatalog>(
                    File.ReadAllText(_engineeringCatalogPath, Encoding.UTF8));
                if (catalog == null || catalog.schemaVersion < 1)
                    throw new InvalidDataException("schemaVersion must be at least 1");

                catalog.layers ??= Array.Empty<EngineeringLayerDefinition>();
                catalog.layers = catalog.layers
                    .Where(layer => layer != null && !string.IsNullOrWhiteSpace(layer.id))
                    .GroupBy(layer => layer.id.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToArray();
                catalog.entries ??= Array.Empty<EngineeringCapabilityEntry>();
                foreach (var entry in catalog.entries.Where(entry => entry != null))
                {
                    entry.kind = NormalizeEngineeringKind(entry.kind);
                    entry.capabilities ??= Array.Empty<string>();
                    entry.capabilitiesEn ??= Array.Empty<string>();
                    entry.constraints ??= Array.Empty<string>();
                    entry.constraintsEn ??= Array.Empty<string>();
                    entry.evidence ??= Array.Empty<string>();
                    entry.dependencies ??= Array.Empty<string>();
                    entry.layerIds = (entry.layerIds ?? Array.Empty<string>())
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => id.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }

                if (!catalog.layers.Any(layer => string.Equals(layer.id, _engineeringLayerFilterId, StringComparison.OrdinalIgnoreCase)))
                    _engineeringLayerFilterId = string.Empty;

                _engineeringCatalog = catalog;
                var selectedId = _selectedEngineeringCapability?.id;
                _selectedEngineeringCapability = catalog.entries.FirstOrDefault(entry =>
                    entry != null && string.Equals(entry.id, selectedId, StringComparison.OrdinalIgnoreCase))
                    ?? catalog.entries.FirstOrDefault(entry => entry != null);
                _engineeringDecisionBasisBuffer = _selectedEngineeringCapability?.decisionBasis ?? string.Empty;
                ResetEngineeringUsageNotesBuffer(_selectedEngineeringCapability);
            }
            catch (Exception exception)
            {
                _engineeringCatalogError = exception.Message;
                _selectedEngineeringCapability = null;
            }
        }

        private static string NormalizeEngineeringKind(string kind) => kind?.Trim().ToLowerInvariant() switch
        {
            "plugin" => "plugin",
            "architecture" => "architecture",
            "system" => "system",
            _ => "unknown"
        };

        private void DrawEngineeringCapabilities()
        {
            EditorGUILayout.LabelField(L("engineering.title"), ReportHeaderStyle());
            EditorGUILayout.HelpBox(L("engineering.summary"), MessageType.Info);

            if (!string.IsNullOrWhiteSpace(_engineeringCatalogError))
            {
                EditorGUILayout.HelpBox(
                    AgentWorkbenchText.Format("engineering.invalid", _engineeringCatalogError),
                    MessageType.Error);
                return;
            }

            if (!File.Exists(_engineeringCatalogPath))
            {
                EditorGUILayout.HelpBox(
                    AgentWorkbenchText.Format("engineering.missing", _engineeringCatalogPath),
                    MessageType.Warning);
                return;
            }

            var entries = (_engineeringCatalog?.entries ?? Array.Empty<EngineeringCapabilityEntry>())
                .Where(entry => entry != null)
                .OrderBy(entry => EngineeringKindOrder(entry.kind))
                .ThenBy(EngineeringDisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var visible = entries
                .Where(EngineeringEntryMatchesKindFilter)
                .Where(EngineeringEntryMatchesLayerFilter)
                .ToList();
            if (_selectedEngineeringCapability == null || !visible.Contains(_selectedEngineeringCapability))
                SelectEngineeringCapability(visible.FirstOrDefault());

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.VerticalScope(ReportPanelStyle(), GUILayout.Width(270), GUILayout.ExpandHeight(true)))
                {
                    DrawEngineeringKindFilters(entries);
                    DrawEngineeringLayerFilter();
                    EditorGUILayout.Space(8);
                    _engineeringListScroll = BeginVerticalScrollView(
                        _engineeringListScroll,
                        GUILayout.ExpandHeight(true));
                    if (visible.Count == 0)
                        EditorGUILayout.HelpBox(L("engineering.empty"), MessageType.Info);
                    foreach (var entry in visible)
                    {
                        var previous = GUI.backgroundColor;
                        GUI.backgroundColor = EngineeringKindColor(entry.kind);
                        if (GUILayout.Toggle(
                                entry == _selectedEngineeringCapability,
                                new GUIContent(EngineeringDisplayName(entry), EngineeringDescription(entry)),
                                GUI.skin.button,
                                GUILayout.MinHeight(34)))
                            SelectEngineeringCapability(entry);
                        GUI.backgroundColor = previous;
                    }
                    EditorGUILayout.EndScrollView();
                }

                GUILayout.Space(6);
                using (new EditorGUILayout.VerticalScope(ReportPanelStyle(), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    _engineeringDetailScroll = BeginVerticalScrollView(
                        _engineeringDetailScroll,
                        GUILayout.ExpandHeight(true));
                    DrawEngineeringCapabilityDetail(_selectedEngineeringCapability);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private bool EngineeringEntryMatchesKindFilter(EngineeringCapabilityEntry entry) =>
            _engineeringKindFilter == 0 ||
            (_engineeringKindFilter == 1 && entry.kind == "plugin") ||
            (_engineeringKindFilter == 2 && entry.kind == "architecture") ||
            (_engineeringKindFilter == 3 && entry.kind == "system");

        private bool EngineeringEntryMatchesLayerFilter(EngineeringCapabilityEntry entry) =>
            string.IsNullOrWhiteSpace(_engineeringLayerFilterId) ||
            (entry.layerIds ?? Array.Empty<string>()).Any(id =>
                string.Equals(id, _engineeringLayerFilterId, StringComparison.OrdinalIgnoreCase));

        private void DrawEngineeringKindFilters(IReadOnlyCollection<EngineeringCapabilityEntry> entries)
        {
            var labels = new[]
            {
                $"{L("engineering.all")} ({entries.Count})",
                $"{L("engineering.plugin")} ({entries.Count(entry => entry.kind == "plugin")})",
                $"{L("engineering.architecture")} ({entries.Count(entry => entry.kind == "architecture")})",
                $"{L("engineering.system")} ({entries.Count(entry => entry.kind == "system")})"
            };
            for (var index = 0; index < labels.Length; index++)
            {
                if (GUILayout.Toggle(
                        _engineeringKindFilter == index,
                        labels[index],
                        TabButtonStyle(),
                        GUILayout.Height(TabButtonHeight)))
                    _engineeringKindFilter = index;
            }
        }

        private void DrawEngineeringLayerFilter()
        {
            var layers = _engineeringCatalog?.layers ?? Array.Empty<EngineeringLayerDefinition>();
            if (layers.Length == 0)
                return;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(L("engineering.layerFilter"), EditorStyles.boldLabel);
            var ids = new[] { string.Empty }.Concat(layers.Select(layer => layer.id)).ToArray();
            var labels = new[] { L("engineering.allLayers") }
                .Concat(layers.Select(EngineeringLayerDisplayName))
                .ToArray();
            var selectedIndex = Array.FindIndex(ids, id =>
                string.Equals(id, _engineeringLayerFilterId, StringComparison.OrdinalIgnoreCase));
            selectedIndex = EditorGUILayout.Popup(Math.Max(0, selectedIndex), labels);
            _engineeringLayerFilterId = ids[selectedIndex];
        }

        private static int EngineeringKindOrder(string kind) => kind switch
        {
            "plugin" => 0,
            "architecture" => 1,
            "system" => 2,
            _ => 3
        };

        private void SelectEngineeringCapability(EngineeringCapabilityEntry entry)
        {
            if (entry == _selectedEngineeringCapability)
                return;
            _selectedEngineeringCapability = entry;
            _engineeringDecisionBasisBuffer = entry?.decisionBasis ?? string.Empty;
            ResetEngineeringUsageNotesBuffer(entry);
            _engineeringDetailScroll = Vector2.zero;
        }

        private void DrawEngineeringCapabilityDetail(EngineeringCapabilityEntry entry)
        {
            if (entry == null)
            {
                EditorGUILayout.HelpBox(L("engineering.empty"), MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(EngineeringDisplayName(entry), ReportTitleStyle());
            EditorGUILayout.LabelField(EngineeringKindLabel(entry.kind), EditorStyles.boldLabel);
            var description = EngineeringDescription(entry);
            if (!string.IsNullOrWhiteSpace(description))
                EditorGUILayout.LabelField(description, EngineeringWrapStyle());

            DrawEngineeringField(L("engineering.policyLevel"), entry.usagePolicy);
            DrawEngineeringField(L("engineering.version"), entry.version);
            DrawEngineeringField(L("engineering.source"), entry.source);
            DrawEngineeringArray(L("engineering.layers"), EngineeringLayerLabels(entry), false);

            if (_engineeringUsageNotesLanguage != _config.currentLanguage)
                ResetEngineeringUsageNotesBuffer(entry);
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(L("engineering.policy"), EditorStyles.boldLabel);
            _engineeringUsageNotesBuffer = EditorGUILayout.TextArea(
                _engineeringUsageNotesBuffer ?? string.Empty,
                GUILayout.MinHeight(82f));
            using (new EditorGUI.DisabledScope(string.Equals(
                       _engineeringUsageNotesBuffer ?? string.Empty,
                       EngineeringUsageNotes(entry),
                       StringComparison.Ordinal)))
            {
                if (GUILayout.Button(L("engineering.savePolicy"), GUILayout.Width(150f)))
                {
                    var fieldName = _config.currentLanguage == "en-US" ? "usageNotesEn" : "usageNotes";
                    SaveEngineeringEntryString(
                        entry,
                        fieldName,
                        _engineeringUsageNotesBuffer ?? string.Empty,
                        "engineering.policySaved");
                }
            }

            if (entry.kind == "architecture")
            {
                var valid = entry.locked && string.Equals(entry.usagePolicy, "required", StringComparison.OrdinalIgnoreCase);
                EditorGUILayout.HelpBox(
                    L(valid ? "engineering.architectureLocked" : "engineering.architectureInvalid"),
                    valid ? MessageType.Warning : MessageType.Error);
            }
            else if (entry.kind == "plugin")
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(L("engineering.decisionBasis"), EditorStyles.boldLabel);
                _engineeringDecisionBasisBuffer = EditorGUILayout.TextArea(
                    _engineeringDecisionBasisBuffer ?? string.Empty,
                    GUILayout.MinHeight(82f));
                using (new EditorGUI.DisabledScope(string.Equals(
                           _engineeringDecisionBasisBuffer ?? string.Empty,
                           entry.decisionBasis ?? string.Empty,
                           StringComparison.Ordinal)))
                {
                    if (GUILayout.Button(L("engineering.saveBasis"), GUILayout.Width(150f)))
                        SavePluginDecisionBasis(entry, _engineeringDecisionBasisBuffer ?? string.Empty);
                }
            }

            DrawEngineeringArray(L("engineering.capabilities"), EngineeringLocalizedArray(entry.capabilities, entry.capabilitiesEn), false);
            DrawEngineeringArray(L("engineering.constraints"), EngineeringLocalizedArray(entry.constraints, entry.constraintsEn), false);
            DrawEngineeringArray(L("engineering.evidence"), entry.evidence, true);

            var dependencies = (entry.dependencies ?? Array.Empty<string>())
                .Select(id => _engineeringCatalog.entries.FirstOrDefault(candidate =>
                    candidate != null && string.Equals(candidate.id, id, StringComparison.OrdinalIgnoreCase)))
                .Select(candidate => candidate == null ? string.Empty : EngineeringDisplayName(candidate))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            DrawEngineeringArray(L("engineering.dependencies"), dependencies, false);
        }

        private static void DrawEngineeringField(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.Width(94f));
                EditorGUILayout.SelectableLabel(value, EngineeringWrapStyle(), GUILayout.MinHeight(20f));
            }
        }

        private void DrawEngineeringArray(string label, IEnumerable<string> values, bool openPaths)
        {
            var items = (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            if (items.Length == 0)
                return;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            foreach (var item in items)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"• {item}", EngineeringWrapStyle(), GUILayout.ExpandWidth(true));
                    if (!openPaths)
                        continue;
                    var fullPath = AbsoluteProjectPath(item);
                    using (new EditorGUI.DisabledScope(!File.Exists(fullPath) && !Directory.Exists(fullPath)))
                    {
                        if (GUILayout.Button(L("engineering.openEvidence"), GUILayout.Width(54f)))
                            OpenPath(fullPath);
                    }
                }
            }
        }

        private void SavePluginDecisionBasis(EngineeringCapabilityEntry entry, string value)
        {
            if (entry == null || entry.kind != "plugin" || !File.Exists(_engineeringCatalogPath))
                return;
            SaveEngineeringEntryString(entry, "decisionBasis", value, "engineering.saved");
        }

        private void SaveEngineeringEntryString(
            EngineeringCapabilityEntry entry,
            string fieldName,
            string value,
            string notificationKey)
        {
            if (entry == null || string.IsNullOrWhiteSpace(fieldName) || !File.Exists(_engineeringCatalogPath))
                return;
            try
            {
                var json = File.ReadAllText(_engineeringCatalogPath, Encoding.UTF8);
                var idPattern = $"\\\"id\\\"\\s*:\\s*\\\"{Regex.Escape(entry.id)}\\\"";
                var idMatch = Regex.Match(json, idPattern);
                if (!idMatch.Success)
                    throw new InvalidDataException($"Entry not found: {entry.id}");
                var objectStart = json.LastIndexOf('{', idMatch.Index);
                var objectEnd = FindJsonObjectEnd(json, objectStart);
                if (objectStart < 0 || objectEnd <= objectStart)
                    throw new InvalidDataException($"Invalid entry object: {entry.id}");

                var entryJson = json.Substring(objectStart, objectEnd - objectStart + 1);
                var fieldRegex = new Regex(
                    $"(\\\"{Regex.Escape(fieldName)}\\\"\\s*:\\s*)\\\"(?:\\\\.|[^\\\"\\\\])*\\\"");
                if (fieldRegex.IsMatch(entryJson))
                {
                    entryJson = fieldRegex.Replace(
                        entryJson,
                        match => match.Groups[1].Value + EscapeEngineeringJsonString(value),
                        1);
                }
                else
                {
                    var policyAnchor = new Regex(
                        "(\\\"usagePolicy\\\"\\s*:\\s*\\\"(?:\\\\.|[^\\\"\\\\])*\\\",)");
                    if (!policyAnchor.IsMatch(entryJson))
                        throw new InvalidDataException($"usagePolicy field missing: {entry.id}");
                    entryJson = policyAnchor.Replace(
                        entryJson,
                        match => match.Groups[1].Value +
                                 $"{Environment.NewLine}      \"{fieldName}\": {EscapeEngineeringJsonString(value)},",
                        1);
                }
                json = json.Substring(0, objectStart) + entryJson + json.Substring(objectEnd + 1);
                var updatedAtRegex = new Regex("(\\\"updatedAt\\\"\\s*:\\s*)\\\"(?:\\\\.|[^\\\"\\\\])*\\\"");
                if (updatedAtRegex.IsMatch(json))
                {
                    json = updatedAtRegex.Replace(
                        json,
                        match => match.Groups[1].Value + EscapeEngineeringJsonString(DateTime.Now.ToString("o")),
                        1);
                }
                File.WriteAllText(_engineeringCatalogPath, json, new UTF8Encoding(false));
                ReloadEngineeringCatalog();
                ShowNotification(new GUIContent(L(notificationKey)));
            }
            catch (Exception exception)
            {
                _engineeringCatalogError = exception.Message;
            }
        }

        private void ResetEngineeringUsageNotesBuffer(EngineeringCapabilityEntry entry)
        {
            _engineeringUsageNotesLanguage = _config.currentLanguage;
            _engineeringUsageNotesBuffer = EngineeringUsageNotes(entry);
        }

        private string EngineeringUsageNotes(EngineeringCapabilityEntry entry) =>
            entry == null
                ? string.Empty
                : _config.currentLanguage == "en-US" ? entry.usageNotesEn ?? string.Empty : entry.usageNotes ?? string.Empty;

        private static int FindJsonObjectEnd(string json, int start)
        {
            if (start < 0)
                return -1;
            var depth = 0;
            var inString = false;
            var escaped = false;
            for (var index = start; index < json.Length; index++)
            {
                var character = json[index];
                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (character == '\\')
                        escaped = true;
                    else if (character == '"')
                        inString = false;
                    continue;
                }
                if (character == '"')
                    inString = true;
                else if (character == '{')
                    depth++;
                else if (character == '}' && --depth == 0)
                    return index;
            }
            return -1;
        }

        private static string EscapeEngineeringJsonString(string value) =>
            $"\"{(value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")}\"";

        private string EngineeringDisplayName(EngineeringCapabilityEntry entry) =>
            entry == null ? string.Empty : Or(EngineeringLocalizedText(entry.displayName, entry.displayNameEn), entry.id);

        private string EngineeringDescription(EngineeringCapabilityEntry entry) =>
            entry == null ? string.Empty : EngineeringLocalizedText(entry.description, entry.descriptionEn);

        private string EngineeringLayerDisplayName(EngineeringLayerDefinition layer) =>
            layer == null ? string.Empty : Or(EngineeringLocalizedText(layer.displayName, layer.displayNameEn), layer.id);

        private IEnumerable<string> EngineeringLayerLabels(EngineeringCapabilityEntry entry)
        {
            if (entry == null)
                return Array.Empty<string>();

            var definitions = _engineeringCatalog?.layers ?? Array.Empty<EngineeringLayerDefinition>();
            return (entry.layerIds ?? Array.Empty<string>()).Select(id =>
            {
                var definition = definitions.FirstOrDefault(layer =>
                    string.Equals(layer.id, id, StringComparison.OrdinalIgnoreCase));
                return definition == null ? id : EngineeringLayerDisplayName(definition);
            });
        }

        private string EngineeringLocalizedText(string chinese, string english) =>
            _config.currentLanguage == "en-US" && !string.IsNullOrWhiteSpace(english) ? english : chinese;

        private IEnumerable<string> EngineeringLocalizedArray(IEnumerable<string> chinese, IEnumerable<string> english)
        {
            var englishItems = (english ?? Array.Empty<string>()).ToArray();
            return _config.currentLanguage == "en-US" && englishItems.Length > 0
                ? englishItems
                : chinese ?? Array.Empty<string>();
        }

        private static string EngineeringKindLabel(string kind) => kind switch
        {
            "plugin" => L("engineering.plugin"),
            "architecture" => L("engineering.architecture"),
            "system" => L("engineering.system"),
            _ => kind
        };

        private static Color EngineeringKindColor(string kind) => kind switch
        {
            "plugin" => new Color(0.56f, 0.42f, 0.82f, 0.95f),
            "architecture" => new Color(0.20f, 0.66f, 0.78f, 0.95f),
            "system" => new Color(0.27f, 0.55f, 0.95f, 0.95f),
            _ => new Color(0.50f, 0.50f, 0.50f, 0.95f)
        };

        private static GUIStyle EngineeringWrapStyle() => new(EditorStyles.label)
        {
            wordWrap = true,
            richText = false
        };
    }
}
#endif
