#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AgentWorkflow.Editor
{
    public sealed partial class AgentWorkbenchWindow : EditorWindow
    {
        private static bool IsEditorGuidanceCategory(string category) =>
            string.Equals(category, "feature", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "architecture", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "system", StringComparison.OrdinalIgnoreCase);

        private static bool HasEditorGuidance(EditorGuidance guidance) =>
            guidance != null &&
            (!string.IsNullOrWhiteSpace(guidance.summary) ||
             HasGuidanceItems(guidance.inspectorReferences) ||
             HasGuidanceItems(guidance.tunableParameters) ||
             HasGuidanceItems(guidance.sceneSetup) ||
             HasGuidanceItems(guidance.usage));

        private static bool HasGuidanceItems(IEnumerable<string> items) =>
            items != null && items.Any(item => !string.IsNullOrWhiteSpace(item));

        private void ToggleEditorGuidance(string key)
        {
            if (!_visibleEditorGuidance.Add(key))
                _visibleEditorGuidance.Remove(key);
        }

        private void DrawEditorGuidance(IEnumerable<CapabilityEditorGuidance> entries)
        {
            foreach (var entry in entries.Where(item => item != null && HasEditorGuidance(item.Guidance)))
                DrawEditorGuidance(entry.Title, entry.Guidance);
        }

        private void DrawEditorGuidance(string title, EditorGuidance guidance)
        {
            if (!HasEditorGuidance(guidance))
                return;

            using (new EditorGUILayout.VerticalScope(ReportPanelStyle()))
            {
                EditorGUILayout.LabelField(title, ReportMiniHeaderStyle());
                if (!string.IsNullOrWhiteSpace(guidance.summary))
                    EditorGUILayout.HelpBox(guidance.summary.Trim(), MessageType.Info);
                DrawGuidanceItems(L("guidance.references"), guidance.inspectorReferences);
                DrawGuidanceItems(L("guidance.parameters"), guidance.tunableParameters);
                DrawGuidanceItems(L("guidance.sceneSetup"), guidance.sceneSetup);
                DrawGuidanceItems(L("guidance.usage"), guidance.usage);
            }
        }

        private static void DrawGuidanceItems(string heading, IEnumerable<string> items)
        {
            var visibleItems = (items ?? Array.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToList();
            if (visibleItems.Count == 0)
                return;

            EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
            foreach (var item in visibleItems)
                EditorGUILayout.LabelField("• " + item, EditorStyles.wordWrappedLabel);
        }
    }
}
#endif
