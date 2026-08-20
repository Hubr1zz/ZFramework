#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AgentWorkflow.Editor
{
    public sealed partial class AgentWorkbenchWindow : EditorWindow
    {
        private const float ChangeListWidth = 270f;
        private const float ChangePanelGap = 6f;

        private void DrawChangesPanel()
        {
            DrawChangeFolderControls();

            var visibleChanges = new List<ChangeEntry>();
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.VerticalScope(ReportPanelStyle(), GUILayout.Width(ChangeListWidth), GUILayout.ExpandHeight(true)))
                {
                    DrawChangeCategoryTabs();
                    EditorGUILayout.Space(4);
                    visibleChanges = _openChanges
                        .Where(ChangeMatchesCategory)
                        .Where(ChangeMatchesFolder)
                        .ToList();
                    if (_selectedChange == null || !visibleChanges.Contains(_selectedChange))
                        _selectedChange = visibleChanges.FirstOrDefault();
                    EditorGUILayout.LabelField($"Changes（{visibleChanges.Count}）", ReportHeaderStyle());
                    _changeListScroll.x = 0f;
                    _changeListScroll = BeginVerticalScrollView(
                        _changeListScroll,
                        GUILayout.ExpandWidth(true),
                        GUILayout.ExpandHeight(true));
                    if (visibleChanges.Count == 0)
                        EditorGUILayout.HelpBox(L("change.empty"), MessageType.Info);
                    foreach (var change in visibleChanges)
                        DrawChangeListItem(change);
                    EditorGUILayout.EndScrollView();
                }

                GUILayout.Space(ChangePanelGap);
                using (new EditorGUILayout.VerticalScope(ReportPanelStyle(), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    _changeDetailScroll.x = 0f;
                    _changeDetailScroll = BeginVerticalScrollView(
                        _changeDetailScroll,
                        GUILayout.ExpandWidth(true),
                        GUILayout.ExpandHeight(true));
                    if (_selectedChange == null)
                    {
                        var selectedFolder = SelectedCustomChangeFolder();
                        if (selectedFolder != null && !FolderHasAnyFormalEntry(selectedFolder.id))
                            DrawEmptyFormalFolderPanel(selectedFolder);
                        else
                            EditorGUILayout.HelpBox(L("change.empty"), MessageType.Info);
                    }
                    else
                        DrawChangeDetail(_selectedChange);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawChangeCategoryTabs()
        {
            var nextCategory = (SpecCategoryTab)DrawUniformTabs(
                (int)_changeCategoryTab,
                new[]
                {
                    $"{L("common.all")}（{_openChanges.Count}）",
                    $"{L("spec.architecture")}（{_openChanges.Count(change => ChangeHasCategory(change, "system"))}）",
                    $"{L("spec.feature")}（{_openChanges.Count(change => ChangeHasCategory(change, "feature"))}）",
                    $"{L("spec.rule")}（{_openChanges.Count(change => ChangeHasCategory(change, "game-rule"))}）"
                },
                TabButtonStyle(),
                TabButtonHeight);
            if (nextCategory != _changeCategoryTab)
                _changeCategoryTab = nextCategory;
        }

        private SpecFolderConfig SelectedCustomChangeFolder()
        {
            var folders = _config.specFolders ?? Array.Empty<SpecFolderConfig>();
            var index = _changeFolderFilterIndex - 2;
            return index >= 0 && index < folders.Length ? folders[index] : null;
        }

        private bool ChangeMatchesCategory(ChangeEntry change)
        {
            var category = _changeCategoryTab switch
            {
                SpecCategoryTab.System => "system",
                SpecCategoryTab.Feature => "feature",
                SpecCategoryTab.GameRule => "game-rule",
                _ => null
            };
            return category == null || ChangeHasCategory(change, category);
        }

        private static bool ChangeHasCategory(ChangeEntry change, string category) =>
            change != null &&
            ((change.Categories?.Contains(category) ?? false) || change.Category == category);

        private bool ChangeMatchesFolder(ChangeEntry change)
        {
            if (_changeFolderFilterIndex == 0)
                return true;
            var folderId = FolderIdFor(change.Capability);
            if (_changeFolderFilterIndex == 1)
                return string.IsNullOrWhiteSpace(folderId);
            var folders = _config.specFolders ?? Array.Empty<SpecFolderConfig>();
            var index = _changeFolderFilterIndex - 2;
            return index >= 0 && index < folders.Length && folders[index].id == folderId;
        }

        private void DrawChangeFolderControls()
        {
            var folders = _config.specFolders ?? Array.Empty<SpecFolderConfig>();
            var labels = new List<string> { L("spec.allFolders"), L("spec.noFolder") };
            labels.AddRange(folders.Select(folder => folder.name));
            _changeFolderFilterIndex = Mathf.Clamp(_changeFolderFilterIndex, 0, labels.Count - 1);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(L("spec.folder"), GUILayout.Width(48));
                _changeFolderFilterIndex = EditorGUILayout.Popup(
                    _changeFolderFilterIndex,
                    labels.ToArray(),
                    GUILayout.Width(180));
                GUILayout.Space(12);
                EditorGUILayout.LabelField(L("spec.newFolder"), GUILayout.Width(76));
                _newSpecFolderName = EditorGUILayout.TextField(_newSpecFolderName, GUILayout.Width(180));
                GUI.enabled = !string.IsNullOrWhiteSpace(_newSpecFolderName);
                if (GUILayout.Button(L("spec.create"), GUILayout.Width(58)))
                {
                    CreateSpecFolder(_newSpecFolderName);
                    _changeFolderFilterIndex = 0;
                }
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.Space(4);
        }

        private void DrawChangeListItem(ChangeEntry change)
        {
            const float rowHeight = 29f;
            if (ReferenceEquals(_renamingChange, change))
            {
                using (new EditorGUILayout.HorizontalScope(GUILayout.Height(rowHeight)))
                {
                    _changeRenameBuffer = EditorGUILayout.TextField(_changeRenameBuffer, GUILayout.Height(rowHeight - 2));
                    if (GUILayout.Button("✓", GUILayout.Width(28), GUILayout.Height(rowHeight - 2)))
                        CommitChangeRename(change);
                    if (GUILayout.Button("×", GUILayout.Width(28), GUILayout.Height(rowHeight - 2)))
                        CancelChangeRename();
                }
                return;
            }

            var rect = GUILayoutUtility.GetRect(1, rowHeight, GUILayout.ExpandWidth(true));
            const float folderButtonWidth = 28f;
            var selectRect = new Rect(rect.x, rect.y, rect.width - folderButtonWidth - 2f, rect.height);
            var folderRect = new Rect(selectRect.xMax + 2f, rect.y, folderButtonWidth, rect.height);
            if (Event.current.type == EventType.ContextClick && selectRect.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent(L("spec.rename")), false, () => BeginChangeRename(change));
                menu.ShowAsContext();
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseDown &&
                     Event.current.button == 0 &&
                     Event.current.clickCount == 2 &&
                     selectRect.Contains(Event.current.mousePosition))
            {
                BeginChangeRename(change);
                Event.current.Use();
            }
            if (GUI.Button(selectRect, GUIContent.none, SpecListItemStyle(ReferenceEquals(change, _selectedChange))))
            {
                if (TryLeaveRawEditor())
                {
                    _selectedChange = change;
                    _changeDetailScroll = Vector2.zero;
                }
            }

            var completed = change.Tasks.Count(task => task.Completed);
            var progress = change.Tasks.Count == 0
                ? "—"
                : $"{Mathf.RoundToInt(completed * 100f / change.Tasks.Count)}%";
            const float progressWidth = 42f;
            const float categoryWidth = 78f;
            GUI.Label(
                new Rect(selectRect.x + 8, selectRect.y, selectRect.width - progressWidth - categoryWidth - 20, selectRect.height),
                change.Title,
                SpecListNameStyle());
            GUI.Label(
                new Rect(selectRect.xMax - progressWidth - categoryWidth - 8, selectRect.y, categoryWidth, selectRect.height),
                CategoryLabel(change.Category),
                CategoryStyle(change.Category));
            var progressStyle = new GUIStyle(ReportMutedStyle())
            {
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 0, 0)
            };
            GUI.Label(
                new Rect(selectRect.xMax - progressWidth - 4, selectRect.y, progressWidth, selectRect.height),
                progress,
                progressStyle);
            if (GUI.Button(folderRect, FolderAssignmentContent(change.Capability), FolderListButtonStyle()))
                ShowFolderAssignmentMenu(change.Capability);
        }

        private void BeginChangeRename(ChangeEntry change)
        {
            if (!TryLeaveRawEditor())
                return;
            _renamingChange = change;
            _changeRenameBuffer = change.Title;
            Repaint();
        }

        private void CancelChangeRename()
        {
            _renamingChange = null;
            _changeRenameBuffer = string.Empty;
            Repaint();
        }

        private void CommitChangeRename(ChangeEntry change)
        {
            var nextTitle = _changeRenameBuffer.Trim();
            if (string.IsNullOrWhiteSpace(nextTitle))
            {
                ShowNotification(new GUIContent(L("change.renameEmpty")));
                return;
            }

            UpdateChangeDisplayTitle(change.Path, nextTitle);
            var changeName = change.Name;
            CancelChangeRename();
            AssetDatabase.Refresh();
            ReloadOpenSpec();
            _selectedChange = _openChanges.FirstOrDefault(item => item.Name == changeName);
        }

        private static void UpdateChangeDisplayTitle(string changeRoot, string nextTitle)
        {
            ReplaceJsonStringProperty(Path.Combine(changeRoot, "change-review.json"), "title", nextTitle);
            var proposalPath = Path.Combine(changeRoot, "proposal.md");
            if (!File.Exists(proposalPath))
                return;

            var content = File.ReadAllText(proposalPath, Encoding.UTF8);
            var heading = Regex.Match(
                content,
                @"^#\s+(?<prefix>(?:(?:Proposal|Change):\s*)?).+$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (!heading.Success)
                return;
            var replacement = "# " + heading.Groups["prefix"].Value + nextTitle;
            content = content.Remove(heading.Index, heading.Length)
                .Insert(heading.Index, replacement);
            File.WriteAllText(proposalPath, content, Encoding.UTF8);
        }

        private void DrawChangeDetail(ChangeEntry change)
        {
            if (change == null)
                return;

            using (new EditorGUILayout.VerticalScope(ReportSectionStyle()))
            {
                EditorGUILayout.LabelField(change.Title, ReportTitleStyle());
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(CategoryLabel(change.Category), CategoryStyle(change.Category), GUILayout.Width(92));
                    GUILayout.FlexibleSpace();
                    var guidanceKey = ChangeSectionKey("formal", change.Path, "editor-guidance");
                    if (change.EditorGuidance.Count > 0 && GUILayout.Button(
                            L("guidance.button"),
                            ReportActionButtonStyle(),
                            GUILayout.Width(88),
                            GUILayout.Height(28)))
                        ToggleEditorGuidance(guidanceKey);
                    var notesPath = Path.Combine(change.Path, "change-review.json");
                    if (GUILayout.Button(
                            L("change.notes"),
                            ReportActionButtonStyle(),
                            GUILayout.Width(64),
                            GUILayout.Height(28)))
                        ToggleChangeNotes(notesPath, change.ImplementationNotes);
                    if (GUILayout.Button(
                            L("change.copyId"),
                            ReportActionButtonStyle(),
                            GUILayout.Width(72),
                            GUILayout.Height(28)))
                    {
                        EditorGUIUtility.systemCopyBuffer = change.Name;
                        ShowNotification(new GUIContent(AgentWorkbenchText.Format("common.copied", change.Name)));
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    var canArchive = CanArchiveChange(change, out var archiveReason);
                    GUI.enabled = canArchive;
                    if (GUILayout.Button(
                            new GUIContent(L("change.archive"), archiveReason),
                            ReportActionButtonStyle(),
                            GUILayout.Width(64),
                            GUILayout.Height(28)))
                        ConfirmArchiveChange(change);
                    GUI.enabled = true;
                    if (GUILayout.Button(
                            L("common.delete"),
                            ReportActionButtonStyle(),
                            GUILayout.Width(64),
                            GUILayout.Height(28)))
                        ConfirmDeleteChange(change);
                    if (GUILayout.Button(L("common.openFolder"), ReportActionButtonStyle(), GUILayout.Width(82), GUILayout.Height(28)))
                        OpenPath(change.Path);
                }
                DrawTwoColumnFields(
                    new KeyValuePair<string, string>("Change", change.Name),
                    new KeyValuePair<string, string>("Code Readiness", Or(change.CodeReadiness, L("common.unassessed"))),
                    new KeyValuePair<string, string>("Approval", Or(change.ApprovalStatus, "implementation-change")),
                    new KeyValuePair<string, string>(L("change.syncStatus"), Or(change.SpecSyncStatus, "pending")),
                    new KeyValuePair<string, string>(L("common.source"), Or(change.SourceKind, "manual")));
                if (change.SyncValidation != null &&
                    change.SyncValidation.status is "conflict" or "review-required" or "merge-safe")
                {
                    var validationItems = string.Equals(change.SyncValidation.status, "conflict", StringComparison.OrdinalIgnoreCase)
                        ? change.SyncValidation.conflicts
                        : change.SyncValidation.changes;
                    var conflictDetails = validationItems == null
                        ? string.Empty
                        : string.Join("\n", validationItems
                            .Where(item => item != null)
                            .Select(item => $"• {Or(item.message, item.type)}"));
                    EditorGUILayout.HelpBox(
                        $"{L("change.syncValidation")}：{Or(change.SyncValidation.summary, L("graph.syncConflict"))}" +
                        (string.IsNullOrWhiteSpace(conflictDetails) ? string.Empty : $"\n{conflictDetails}"),
                        string.Equals(change.SyncValidation.status, "merge-safe", StringComparison.OrdinalIgnoreCase)
                            ? MessageType.Info
                            : MessageType.Warning);
                }
                var detailNotesPath = Path.Combine(change.Path, "change-review.json");
                if (IsEditingChangeNotes(detailNotesPath))
                    DrawChangeNotesEditor(detailNotesPath);

                var detailGuidanceKey = ChangeSectionKey("formal", change.Path, "editor-guidance");
                if (_visibleEditorGuidance.Contains(detailGuidanceKey))
                    DrawEditorGuidance(change.EditorGuidance);
            }

            DrawCollapsibleChangeSection(
                ChangeSectionKey("formal", change.Path, "review"),
                L("review.title"),
                () =>
                {
                if (!DrawStructuredTranslationGate(Path.Combine(change.Path, "change-review.json")))
                    return;
                EditorGUILayout.HelpBox(
                    $"{Or(change.Verification?.status, L("common.unverified"))}\n{Or(LocalizedVerificationSummary(Path.Combine(change.Path, "change-review.json"), change.Verification?.summary), L("common.noVerification"))}",
                    MessageType.Info);
                DrawEvidenceList(L("spec.evidence"), change.Evidence);
                DrawReviewIssueTable(
                    change.ReviewIssues,
                    (issue, accepted, note) => UpdateChangeReviewIssueAcceptance(change, issue, accepted, note));
                });

            DrawCollapsibleChangeSection(
                ChangeSectionKey("formal", change.Path, "dependencies"),
                $"{L("common.dependencies")}（{change.Dependencies.Count}）",
                () =>
                {
                    DrawDependencyTable(change.Dependencies);
                });

            DrawTaskProgress(
                ChangeSectionKey("formal", change.Path, "tasks"),
                change.Tasks,
                Path.Combine(change.Path, "tasks.md"),
                change.TasksContent);

            foreach (var spec in OrderedChangeSpecs(change))
            {
                DrawCollapsibleChangeSection(
                    ChangeSectionKey("formal", change.Path, $"spec:{spec.Capability}"),
                    $"{CategoryLabel(spec.Category)} · {spec.Title}",
                    () =>
                    {
                        DrawTwoColumnFields(
                            new KeyValuePair<string, string>("Capability", spec.Capability),
                            new KeyValuePair<string, string>(L("common.category"), CategoryLabel(spec.Category)));
                        DrawEditableMarkdown(
                            "Spec",
                            spec.Path,
                            spec.Content,
                            ChangeDetailContentWidth());
                    });
            }

            if (!string.IsNullOrWhiteSpace(change.ProposalContent))
            {
                DrawCollapsibleChangeSection(
                    ChangeSectionKey("formal", change.Path, "proposal"),
                    "Proposal",
                    () => DrawEditableMarkdown(
                        "Proposal",
                        Path.Combine(change.Path, "proposal.md"),
                        change.ProposalContent,
                        ChangeDetailContentWidth()));
            }

            if (!string.IsNullOrWhiteSpace(change.DesignContent))
            {
                DrawCollapsibleChangeSection(
                    ChangeSectionKey("formal", change.Path, "design"),
                    "Design",
                    () => DrawEditableMarkdown(
                        "Design",
                        Path.Combine(change.Path, "design.md"),
                        change.DesignContent,
                        ChangeDetailContentWidth()));
            }
        }

        private void DrawReviewIssueTable(
            IReadOnlyCollection<ReviewIssue> issues,
            Action<ReviewIssue, bool, string> updateAcceptance)
        {
            var visibleIssues = (issues ?? Array.Empty<ReviewIssue>())
                .Where(issue => issue != null)
                .ToList();
            if (visibleIssues.Count == 0)
            {
                EditorGUILayout.LabelField(L("review.none"), ReportMutedStyle());
                return;
            }

            foreach (var issue in visibleIssues)
                DrawReviewIssueRow(issue, updateAcceptance);
        }

        private void DrawReviewIssueRow(
            ReviewIssue issue,
            Action<ReviewIssue, bool, string> updateAcceptance)
        {
            var accepted = string.Equals(issue.status, "accepted", StringComparison.OrdinalIgnoreCase);
            var resolved = string.Equals(issue.status, "resolved", StringComparison.OrdinalIgnoreCase);
            var canAccept = !resolved && !string.Equals(issue.severity, "blocking", StringComparison.OrdinalIgnoreCase);
            var severityStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = ReviewSeverityColor(issue.severity) },
                alignment = TextAnchor.MiddleLeft
            };
            var content = Or(ReviewSummary(issue), L("common.noDetails"));
            if (!string.IsNullOrWhiteSpace(ReviewDetails(issue)))
                content += $"\n{ReviewDetails(issue)}";
            if (accepted)
                content += $"\n{AgentWorkbenchText.Format("review.acceptedBy", Or(issue.acceptedBy, "-"), Or(issue.acceptedAt, "-"))}";

            using (new EditorGUILayout.VerticalScope(ReportSectionStyle(), GUILayout.ExpandWidth(true)))
            {
                bool nextAccepted;
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(ReviewSeverityIcon(issue.severity), GUILayout.Width(18), GUILayout.Height(18));
                    GUILayout.Label(ReviewSeverityLabel(issue.severity), severityStyle, GUILayout.Width(64));
                    GUILayout.Label(ReviewTypeLabel(issue.type), ReportMutedStyle(), GUILayout.ExpandWidth(true));
                    GUILayout.Label(L("review.accept"), EditorStyles.miniBoldLabel, GUILayout.Width(48));
                    EditorGUI.BeginDisabledGroup(!canAccept);
                    nextAccepted = GUILayout.Toggle(accepted, GUIContent.none, GUILayout.Width(24));
                    EditorGUI.EndDisabledGroup();
                }
                GUILayout.Label(content, ReportMiniWrapStyle(), GUILayout.ExpandWidth(true));
                GUILayout.Label(L("review.acceptanceNote"), EditorStyles.miniBoldLabel);
                EditorGUI.BeginDisabledGroup(!canAccept);
                var nextNote = EditorGUILayout.TextArea(
                    issue.acceptanceNote ?? string.Empty,
                    GUILayout.ExpandWidth(true),
                    GUILayout.MinHeight(36));
                EditorGUI.EndDisabledGroup();

                var acceptanceChanged = nextAccepted != accepted;
                var acceptedNoteChanged = accepted && !string.Equals(nextNote, issue.acceptanceNote, StringComparison.Ordinal);
                issue.acceptanceNote = nextNote;
                if (canAccept && (acceptanceChanged || acceptedNoteChanged))
                    updateAcceptance?.Invoke(issue, nextAccepted, nextNote);
            }
        }

        private IEnumerable<SpecFile> OrderedChangeSpecs(ChangeEntry change)
        {
            var preferredCategory = _changeCategoryTab switch
            {
                SpecCategoryTab.System => "system",
                SpecCategoryTab.Feature => "feature",
                SpecCategoryTab.GameRule => "game-rule",
                _ => null
            };
            return (change?.Specs ?? new List<SpecFile>())
                .OrderBy(spec => preferredCategory != null && spec.Category == preferredCategory ? 0 : 1)
                .ThenBy(spec => spec.Category)
                .ThenBy(spec => spec.Title, StringComparer.OrdinalIgnoreCase);
        }

        private static GUIContent ReviewSeverityIcon(string severity) => severity?.ToLowerInvariant() switch
        {
            "blocking" => EditorGUIUtility.IconContent("console.erroricon.sml"),
            "warning" => EditorGUIUtility.IconContent("console.warnicon.sml"),
            _ => EditorGUIUtility.IconContent("console.infoicon.sml")
        };

        private static Color ReviewSeverityColor(string severity) => severity?.ToLowerInvariant() switch
        {
            "blocking" => new Color(1f, 0.38f, 0.32f),
            "warning" => new Color(1f, 0.68f, 0.20f),
            _ => new Color(0.38f, 0.70f, 1f)
        };

        private static string ReviewSeverityLabel(string severity) => severity?.ToLowerInvariant() switch
        {
            "blocking" => L("review.blocking"),
            "warning" => L("review.warning"),
            _ => L("review.info")
        };

        private static string ReviewTypeLabel(string type) => type switch
        {
            "design-conflict" => L("review.designConflict"),
            "dependency-missing" => L("review.dependencyMissing"),
            _ => L("review.implementationDelta")
        };

        private void UpdateChangeReviewIssueAcceptance(
            ChangeEntry change,
            ReviewIssue issue,
            bool accepted,
            string note)
        {
            if (change == null || issue == null)
                return;

            ApplyReviewIssueAcceptance(issue, accepted, note);
            var changeReviewPath = Path.Combine(change.Path, "change-review.json");
            UpdateReviewIssueFile<ChangeReview>(changeReviewPath, issue, review => review.reviewIssues, (review, items) =>
            {
                review.schemaVersion = 5;
                review.reviewIssues = items;
            });

            var specsPath = Path.Combine(change.Path, "specs");
            if (Directory.Exists(specsPath))
            {
                foreach (var reviewPath in Directory.GetFiles(specsPath, "spec-review.json", SearchOption.AllDirectories))
                    UpdateReviewIssueFile<ImportSpecReview>(reviewPath, issue, review => review.reviewIssues, (review, items) =>
                    {
                        review.schemaVersion = 5;
                        review.reviewIssues = items;
                    });
            }

            Repaint();
        }

        private static void UpdateReviewIssueFile<T>(
            string path,
            ReviewIssue updatedIssue,
            Func<T, ReviewIssue[]> getIssues,
            Action<T, ReviewIssue[]> setIssues) where T : class, new()
        {
            if (!File.Exists(path))
                return;
            var review = JsonUtility.FromJson<T>(File.ReadAllText(path, Encoding.UTF8)) ?? new T();
            var issues = getIssues(review) ?? Array.Empty<ReviewIssue>();
            var changed = false;
            foreach (var item in issues)
            {
                if (item == null || !string.Equals(item.id, updatedIssue.id, StringComparison.OrdinalIgnoreCase))
                    continue;
                CopyReviewIssueAcceptance(updatedIssue, item);
                changed = true;
            }
            if (!changed)
                return;
            setIssues(review, issues);
            File.WriteAllText(path, JsonUtility.ToJson(review, true), Encoding.UTF8);
        }

        private void ApplyReviewIssueAcceptance(ReviewIssue issue, bool accepted, string note)
        {
            var wasAccepted = string.Equals(issue.status, "accepted", StringComparison.OrdinalIgnoreCase);
            issue.status = accepted ? "accepted" : "open";
            issue.acceptedBy = accepted
                ? (wasAccepted ? Or(issue.acceptedBy, L("review.unknownActor")) : Or(_maintainer, L("review.unknownActor")))
                : string.Empty;
            issue.acceptedAt = accepted
                ? (wasAccepted ? Or(issue.acceptedAt, DateTime.Now.ToString("o")) : DateTime.Now.ToString("o"))
                : string.Empty;
            issue.acceptanceNote = accepted
                ? Or(note, L("review.acceptedInWorkbench"))
                : note ?? string.Empty;
        }

        private static void CopyReviewIssueAcceptance(ReviewIssue source, ReviewIssue target)
        {
            target.status = source.status;
            target.acceptedBy = source.acceptedBy;
            target.acceptedAt = source.acceptedAt;
            target.acceptanceNote = source.acceptanceNote;
        }

        private void DrawChangeFolderAssignment(ChangeEntry change)
        {
            if (change == null || string.IsNullOrWhiteSpace(change.Capability))
                return;
            var folders = _config.specFolders ?? Array.Empty<SpecFolderConfig>();
            var labels = new List<string> { L("spec.noFolder") };
            labels.AddRange(folders.Select(folder => folder.name));
            var currentId = FolderIdFor(change.Capability);
            var currentIndex = string.IsNullOrWhiteSpace(currentId)
                ? 0
                : Array.FindIndex(folders, folder => folder.id == currentId) + 1;
            currentIndex = Mathf.Max(0, currentIndex);
            var nextIndex = EditorGUILayout.Popup(L("spec.folder"), currentIndex, labels.ToArray());
            if (nextIndex != currentIndex)
                AssignSpecFolder(change.Capability, nextIndex == 0 ? null : folders[nextIndex - 1].id);
        }

        private void ConfirmDeleteChange(ChangeEntry change)
        {
            if (change == null || !EditorUtility.DisplayDialog(
                    L("change.deleteTitle"),
                    AgentWorkbenchText.Format("change.deleteConfirm", change.Title),
                    L("common.delete"),
                    L("spec.cancel")))
                return;
            var changesRoot = Path.GetFullPath(Path.Combine(_openSpecPath, "changes"));
            var target = Path.GetFullPath(change.Path);
            if (!target.StartsWith(changesRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return;
            Directory.Delete(target, true);
            AssetDatabase.Refresh();
            ReloadOpenSpec();
        }

        private bool CanArchiveChange(ChangeEntry change, out string reason)
        {
            if (change == null)
            {
                reason = L("change.archiveUnavailable");
                return false;
            }
            var incomplete = change.Tasks.Count(task => !task.Completed);
            if (incomplete > 0)
            {
                reason = AgentWorkbenchText.Format("change.archiveTasksPending", incomplete);
                return false;
            }
            if (!string.Equals(change.SpecSyncStatus, "synced", StringComparison.OrdinalIgnoreCase))
            {
                reason = L("change.archiveSyncPending");
                return false;
            }
            reason = L("change.archiveReady");
            return true;
        }

        private void ConfirmArchiveChange(ChangeEntry change)
        {
            if (!CanArchiveChange(change, out _) || !EditorUtility.DisplayDialog(
                    L("change.archiveTitle"),
                    AgentWorkbenchText.Format("change.archiveConfirm", change.Title),
                    L("change.archive"),
                    L("common.cancel")))
                return;

            var changesRoot = Path.GetFullPath(Path.Combine(_openSpecPath, "changes"));
            var source = Path.GetFullPath(change.Path);
            if (!source.StartsWith(changesRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return;
            var archiveRoot = Path.Combine(changesRoot, "archive");
            Directory.CreateDirectory(archiveRoot);
            var target = Path.Combine(archiveRoot, $"{DateTime.Now:yyyy-MM-dd}-{change.Name}");
            if (Directory.Exists(target))
            {
                EditorUtility.DisplayDialog(
                    L("change.archiveTitle"),
                    AgentWorkbenchText.Format("change.archiveExists", target),
                    L("common.confirm"));
                return;
            }
            Directory.Move(source, target);
            _selectedChange = null;
            AssetDatabase.Refresh();
            ReloadOpenSpec();
            ShowNotification(new GUIContent(L("change.archiveComplete")));
        }

        private void DrawTaskProgress(
            string sectionKey,
            IReadOnlyCollection<ChangeTask> tasks,
            string markdownPath = null,
            string markdown = null)
        {
            var total = tasks?.Count ?? 0;
            var completed = tasks?.Count(task => task.Completed) ?? 0;
            DrawCollapsibleChangeSection(sectionKey, $"Tasks（{completed}/{total}）", () =>
            {
                if (!string.IsNullOrWhiteSpace(markdownPath))
                {
                    if (string.Equals(_editingMarkdownPath, markdownPath, StringComparison.OrdinalIgnoreCase))
                    {
                        DrawMarkdownEditor(markdownPath, ChangeDetailContentWidth());
                        return;
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(L("spec.rawText"), ReportActionButtonStyle(), GUILayout.Width(112), GUILayout.Height(28)))
                            BeginMarkdownEdit("Tasks", markdownPath, markdown);
                    }
                }
                var rect = GUILayoutUtility.GetRect(1, 20, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(rect, total == 0 ? 0f : (float)completed / total, total == 0 ? L("change.noTasks") : $"{completed}/{total}");
                GUILayout.Space(4);
                if (tasks == null)
                    return;
                var canonicalMarkdown = !string.IsNullOrWhiteSpace(markdownPath) && File.Exists(markdownPath)
                    ? File.ReadAllText(markdownPath, Encoding.UTF8)
                    : markdown;
                var localized = string.IsNullOrWhiteSpace(markdownPath)
                    ? null
                    : ResolveLocalizedDocument(markdownPath, canonicalMarkdown);
                if (localized != null && localized.State != LocalizedDocumentState.Authority)
                {
                    DrawLocalizedMarkdown(markdownPath, canonicalMarkdown, ChangeDetailContentWidth());
                    return;
                }
                foreach (var task in tasks)
                    EditorGUILayout.LabelField(task.Completed ? $"✓ {task.Text}" : $"○ {task.Text}", ReportMiniWrapStyle());
            });
        }

        private void DrawCollapsibleChangeSection(
            string sectionKey,
            string title,
            Action drawContent,
            float fixedWidth = 0f)
        {
            var layoutOption = fixedWidth > 0f
                ? GUILayout.Width(fixedWidth)
                : GUILayout.ExpandWidth(true);
            using (new EditorGUILayout.VerticalScope(ReportSectionStyle(), layoutOption))
            {
                var expanded = !_collapsedChangeSections.Contains(sectionKey);
                var headerRect = GUILayoutUtility.GetRect(1f, 34f, GUILayout.ExpandWidth(true));
                var headerBackground = AgentWorkbenchTheme.IsDarkMode
                    ? new Color(0.16f, 0.19f, 0.24f)
                    : new Color(0.73f, 0.76f, 0.80f);
                EditorGUI.DrawRect(headerRect, headerBackground);
                EditorGUI.DrawRect(
                    new Rect(headerRect.x, headerRect.y, 4f, headerRect.height),
                    AgentWorkbenchTheme.HeadingColor(2, AgentWorkbenchTheme.IsDarkMode));

                if (GUI.Button(headerRect, GUIContent.none, GUIStyle.none))
                {
                    if (expanded)
                        _collapsedChangeSections.Add(sectionKey);
                    else
                        _collapsedChangeSections.Remove(sectionKey);
                    expanded = !expanded;
                    Repaint();
                }

                GUI.Label(
                    new Rect(headerRect.x + 12f, headerRect.y, 22f, headerRect.height),
                    expanded ? "▼" : "▶",
                    ChangeSectionTitleStyle());
                GUI.Label(
                    new Rect(headerRect.x + 34f, headerRect.y, headerRect.width - 42f, headerRect.height),
                    title,
                    ChangeSectionTitleStyle());

                if (!expanded)
                    return;
                GUILayout.Space(6f);
                drawContent?.Invoke();
            }
        }

        private static string ChangeSectionKey(string scope, string identity, string section) =>
            $"{scope}:{Or(identity, "unknown")}:{section}";

        private static string CategoryLabel(string category)
        {
            return category switch
            {
                "architecture" => L("spec.architecture"),
                "system" => L("spec.architecture"),
                "feature" => L("spec.feature"),
                "game-rule" => L("spec.rule"),
                "paired" => L("change.paired"),
                _ => L("spec.uncategorized")
            };
        }

        private static GUIStyle CategoryStyle(string category)
        {
            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = category switch
            {
                "architecture" => new Color(0.35f, 0.65f, 1f),
                "system" => new Color(0.35f, 0.65f, 1f),
                "feature" => new Color(0.35f, 0.82f, 0.5f),
                "game-rule" => new Color(0.95f, 0.68f, 0.2f),
                "paired" => new Color(0.72f, 0.56f, 0.95f),
                _ => Color.gray
            };
            return style;
        }

        private void DrawItem(QueueItem item)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField(item.Title, EditorStyles.boldLabel);
                DrawTwoColumnFields(
                    new KeyValuePair<string, string>(L("common.priority"), item.Priority),
                    new KeyValuePair<string, string>(L("common.status"), LocalizeQueueStatus(item.Status)),
                    new KeyValuePair<string, string>(L("common.file"), item.File),
                    new KeyValuePair<string, string>(L("common.type"), item.Type),
                    new KeyValuePair<string, string>(L("queue.maintainer"), item.Maintainer),
                    new KeyValuePair<string, string>(L("common.maintainedAt"), item.MaintainedAt),
                    new KeyValuePair<string, string>(L("queue.note"), item.MaintenanceNote),
                    new KeyValuePair<string, string>(L("common.source"), item.Source));

                if (!string.IsNullOrWhiteSpace(item.Description))
                {
                    EditorGUILayout.LabelField(L("common.description"), EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(item.Description, MessageType.None);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = item.Status != "已维护";
                    if (GUILayout.Button(L("queue.markDone"), GUILayout.Width(112)))
                        UpdateItem(item, "已维护", updateMaintenanceFields: true);

                    GUI.enabled = item.Status != "进行中";
                    if (GUILayout.Button(L("queue.markProgress"), GUILayout.Width(112)))
                        UpdateItem(item, "进行中", updateMaintenanceFields: false);

                    GUI.enabled = item.Status != "待处理";
                    if (GUILayout.Button(L("queue.restore"), GUILayout.Width(112)))
                        UpdateItem(item, "待处理", updateMaintenanceFields: false);

                    GUI.enabled = true;
                }
            }
        }

        private static string LocalizeQueueStatus(string status)
        {
            return status switch
            {
                "已维护" => L("queue.done"),
                "进行中" => L("queue.progress"),
                _ => L("queue.pending")
            };
        }

        private static string LocalizeStatus(string value)
        {
            return value != null && value.StartsWith("sync.", StringComparison.Ordinal)
                ? L(value)
                : value;
        }

        private static string ImportSourceSummary(DesignImportRun run)
        {
            if (run?.sourceRoots != null && run.sourceRoots.Length > 0)
                return string.Join("; ", run.sourceRoots.Select(source => $"{source.id}: {source.path}"));
            return Or(run?.source, L("common.unknown"));
        }

        private static void DrawField(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                value = "-";

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, EditorStyles.miniBoldLabel, GUILayout.Width(64));
                EditorGUILayout.SelectableLabel(value, EditorStyles.miniLabel, GUILayout.Height(18));
            }
        }

        private static void DrawTwoColumnFields(params KeyValuePair<string, string>[] fields)
        {
            const float twoColumnMinimumWidth = 640f;
            const float columnSpacing = 8f;
            var widthProbe = GUILayoutUtility.GetRect(1f, 0f, GUILayout.ExpandWidth(true));
            var availableWidth = Mathf.Max(1f, widthProbe.width);
            var columnCount = availableWidth >= twoColumnMinimumWidth ? 2 : 1;

            for (var index = 0; index < fields.Length; index += columnCount)
            {
                var columnWidth = columnCount == 2
                    ? (availableWidth - columnSpacing) * 0.5f
                    : availableWidth;
                var rowHeight = CompactFieldHeight(fields[index], columnWidth);
                if (columnCount == 2 && index + 1 < fields.Length)
                    rowHeight = Mathf.Max(rowHeight, CompactFieldHeight(fields[index + 1], columnWidth));

                var row = GUILayoutUtility.GetRect(1f, rowHeight, GUILayout.ExpandWidth(true));
                columnWidth = columnCount == 2
                    ? (row.width - columnSpacing) * 0.5f
                    : row.width;
                DrawCompactField(new Rect(row.x, row.y, columnWidth, rowHeight), fields[index]);
                if (columnCount == 2 && index + 1 < fields.Length)
                    DrawCompactField(
                        new Rect(row.x + columnWidth + columnSpacing, row.y, columnWidth, rowHeight),
                        fields[index + 1]);
            }
        }

        private float ChangeDetailContentWidth()
        {
            return CurrentLayoutContentWidth();
        }

        private bool IsEditingChangeNotes(string path) =>
            !string.IsNullOrWhiteSpace(path) &&
            string.Equals(_editingChangeNotesPath, path, StringComparison.OrdinalIgnoreCase);

        private void ToggleChangeNotes(string path, string currentNotes)
        {
            if (IsEditingChangeNotes(path))
            {
                _editingChangeNotesPath = null;
                _editingChangeNotesBuffer = string.Empty;
                return;
            }
            _editingChangeNotesPath = path;
            _editingChangeNotesBuffer = currentNotes ?? string.Empty;
        }

        private void DrawChangeNotesEditor(string reviewPath)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(L("change.notesHint"), ReportMiniWrapStyle());
                _editingChangeNotesBuffer = EditorGUILayout.TextArea(
                    _editingChangeNotesBuffer,
                    GUILayout.MinHeight(72),
                    GUILayout.ExpandWidth(true));
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(L("spec.save"), ReportActionButtonStyle(), GUILayout.Width(72), GUILayout.Height(26)))
                        SaveChangeNotes(reviewPath);
                    if (GUILayout.Button(L("spec.discard"), ReportActionButtonStyle(), GUILayout.Width(72), GUILayout.Height(26)))
                    {
                        _editingChangeNotesPath = null;
                        _editingChangeNotesBuffer = string.Empty;
                    }
                }
            }
        }

        private void SaveChangeNotes(string reviewPath)
        {
            if (string.IsNullOrWhiteSpace(reviewPath) || !File.Exists(reviewPath))
                return;
            ChangeReview review;
            try
            {
                review = JsonUtility.FromJson<ChangeReview>(File.ReadAllText(reviewPath, Encoding.UTF8)) ??
                         new ChangeReview();
            }
            catch (Exception exception)
            {
                ShowNotification(new GUIContent(AgentWorkbenchText.Format("common.readFailed", exception.Message)));
                return;
            }
            review.implementationNotes = _editingChangeNotesBuffer.Trim();
            File.WriteAllText(reviewPath, JsonUtility.ToJson(review, true) + Environment.NewLine, Encoding.UTF8);
            _editingChangeNotesPath = null;
            _editingChangeNotesBuffer = string.Empty;
            ShowNotification(new GUIContent(L("change.notesSaved")));
            AssetDatabase.Refresh();
            if (reviewPath.StartsWith(_draftStorePath, StringComparison.OrdinalIgnoreCase))
                ReloadDesignImports();
            else
                ReloadOpenSpec();
        }

        private static float CompactFieldHeight(KeyValuePair<string, string> field, float width)
        {
            var value = string.IsNullOrWhiteSpace(field.Value) ? "-" : field.Value;
            var labelHeight = CompactFieldLabelStyle().CalcHeight(new GUIContent(field.Key), width);
            var valueHeight = CompactFieldValueStyle().CalcHeight(new GUIContent(value), width);
            return Mathf.Max(38f, labelHeight + valueHeight + 4f);
        }

        private static void DrawCompactField(Rect rect, KeyValuePair<string, string> field)
        {
            var value = string.IsNullOrWhiteSpace(field.Value) ? "-" : field.Value;
            var labelStyle = CompactFieldLabelStyle();
            var valueStyle = CompactFieldValueStyle();
            var labelHeight = labelStyle.CalcHeight(new GUIContent(field.Key), rect.width);
            GUI.Label(new Rect(rect.x, rect.y, rect.width, labelHeight), field.Key, labelStyle);
            EditorGUI.SelectableLabel(
                new Rect(rect.x, rect.y + labelHeight + 2f, rect.width, Mathf.Max(0f, rect.height - labelHeight - 2f)),
                value,
                valueStyle);
        }

        private static GUIStyle CompactFieldLabelStyle()
        {
            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            style.normal.textColor = ThemePrimaryTextColor();
            return style;
        }

        private static GUIStyle CompactFieldValueStyle()
        {
            var style = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            style.normal.textColor = ThemePrimaryTextColor();
            return style;
        }

    }
}
#endif
