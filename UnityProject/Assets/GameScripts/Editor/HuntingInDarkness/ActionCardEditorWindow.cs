#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using SO.Boss.ActionCard;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Editor
{
    /// <summary>
    /// ZFramework target-project action-card authoring window. Uses Unity's native
    /// serialized inspector so the migrated project does not require Odin.
    /// </summary>
    public sealed class ActionCardEditorWindow : EditorWindow
    {
        private const string DefaultFolder =
            "Assets/GameScripts/GameLogic/HuntingInDarkness/SO/Character/ActionCard SO";

        private readonly List<CharacterActionCardData> _cards = new();
        private Vector2 _listScroll;
        private Vector2 _inspectorScroll;
        private CharacterActionCardData _selected;
        private UnityEditor.Editor _cachedEditor;

        [MenuItem("Tools/Hunting in Darkness/行动卡编辑器")]
        private static void Open()
        {
            var window = GetWindow<ActionCardEditorWindow>();
            window.titleContent = new GUIContent("行动卡编辑器");
            window.Show();
        }

        private void OnEnable() => RefreshCards();

        private void OnDisable()
        {
            if (_cachedEditor != null)
                DestroyImmediate(_cachedEditor);
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("＋ 新建卡牌", EditorStyles.toolbarButton))
                    CreateNewCard();
                if (GUILayout.Button("刷新", EditorStyles.toolbarButton))
                    RefreshCards();
                if (GUILayout.Button("保存", EditorStyles.toolbarButton))
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_selected == null))
                {
                    if (GUILayout.Button("删除", EditorStyles.toolbarButton))
                        DeleteCard(_selected);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawCardList();
                DrawSelectedInspector();
            }
        }

        private void DrawCardList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(230f)))
            {
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
                foreach (var card in _cards)
                {
                    if (card == null) continue;
                    bool selected = card == _selected;
                    if (GUILayout.Toggle(selected, card.name, "Button") != selected)
                        Select(card);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSelectedInspector()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (_selected == null)
                {
                    EditorGUILayout.HelpBox("选择或新建一张行动卡。", MessageType.Info);
                    return;
                }

                _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
                _cachedEditor ??= UnityEditor.Editor.CreateEditor(_selected);
                _cachedEditor.OnInspectorGUI();
                EditorGUILayout.EndScrollView();
            }
        }

        private void RefreshCards()
        {
            _cards.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CharacterActionCardData)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CharacterActionCardData>(path);
                if (card != null) _cards.Add(card);
            }
            _cards.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            Repaint();
        }

        private void Select(CharacterActionCardData card)
        {
            _selected = card;
            if (_cachedEditor != null) DestroyImmediate(_cachedEditor);
            _cachedEditor = card == null ? null : UnityEditor.Editor.CreateEditor(card);
            Selection.activeObject = card;
        }

        private void CreateNewCard()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "新建行动卡", "NewActionCard", "asset", "选择行动卡 SO 保存位置", DefaultFolder);
            if (string.IsNullOrEmpty(path)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? DefaultFolder);
            var card = CreateInstance<CharacterActionCardData>();
            card.cardName = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(card, path);
            AssetDatabase.SaveAssets();
            RefreshCards();
            Select(card);
        }

        private void DeleteCard(CharacterActionCardData card)
        {
            if (card == null || !EditorUtility.DisplayDialog(
                    "删除行动卡", $"确定删除「{card.name}」？此操作不可撤销。", "删除", "取消"))
                return;

            string path = AssetDatabase.GetAssetPath(card);
            Select(null);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
            RefreshCards();
        }
    }
}
#endif
