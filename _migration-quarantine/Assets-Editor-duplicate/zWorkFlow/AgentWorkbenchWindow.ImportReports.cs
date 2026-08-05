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
        private void DrawImportReportsTab()
        {
            if (_designImports.Count == 0)
            {
                EditorGUILayout.HelpBox(L("import.empty"), MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.VerticalScope(ReportPanelStyle(), GUILayout.Width(270), GUILayout.ExpandHeight(true)))
                {
                    DrawImportNavigation();
                }

                GUILayout.Space(6);
                using (new EditorGUILayout.VerticalScope(
                           ReportPanelStyle(),
                           GUILayout.ExpandWidth(true),
                           GUILayout.ExpandHeight(true)))
                {
                    if (!_showImportSpecNavigation || _selectedImportRun == null)
                    {
                        EditorGUILayout.HelpBox(L("import.selectRun"), MessageType.Info);
                    }
                    else
                    {
                        _importSpecDetailScroll = BeginVerticalScrollView(
                            _importSpecDetailScroll,
                            GUILayout.ExpandWidth(true),
                            GUILayout.ExpandHeight(true));
                        DrawImportRunDetail(_selectedImportRun);
                        EditorGUILayout.EndScrollView();
                    }
                }
            }
        }

        private void DrawImportNavigation()
        {
            if (!_showImportSpecNavigation || _selectedImportRun == null)
            {
                EditorGUILayout.LabelField($"{L("import.records")}（{_designImports.Count}）", ReportHeaderStyle());
                _importRunScroll = BeginVerticalScrollView(_importRunScroll);
                foreach (var run in _designImports.OrderByDescending(item => item.createdAt))
                {
                    var style = ReportButtonStyle(false);
                    var label =
                        $"{run.runId}\n{Or(run.scope, L("import.scopeAll"))} · {Or(run.publicationStatus, run.status)}";
                    if (GUILayout.Button(
                            label,
                            style,
                            GUILayout.Height(ReportButtonHeight(label, style, 248)),
                            GUILayout.ExpandWidth(true)) && TryLeaveRawEditor())
                        SelectImportRun(run);
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(L("common.back"), ReportActionButtonStyle(), GUILayout.Width(68), GUILayout.Height(26)))
                {
                    _showImportSpecNavigation = false;
                    _showImportRunInformation = false;
                    _importRunScroll = Vector2.zero;
                    return;
                }
                var runInformationStyle = ReportButtonStyle(_showImportRunInformation);
                runInformationStyle.alignment = TextAnchor.MiddleCenter;
                if (GUILayout.Button(
                        L("import.recordInfo"),
                        runInformationStyle,
                        GUILayout.ExpandWidth(true),
                        GUILayout.Height(26)))
                {
                    if (TryLeaveRawEditor())
                    {
                        _showImportRunInformation = true;
                        _showImportAuditNotices = true;
                        _importSpecDetailScroll = Vector2.zero;
                    }
                }
            }
            EditorGUILayout.LabelField(_selectedImportRun.runId, ReportMutedStyle());
            EditorGUILayout.LabelField(
                $"{L("import.proposals")}（{_selectedImportRun.SpecGroups?.Count ?? 0}）",
                ReportHeaderStyle());

            var groups = _selectedImportRun.SpecGroups ?? new List<DraftSpecGroup>();
            var nextCategory = (SpecCategoryTab)DrawUniformTabs(
                (int)_draftCategoryTab,
                new[]
                {
                    $"{L("common.all")}（{groups.Count}）",
                    $"{L("spec.architecture")}（{groups.Count(group => DraftGroupCategory(group) == "system")}）",
                    $"{L("spec.feature")}（{groups.Count(group => DraftGroupCategory(group) == "feature")}）",
                    $"{L("spec.rule")}（{groups.Count(group => DraftGroupCategory(group) == "game-rule")}）"
                },
                TabButtonStyle(),
                TabButtonHeight);
            if (nextCategory != _draftCategoryTab)
            {
                _draftCategoryTab = nextCategory;
                _importSpecListScroll = Vector2.zero;
                CancelDraftRename();
            }

            var visibleGroups = groups.Where(DraftGroupMatchesCategory).ToList();
            if (_selectedDraftGroup == null || !visibleGroups.Contains(_selectedDraftGroup))
                SelectDraftGroup(visibleGroups.FirstOrDefault());

            _importSpecListScroll = BeginVerticalScrollView(_importSpecListScroll);
            if (visibleGroups.Count == 0)
                EditorGUILayout.HelpBox(L("import.categoryEmpty"), MessageType.Info);
            foreach (var group in visibleGroups)
            {
                if (ReferenceEquals(_renamingDraftGroup, group))
                {
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(44)))
                    {
                        _draftRenameBuffer = EditorGUILayout.TextField(_draftRenameBuffer, GUILayout.Height(30));
                        if (GUILayout.Button("✓", GUILayout.Width(28), GUILayout.Height(30)))
                            CommitDraftRename(group);
                        if (GUILayout.Button("×", GUILayout.Width(28), GUILayout.Height(30)))
                            CancelDraftRename();
                    }
                    continue;
                }

                var selected = !_showImportRunInformation && ReferenceEquals(group, _selectedDraftGroup);
                var style = ReportButtonStyle(selected);
                var current = SelectedVersion(group)?.Spec;
                var readiness = Or(current?.Readiness, L("common.unknown"));
                var conflict = group.Versions.Count > 1
                    ? $" · {group.Versions.Count} {L("import.versions")}"
                    : string.Empty;
                var label = $"{group.Title}\n{CategoryLabel(current?.Category)} · {readiness}{conflict}";
                var rowRect = GUILayoutUtility.GetRect(
                    1,
                    ReportButtonHeight(label, style, 248),
                    GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent(L("spec.rename")), false, () => BeginDraftRename(group));
                    menu.ShowAsContext();
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseDown &&
                         Event.current.button == 0 &&
                         Event.current.clickCount == 2 &&
                         rowRect.Contains(Event.current.mousePosition))
                {
                    BeginDraftRename(group);
                    Event.current.Use();
                }
                if (GUI.Button(rowRect, label, style))
                {
                    if (TryLeaveRawEditor())
                    {
                        SelectDraftGroup(group);
                        _showImportRunInformation = false;
                        _importSpecDetailScroll = Vector2.zero;
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static string DraftGroupCategory(DraftSpecGroup group) =>
            SelectedVersion(group)?.Spec?.Category ?? string.Empty;

        private bool DraftGroupMatchesCategory(DraftSpecGroup group)
        {
            var category = _draftCategoryTab switch
            {
                SpecCategoryTab.System => "system",
                SpecCategoryTab.Feature => "feature",
                SpecCategoryTab.GameRule => "game-rule",
                _ => null
            };
            return category == null || DraftGroupCategory(group) == category;
        }

        private void SelectImportRun(DesignImportRun run)
        {
            _selectedImportRun = run;
            _selectedDraftGroup = run?.SpecGroups?.FirstOrDefault();
            _selectedImportSpec = SelectedVersion(_selectedDraftGroup)?.Spec;
            _showImportSpecNavigation = true;
            _showImportRunInformation = true;
            _showImportAuditNotices = true;
            _importSpecListScroll = Vector2.zero;
            _importSpecDetailScroll = Vector2.zero;
        }

        private static DraftSpecVersion SelectedVersion(DraftSpecGroup group)
        {
            if (group == null || group.Versions.Count == 0)
                return null;
            return group.Versions.FirstOrDefault(item =>
                       string.Equals(item.id, group.selectedVersionId, StringComparison.OrdinalIgnoreCase)) ??
                   group.Versions[0];
        }

        private void SelectDraftGroup(DraftSpecGroup group)
        {
            _selectedDraftGroup = group;
            _selectedImportSpec = SelectedVersion(group)?.Spec;
        }

        private void SelectDraftSpec(DesignImportSpec spec)
        {
            if (spec == null || _selectedImportRun?.SpecGroups == null)
                return;
            var group = _selectedImportRun.SpecGroups.FirstOrDefault(item =>
                string.Equals(item.capability, spec.Capability, StringComparison.OrdinalIgnoreCase));
            if (group == null)
                return;
            _draftCategoryTab = CategoryTabFor(spec.Category);
            SelectDraftGroup(group);
            _showImportRunInformation = false;
            _importSpecDetailScroll = Vector2.zero;
        }

        private void BeginDraftRename(DraftSpecGroup group)
        {
            if (!TryLeaveRawEditor())
                return;
            _renamingDraftGroup = group;
            _draftRenameBuffer = group.Title;
            Repaint();
        }

        private void CancelDraftRename()
        {
            _renamingDraftGroup = null;
            _draftRenameBuffer = string.Empty;
            Repaint();
        }

        private void CommitDraftRename(DraftSpecGroup group)
        {
            var nextTitle = _draftRenameBuffer.Trim();
            if (string.IsNullOrWhiteSpace(nextTitle))
            {
                ShowNotification(new GUIContent(L("import.renameEmpty")));
                return;
            }

            var selectedVersion = SelectedVersion(group);
            var spec = selectedVersion?.Spec;
            var storedGroup = _draftSpecGroups.FirstOrDefault(item =>
                string.Equals(item.capability, group.capability, StringComparison.OrdinalIgnoreCase));
            var storedVersion = storedGroup?.Versions.FirstOrDefault(item =>
                string.Equals(item.id, selectedVersion?.id, StringComparison.OrdinalIgnoreCase));
            if (spec == null || storedGroup == null || storedVersion == null)
                return;

            if (CurrentLanguageIsAuthority(spec.SpecPath))
            {
                UpdateSpecDisplayTitle(spec.SpecPath, spec.ReviewPath, nextTitle);
                storedGroup.title = nextTitle;
                storedVersion.contentHash = StableSpecHash(File.ReadAllText(spec.SpecPath, Encoding.UTF8));

                var draftRoot = spec.DraftChangePath;
                if (!string.IsNullOrWhiteSpace(draftRoot))
                {
                    ReplaceDependencyNodeLabel(
                        Path.Combine(draftRoot, "dependencies.json"),
                        spec.Capability,
                        nextTitle);
                    UpdateDraftChangeTitleWhenPrimary(draftRoot, spec.Capability, nextTitle);
                }
                foreach (var runId in storedVersion.runIds ?? Array.Empty<string>())
                {
                    ReplaceDependencyNodeLabel(
                        Path.Combine(_designImportsPath, runId, "dependencies.json"),
                        spec.Capability,
                        nextTitle);
                }

                SaveDraftStore();
            }
            else
            {
                SaveSpecTitle(spec.Capability, CurrentWorkbenchLanguage, nextTitle);
            }

            CancelDraftRename();
            AssetDatabase.Refresh();
            ReloadDesignImports();
        }

        private static void UpdateDraftChangeTitleWhenPrimary(
            string draftRoot,
            string capability,
            string nextTitle)
        {
            var reviewPath = Path.Combine(draftRoot, "change-review.json");
            if (!File.Exists(reviewPath))
                return;
            ChangeReview review;
            try
            {
                review = JsonUtility.FromJson<ChangeReview>(File.ReadAllText(reviewPath, Encoding.UTF8));
            }
            catch
            {
                return;
            }
            if (!string.Equals(
                    review?.capabilities?.FirstOrDefault(),
                    capability,
                    StringComparison.OrdinalIgnoreCase))
                return;
            UpdateChangeDisplayTitle(draftRoot, nextTitle);
        }

        private void DrawDraftConflictSelector(DraftSpecGroup group)
        {
            if (group == null || group.Versions.Count <= 1)
                return;

            using (new EditorGUILayout.VerticalScope(ReportSectionStyle()))
            {
                EditorGUILayout.LabelField(L("import.conflictTitle"), ReportHeaderStyle());
                EditorGUILayout.HelpBox(L("import.conflictHint"), MessageType.Warning);
                foreach (var version in group.Versions)
                {
                    var selected = ReferenceEquals(version.Spec, _selectedImportSpec);
                    var runLabels = version.runIds == null ? string.Empty : string.Join(", ", version.runIds);
                    var label = $"{version.id} · {runLabels} · {Or(version.createdAt, L("common.unknown"))}";
                    if (EditorGUILayout.ToggleLeft(label, selected) && !selected)
                    {
                        if (TryLeaveRawEditor())
                        {
                            _selectedImportSpec = version.Spec;
                        }
                    }
                }

                GUI.enabled = _selectedImportSpec != null;
                if (GUILayout.Button(L("import.useVersion"), ReportActionButtonStyle(), GUILayout.Width(128), GUILayout.Height(30)))
                    ResolveDraftConflict(group, _selectedImportSpec);
                GUI.enabled = true;
            }
        }

        private void ResolveDraftConflict(DraftSpecGroup group, DesignImportSpec selected)
        {
            if (group == null || selected == null ||
                !EditorUtility.DisplayDialog(L("import.resolveTitle"), L("import.resolveConfirm"), L("common.confirm"), L("common.cancel")))
                return;

            var stored = _draftSpecGroups.FirstOrDefault(item =>
                string.Equals(item.capability, group.capability, StringComparison.OrdinalIgnoreCase));
            var winner = stored?.Versions.FirstOrDefault(item =>
                string.Equals(item.id, selected.VersionId, StringComparison.OrdinalIgnoreCase));
            if (stored == null || winner == null)
                return;

            winner.runIds = stored.Versions
                .SelectMany(item => item.runIds ?? Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var loser in stored.Versions.Where(item => !ReferenceEquals(item, winner)).ToList())
                DeleteDraftVersionFiles(loser);
            stored.versions = new List<DraftSpecVersion> { winner };
            stored.selectedVersionId = winner.id;
            stored.status = "ready";
            SaveDraftStore();
            ReloadDesignImports();
        }

        private void ConfirmDeleteDraftSpec(DraftSpecGroup group, DesignImportSpec spec)
        {
            if (group == null || spec == null ||
                !EditorUtility.DisplayDialog(L("import.deleteTitle"), L("import.deleteConfirm"), L("common.delete"), L("common.cancel")))
                return;

            var stored = _draftSpecGroups.FirstOrDefault(item =>
                string.Equals(item.capability, group.capability, StringComparison.OrdinalIgnoreCase));
            var version = stored?.Versions.FirstOrDefault(item =>
                string.Equals(item.id, spec.VersionId, StringComparison.OrdinalIgnoreCase));
            if (stored == null || version == null)
                return;

            var deletedChangePath = AbsoluteProjectPath(version.draftChangePath);
            DeleteDraftVersionFiles(version);
            foreach (var item in _draftSpecGroups.ToList())
            {
                item.Versions.RemoveAll(candidate => string.Equals(
                    AbsoluteProjectPath(candidate.draftChangePath), deletedChangePath, StringComparison.OrdinalIgnoreCase));
                if (item.Versions.Count == 0)
                    _draftSpecGroups.Remove(item);
                else
                {
                    item.selectedVersionId = item.Versions[0].id;
                    item.status = item.Versions.Count > 1 ? "conflict" : "ready";
                }
            }
            SaveDraftStore();
            ReloadDesignImports();
        }

        private void DrawImportRunDetail(DesignImportRun run)
        {
            if (run == null)
                return;

            if (_showImportRunInformation)
            {
                DrawImportRunInformation(run);
                return;
            }

            if (run.SpecGroups == null || run.SpecGroups.Count == 0)
            {
                EditorGUILayout.HelpBox(L("import.noSpecs"), MessageType.Warning);
                return;
            }

            DrawDraftConflictSelector(_selectedDraftGroup);
            DrawImportSpecDetail(_selectedImportSpec);
        }

        private void DrawImportRunInformation(DesignImportRun run)
        {
            EditorGUILayout.LabelField($"{L("import.recordInfo")} · {run.runId}", ReportHeaderStyle());
            using (new EditorGUILayout.VerticalScope(ReportSectionStyle()))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    var noticeCount = ImportAuditNoticeCount(run);
                    GUI.enabled = noticeCount > 0;
                    if (GUILayout.Button(
                            _showImportAuditNotices ? L("import.hideNotices") : L("import.showNotices"),
                            ReportActionButtonStyle(),
                            GUILayout.Width(92),
                            GUILayout.Height(28)))
                        _showImportAuditNotices = !_showImportAuditNotices;
                    GUI.enabled = true;
                    if (GUILayout.Button(L("common.openFolder"), ReportActionButtonStyle(), GUILayout.Width(82), GUILayout.Height(28)))
                        OpenPath(run.DirectoryPath);
                }
                DrawTwoColumnFields(
                    new KeyValuePair<string, string>(L("common.status"), Or(run.publicationStatus, run.status)),
                    new KeyValuePair<string, string>("Scope", Or(run.scope, L("import.scopeAll"))),
                    new KeyValuePair<string, string>(
                        L("common.type"),
                        run.typeFilters == null || run.typeFilters.Length == 0
                            ? L("common.all")
                            : string.Join("、", run.typeFilters)),
                    new KeyValuePair<string, string>(L("common.created"), run.createdAt),
                    new KeyValuePair<string, string>(L("common.missing"), $"{run.GapCount}（{L("common.blocking")} {run.BlockingGapCount}）"),
                    new KeyValuePair<string, string>(L("common.source"), ImportSourceSummary(run)),
                    new KeyValuePair<string, string>(
                        L("import.documents"),
                        $"{L("import.requirementDocuments")} {run.documentCounts?.requirements ?? 0} · " +
                        $"{L("import.contextDocuments")} {run.documentCounts?.context ?? 0}"),
                    new KeyValuePair<string, string>(
                        L("import.duplicateCandidates"),
                        $"{run.duplicatePrecheck?.candidateCount ?? 0}"),
                    new KeyValuePair<string, string>(
                        L("import.auditNotices"),
                        $"{L("import.uncertainties")} {run.uncertaintyCandidates?.Length ?? 0} · " +
                        $"{L("import.ambiguousLinks")} {run.ambiguousLinks?.Length ?? 0}"));
            }

            if (_showImportAuditNotices)
                DrawImportRunAuditNotices(run);
        }

        private void DrawImportRunAuditNotices(DesignImportRun run)
        {
            var uncertainties = run.uncertaintyCandidates ?? Array.Empty<DesignImportAuditItem>();
            var ambiguities = run.ambiguousLinks ?? Array.Empty<DesignImportLinkAmbiguity>();
            var preserved = run.typeFilterAudit?.preservedConstraints ?? Array.Empty<DesignImportAuditItem>();
            var mixed = run.typeFilterAudit?.mixedDescriptions ?? Array.Empty<DesignImportAuditItem>();
            if (uncertainties.Length == 0 && ambiguities.Length == 0 && preserved.Length == 0 && mixed.Length == 0)
                return;

            using (new EditorGUILayout.VerticalScope(ReportSectionStyle()))
            {
                EditorGUILayout.LabelField(L("import.auditNotices"), ReportHeaderStyle());
                foreach (var item in uncertainties)
                    DrawImportAuditNotice(ImportAuditCategory(item?.text), item?.text, item?.source, MessageType.Warning);
                foreach (var item in ambiguities)
                    DrawImportAuditNotice(
                        L("import.uncertainty"),
                        $"[[{item.target}]] → {string.Join("、", item.matches ?? Array.Empty<string>())}",
                        item.source,
                        MessageType.Warning);
                foreach (var item in preserved)
                    DrawImportAuditNotice(ImportAuditCategory(item?.text), item?.text, item?.source, MessageType.Info);
                foreach (var item in mixed)
                    DrawImportAuditNotice(ImportAuditCategory(item?.text), item?.text, item?.source, MessageType.Warning);
            }
        }

        private static int ImportAuditNoticeCount(DesignImportRun run) =>
            (run?.uncertaintyCandidates?.Length ?? 0) +
            (run?.ambiguousLinks?.Length ?? 0) +
            (run?.typeFilterAudit?.preservedConstraints?.Length ?? 0) +
            (run?.typeFilterAudit?.mixedDescriptions?.Length ?? 0);

        private static string ImportAuditCategory(string description)
        {
            var value = description ?? string.Empty;
            if (Regex.IsMatch(value, "UI|表现|视觉|图示|动画|音效|美术", RegexOptions.IgnoreCase))
                return L("import.presentationMissing");
            if (Regex.IsMatch(value, "待定|暂未|尚未|未定义|未细化|缺失|待补", RegexOptions.IgnoreCase))
                return L("import.missingDescription");
            return L("import.uncertainty");
        }

        private void DrawImportAuditNotice(
            string category,
            string description,
            string sourceReference,
            MessageType messageType)
        {
            TryResolveDesignSourceReference(sourceReference, out var path, out var line);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var icon = messageType == MessageType.Info ? "ℹ" : "⚠";
                EditorGUILayout.LabelField(
                    $"{icon} {category}：{StripMarkdownMarkers(Or(description, L("common.noDetails")))}",
                    ReportMiniWrapStyle(),
                    GUILayout.ExpandWidth(true));
                GUI.enabled = !string.IsNullOrWhiteSpace(path);
                if (GUILayout.Button(
                        new GUIContent(L("import.jumpToSource"), sourceReference),
                        ReportActionButtonStyle(),
                        GUILayout.Width(72),
                        GUILayout.Height(24)))
                    OpenMarkdownDocument(path, line);
                GUI.enabled = true;
            }
        }

        private void DrawImportSpecDetail(DesignImportSpec spec)
        {
            if (spec == null)
                return;

            using (new EditorGUILayout.VerticalScope(ReportSectionStyle()))
            {
                var isRule = string.Equals(spec.Category, "game-rule", StringComparison.OrdinalIgnoreCase);
                var pairedFeature = isRule ? FindPairedDraftSpec(spec, "feature") : null;
                var approvalBlocker = string.Empty;
                var canApprove = !isRule && CanApproveDraft(spec, out approvalBlocker);
                if (isRule)
                    approvalBlocker = pairedFeature == null ? L("import.pairedFeatureMissing") : string.Empty;
                var guidanceKey = ChangeSectionKey("draft", Or(spec.DraftChangePath, spec.Capability), "editor-guidance");
                EditorGUILayout.LabelField(spec.Title, ReportTitleStyle());
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (IsEditorGuidanceCategory(spec.Category) && HasEditorGuidance(spec.EditorGuidance) &&
                        GUILayout.Button(L("guidance.button"), ReportActionButtonStyle(), GUILayout.Width(104), GUILayout.Height(30)))
                        ToggleEditorGuidance(guidanceKey);
                    var notesPath = string.IsNullOrWhiteSpace(spec.DraftChangePath)
                        ? string.Empty
                        : Path.Combine(spec.DraftChangePath, "change-review.json");
                    GUI.enabled = !string.IsNullOrWhiteSpace(notesPath) && File.Exists(notesPath);
                    if (GUILayout.Button(L("change.notes"), ReportActionButtonStyle(), GUILayout.Width(80), GUILayout.Height(30)))
                        ToggleChangeNotes(notesPath, spec.ImplementationNotes);
                    GUI.enabled = true;
                    if (GUILayout.Button(L("common.delete"), ReportActionButtonStyle(), GUILayout.Width(80), GUILayout.Height(30)))
                    {
                        ConfirmDeleteDraftSpec(_selectedDraftGroup, spec);
                        return;
                    }
                    var changeExists = ImportChangeExists(_selectedImportRun, spec);
                    if (isRule)
                    {
                        GUI.enabled = pairedFeature != null;
                        if (GUILayout.Button(
                                L("import.viewPairedFeature"),
                                ReportActionButtonStyle(),
                                GUILayout.Width(144),
                                GUILayout.Height(30)))
                            SelectDraftSpec(pairedFeature);
                        GUI.enabled = true;
                    }
                    else
                    {
                        GUI.enabled = canApprove && !changeExists;
                        if (GUILayout.Button(
                                changeExists ? L("import.transferred") : L("import.approveChange"),
                                ReportActionButtonStyle(),
                                GUILayout.Width(144),
                                GUILayout.Height(30)))
                            AddImportSpecToChanges(_selectedImportRun, spec);
                        GUI.enabled = true;
                    }
                    if (GUILayout.Button(L("import.copyId"), ReportActionButtonStyle(), GUILayout.Width(120), GUILayout.Height(30)))
                    {
                        var changeId = ImportChangeId(_selectedImportRun, spec);
                        EditorGUIUtility.systemCopyBuffer = changeId;
                        ShowNotification(new GUIContent(AgentWorkbenchText.Format("common.copied", changeId)));
                    }
                    if (GUILayout.Button(L("import.openSpec"), ReportActionButtonStyle(), GUILayout.Width(98), GUILayout.Height(30)))
                        OpenPath(spec.SpecPath);
                }

                if (isRule)
                {
                    EditorGUILayout.HelpBox(
                        pairedFeature == null
                            ? L("import.pairedFeatureMissing")
                            : L("import.ruleFollowsFeatureApproval"),
                        pairedFeature == null ? MessageType.Warning : MessageType.Info);
                }
                else if (!canApprove)
                {
                    EditorGUILayout.HelpBox(
                        AgentWorkbenchText.Format("import.approvalBlocked", approvalBlocker),
                        MessageType.Warning);
                }
                else
                {
                    var modifiedEvidenceCount = spec.VerificationEvidence.Count(item =>
                        string.Equals(item?.effectiveStatus, "modified", StringComparison.OrdinalIgnoreCase));
                    EditorGUILayout.HelpBox(
                        modifiedEvidenceCount > 0
                            ? AgentWorkbenchText.Format(
                                "import.approvalReadyWithModifiedEvidence",
                                modifiedEvidenceCount)
                            : L("import.approvalReady"),
                        MessageType.Info);
                }

                DrawTwoColumnFields(
                    new KeyValuePair<string, string>("Capability", spec.Capability),
                    new KeyValuePair<string, string>(L("common.category"), CategoryLabel(spec.Category)),
                    new KeyValuePair<string, string>("Readiness", Or(spec.Readiness, L("common.unassessed"))));

                var detailNotesPath = string.IsNullOrWhiteSpace(spec.DraftChangePath)
                    ? string.Empty
                    : Path.Combine(spec.DraftChangePath, "change-review.json");
                if (IsEditingChangeNotes(detailNotesPath))
                    DrawChangeNotesEditor(detailNotesPath);

                if (_visibleEditorGuidance.Contains(guidanceKey))
                    DrawEditorGuidance(spec.Title, spec.EditorGuidance);
            }

            if (string.Equals(spec.Category, "game-rule", StringComparison.OrdinalIgnoreCase))
            {
                DrawDraftChangeArtifacts(spec);
                DrawRuleSpecLink(spec);
                return;
            }

            DrawCollapsibleChangeSection(
                ChangeSectionKey("draft", Or(spec.DraftChangePath, spec.Capability), "review"),
                L("review.title"),
                () =>
                {
                    if (!DrawStructuredTranslationGate(spec.ReviewPath))
                        return;
                    var verificationStatus = Or(spec.VerificationStatus, L("common.unverified"));
                    var verificationType =
                        verificationStatus.IndexOf("conflict", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        verificationStatus.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0
                            ? MessageType.Warning
                            : MessageType.Info;
                    EditorGUILayout.HelpBox(
                        $"{verificationStatus}\n{Or(LocalizedVerificationSummary(spec.ReviewPath, spec.VerificationSummary), L("common.noVerification"))}",
                        verificationType);
                    DrawEvidenceList(L("spec.evidence"), spec.VerificationEvidence);
                    DrawReviewIssueTable(
                        spec.ReviewIssues,
                        (issue, accepted, note) => UpdateDraftReviewIssueAcceptance(spec, issue, accepted, note));
                });

            DrawCollapsibleChangeSection(
                ChangeSectionKey("draft", Or(spec.DraftChangePath, spec.Capability), "dependencies"),
                $"Dependencies（{spec.Dependencies.Count}）",
                () => DrawDependencyTable(spec.Dependencies));

            DrawDraftChangeArtifacts(spec);

            DrawCollapsibleChangeSection(
                ChangeSectionKey("draft", Or(spec.DraftChangePath, spec.Capability), "spec-content"),
                L("spec.content"),
                () =>
                {
                var width = Mathf.Max(320, position.width - 560);
                DrawEditableMarkdown(
                    L("spec.content"),
                    spec.SpecPath,
                    spec.SpecContent,
                    width);
                });

            var pairedRule = FindPairedDraftSpec(spec, "game-rule");
            if (pairedRule != null)
            {
                DrawCollapsibleChangeSection(
                    ChangeSectionKey("draft", Or(spec.DraftChangePath, spec.Capability), "paired-rule"),
                    L("import.pairedRule"),
                    () =>
                    {
                        if (GUILayout.Button(
                                AgentWorkbenchText.Format("import.openPairedRule", pairedRule.Title),
                                ReportActionButtonStyle(),
                                GUILayout.ExpandWidth(true),
                                GUILayout.Height(30)))
                            SelectDraftSpec(pairedRule);
                    });
            }
        }

        private void UpdateDraftReviewIssueAcceptance(
            DesignImportSpec spec,
            ReviewIssue issue,
            bool accepted,
            string note)
        {
            ApplyReviewIssueAcceptance(issue, accepted, note);

            var centralReviewPath = Or(
                spec.ReviewPath,
                Path.Combine(Path.GetDirectoryName(spec.SpecPath) ?? _draftStorePath, "spec-review.json"));
            if (File.Exists(centralReviewPath))
            {
                var review = JsonUtility.FromJson<ImportSpecReview>(File.ReadAllText(centralReviewPath, Encoding.UTF8)) ?? new ImportSpecReview();
                review.schemaVersion = 5;
                review.reviewIssues = spec.ReviewIssues.ToArray();
                File.WriteAllText(centralReviewPath, JsonUtility.ToJson(review, true), Encoding.UTF8);
            }

            if (!string.IsNullOrWhiteSpace(spec.DraftChangePath))
            {
                var changeReviewPath = Path.Combine(spec.DraftChangePath, "change-review.json");
                if (File.Exists(changeReviewPath))
                {
                    var review = JsonUtility.FromJson<ChangeReview>(File.ReadAllText(changeReviewPath, Encoding.UTF8)) ?? new ChangeReview();
                    review.schemaVersion = 5;
                    review.reviewIssues = (review.reviewIssues ?? Array.Empty<ReviewIssue>())
                        .Concat(spec.ReviewIssues)
                        .Where(item => item != null && !string.IsNullOrWhiteSpace(item.id))
                        .GroupBy(item => item.id, StringComparer.OrdinalIgnoreCase)
                        .Select(item => item.Last())
                        .ToArray();
                    File.WriteAllText(changeReviewPath, JsonUtility.ToJson(review, true), Encoding.UTF8);
                }
                var gapsPath = Path.Combine(spec.DraftChangePath, "gaps.json");
                var gaps = ReadGapArray(gapsPath).ToList();
                var gap = gaps.FirstOrDefault(item => string.Equals(item.id, issue.sourceId, StringComparison.OrdinalIgnoreCase));
                if (gap != null)
                {
                    gap.status = accepted ? "accepted" : "open";
                    gap.acceptedBy = issue.acceptedBy;
                    gap.acceptedAt = issue.acceptedAt;
                    gap.userRationale = issue.acceptanceNote;
                    WriteJsonArray(gapsPath, gaps);
                }
            }
            Repaint();
        }

        private void DrawDependencyTable(IReadOnlyCollection<DependencyEdge> dependencies)
        {
            if (dependencies == null || dependencies.Count == 0)
            {
                EditorGUILayout.LabelField(L("common.none"), ReportMutedStyle());
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(L("dependency.name"), EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
                GUILayout.Label(L("dependency.artifact"), EditorStyles.miniBoldLabel, GUILayout.Width(106));
                GUILayout.Label(L("dependency.status"), EditorStyles.miniBoldLabel, GUILayout.Width(112));
            }
            foreach (var dependency in dependencies)
                DrawDependencyRow(dependency);
        }

        private void DrawDependencyRow(DependencyEdge dependency)
        {
            var name = DependencyDisplayName(dependency.to, _selectedImportRun?.Nodes);
            var formal = _specFiles.FirstOrDefault(item =>
                string.Equals(item.Capability, dependency.to, StringComparison.OrdinalIgnoreCase));
            var artifact = string.Empty;
            var status = string.Empty;
            var color = ThemePrimaryTextColor();
            if (formal != null)
            {
                artifact = L("review.formalSpec");
                status = L("dependency.available");
                color = new Color(0.28f, 0.72f, 0.38f);
            }
            else
            {
                var change = FindChangeByCapability(dependency.to);
                if (change != null)
                {
                    artifact = L("dependency.change");
                    var completed = change.Tasks.Count(item => item.Completed);
                    var applied = IsAppliedChangeProvider(change);
                    status = applied
                        ? L("dependency.applied")
                        : change.Tasks.Count == 0 ? L("dependency.available") : $"{completed}/{change.Tasks.Count}";
                    color = applied ? new Color(0.28f, 0.72f, 0.38f) : new Color(0.35f, 0.65f, 1f);
                }
                else
                {
                    var draft = FindDraftSpec(dependency.to);
                    artifact = draft != null ? "Draft Change" : L("dependency.missing");
                    status = draft != null ? L("review.awaitingApproval") : L("dependency.missing");
                    color = draft != null ? new Color(0.82f, 0.6f, 0.22f) : new Color(0.9f, 0.3f, 0.25f);
                }
            }

            var valueStyle = new GUIStyle(ReportMiniWrapStyle());
            valueStyle.normal.textColor = color;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUI.enabled = CanNavigateToCapability(dependency.to);
                if (GUILayout.Button(new GUIContent(name, dependency.to), DependencyLinkStyle(), GUILayout.ExpandWidth(true)))
                    NavigateToCapability(dependency.to);
                GUI.enabled = true;
                GUILayout.Label(artifact, valueStyle, GUILayout.Width(106));
                GUILayout.Label(status, valueStyle, GUILayout.Width(112));
            }
        }

        private ChangeEntry FindChangeByCapability(string capability) => _openChanges.FirstOrDefault(change =>
            string.Equals(change.Capability, capability, StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(Path.Combine(change.Path, "specs", capability)));

        private static bool IsAppliedChangeProvider(ChangeEntry change)
        {
            if (change == null ||
                !string.Equals(change.ApprovalStatus, "implementation-change", StringComparison.OrdinalIgnoreCase) ||
                change.Tasks.Count == 0 || change.Tasks.Any(item => !item.Completed))
                return false;
            var codeReady = string.Equals(change.CodeReadiness, "ready", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(change.CodeReadiness, "implemented", StringComparison.OrdinalIgnoreCase);
            var verified = string.Equals(change.Verification?.status, "verified", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(change.Verification?.status, "implemented", StringComparison.OrdinalIgnoreCase);
            return codeReady && verified;
        }

        private static bool IsApprovedChangeProvider(ChangeEntry change) =>
            change != null &&
            string.Equals(change.ApprovalStatus, "implementation-change", StringComparison.OrdinalIgnoreCase);

        private DesignImportSpec FindDraftSpec(string capability) => _designImports
            .SelectMany(run => run.Specs ?? new List<DesignImportSpec>())
            .FirstOrDefault(spec => string.Equals(spec.Capability, capability, StringComparison.OrdinalIgnoreCase));

        private bool CanNavigateToCapability(string capability) =>
            _specFiles.Any(spec => string.Equals(spec.Capability, capability, StringComparison.OrdinalIgnoreCase)) ||
            FindChangeByCapability(capability) != null ||
            FindDraftSpec(capability) != null;

        private void NavigateToCapability(string capability)
        {
            var formal = _specFiles.FirstOrDefault(spec =>
                string.Equals(spec.Capability, capability, StringComparison.OrdinalIgnoreCase));
            if (formal != null)
            {
                _tab = ToolTab.OpenSpec;
                _openSpecSection = OpenSpecSection.Specs;
                _specCategoryTab = CategoryTabFor(formal.Category);
                _selectedSpec = formal;
                _specDetailScroll = Vector2.zero;
                return;
            }

            var change = FindChangeByCapability(capability);
            if (change != null)
            {
                _tab = ToolTab.OpenSpec;
                _openSpecSection = OpenSpecSection.Changes;
                _changeCategoryTab = CategoryTabFor(change.Category);
                _selectedChange = change;
                _changeDetailScroll = Vector2.zero;
                return;
            }

            var run = _designImports.FirstOrDefault(item => (item.Specs ?? new List<DesignImportSpec>()).Any(spec =>
                string.Equals(spec.Capability, capability, StringComparison.OrdinalIgnoreCase)));
            var draft = run?.Specs?.FirstOrDefault(spec =>
                string.Equals(spec.Capability, capability, StringComparison.OrdinalIgnoreCase));
            if (draft == null)
                return;
            _tab = ToolTab.ImportReports;
            _selectedImportRun = run;
            _showImportSpecNavigation = true;
            SelectDraftSpec(draft);
        }

        private static SpecCategoryTab CategoryTabFor(string category) => category switch
        {
            "architecture" => SpecCategoryTab.System,
            "system" => SpecCategoryTab.System,
            "feature" => SpecCategoryTab.Feature,
            "game-rule" => SpecCategoryTab.GameRule,
            _ => SpecCategoryTab.All
        };

        private static GUIStyle DependencyLinkStyle()
        {
            var style = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                stretchWidth = true
            };
            style.normal.textColor = new Color(0.3f, 0.65f, 1f);
            return style;
        }

        private void DrawDraftChangeArtifacts(DesignImportSpec spec)
        {
            var identity = Or(spec.DraftChangePath, spec.Capability);
            if (string.Equals(spec.Category, "game-rule", StringComparison.OrdinalIgnoreCase))
            {
                DrawCollapsibleChangeSection(
                    ChangeSectionKey("draft", identity, "design"),
                    "Design",
                    () => DrawRuleDesignSources(spec, Mathf.Max(320, position.width - 560)));
                return;
            }

            if (!string.IsNullOrWhiteSpace(spec.ProposalContent))
            {
                DrawCollapsibleChangeSection(
                    ChangeSectionKey("draft", identity, "proposal"),
                    "Proposal",
                    () =>
                    {
                        var width = Mathf.Max(320, position.width - 560);
                        DrawEditableMarkdown(
                            "Proposal",
                            DraftArtifactPath(spec, "proposal.md"),
                            spec.ProposalContent,
                            width);
                    });
            }
            if (!string.IsNullOrWhiteSpace(spec.DesignContent))
            {
                DrawCollapsibleChangeSection(
                    ChangeSectionKey("draft", identity, "design"),
                    "Design",
                    () =>
                    {
                        var width = Mathf.Max(320, position.width - 560);
                        DrawEditableMarkdown(
                            "Design",
                            DraftArtifactPath(spec, "design.md"),
                            spec.DesignContent,
                            width);
                    });
            }
            DrawTaskProgress(
                ChangeSectionKey("draft", identity, "tasks"),
                spec.Tasks,
                DraftArtifactPath(spec, "tasks.md"),
                spec.TasksContent);
        }

        private void DrawRuleSpecLink(DesignImportSpec ruleSpec)
        {
            DrawCollapsibleChangeSection(
                ChangeSectionKey("draft", Or(ruleSpec.DraftChangePath, ruleSpec.Capability), "spec-content"),
                L("spec.content"),
                () =>
                {
                    var pairedFeature = FindPairedDraftSpec(ruleSpec, "feature");
                    if (pairedFeature == null)
                    {
                        EditorGUILayout.HelpBox(L("import.pairedFeatureMissing"), MessageType.Warning);
                        return;
                    }
                    EditorGUILayout.HelpBox(L("import.ruleSpecSummary"), MessageType.Info);
                    if (GUILayout.Button(
                            AgentWorkbenchText.Format("import.openPairedFeature", pairedFeature.Title),
                            ReportActionButtonStyle(),
                            GUILayout.ExpandWidth(true),
                            GUILayout.Height(30)))
                        SelectDraftSpec(pairedFeature);
                    EditorGUILayout.Space(6);
                    DrawEditableMarkdown(
                        L("spec.content"),
                        ruleSpec.SpecPath,
                        ruleSpec.SpecContent,
                        Mathf.Max(320, position.width - 560));
                });
        }

        private DesignImportSpec FindPairedDraftSpec(DesignImportSpec spec, string category)
        {
            if (spec == null || _selectedImportRun?.Specs == null)
                return null;
            var explicitCapability = category == "feature"
                ? spec.PairedFeatureCapability
                : spec.PairedRuleCapability;
            var candidates = _selectedImportRun.Specs.Where(item =>
                item != null &&
                string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.DraftChangePath, spec.DraftChangePath, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(explicitCapability))
            {
                var explicitMatch = candidates.FirstOrDefault(item => string.Equals(
                    item.Capability,
                    explicitCapability,
                    StringComparison.OrdinalIgnoreCase));
                if (explicitMatch != null)
                    return explicitMatch;
            }
            return candidates.FirstOrDefault();
        }

        private void DrawRuleDesignSources(DesignImportSpec spec, float width)
        {
            EditorGUILayout.LabelField("来源文档", MarkdownHeadingStyle(2));
            var paths = ResolveDesignSourcePaths(spec.SourceReferences).ToList();
            if (paths.Count == 0)
            {
                foreach (var reference in spec.SourceReferences)
                    EditorGUILayout.LabelField($"• {reference}", ReportMutedStyle());
                if (spec.SourceReferences.Count == 0)
                    EditorGUILayout.LabelField(L("common.none"), ReportMutedStyle());
                return;
            }
            foreach (var sourcePath in paths)
            {
                var label = Path.GetFileNameWithoutExtension(sourcePath);
                if (GUILayout.Button(
                        new GUIContent($"↗ {label}", sourcePath),
                        ReportActionButtonStyle(),
                        GUILayout.MaxWidth(Mathf.Max(180, width)),
                        GUILayout.Height(28)))
                    OpenMarkdownDocument(sourcePath);
            }
        }

        private void DrawMarkdownEditButton(string label, string path, string content)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(
                        L("spec.rawText"),
                        ReportActionButtonStyle(),
                        GUILayout.Width(112),
                        GUILayout.Height(28)))
                    BeginMarkdownEdit(label, path, content);
            }
        }

        private static string DraftArtifactPath(DesignImportSpec spec, string fileName)
        {
            if (spec == null || string.IsNullOrWhiteSpace(spec.DraftChangePath))
                return null;
            return Path.Combine(spec.DraftChangePath, fileName);
        }

        private void DrawMarkdown(string markdown, float width, string sourceDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                EditorGUILayout.HelpBox(L("spec.unreadable"), MessageType.Warning);
                return;
            }

            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var inCodeBlock = false;
            var code = new StringBuilder();
            var currentSection = string.Empty;
            EditorGUILayout.VerticalScope requirementScope = null;
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                var trimmed = line.Trim();

                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    if (inCodeBlock)
                    {
                        DrawCodeBlock(code.ToString(), width);
                        code.Clear();
                    }
                    inCodeBlock = !inCodeBlock;
                    continue;
                }

                if (inCodeBlock)
                {
                    code.AppendLine(line);
                    continue;
                }

                var imageMatch = Regex.Match(trimmed, @"^!\[(?<alt>[^\]]*)\]\((?<path>[^)]+)\)$");
                if (imageMatch.Success)
                {
                    DrawMarkdownImage(
                        imageMatch.Groups["path"].Value.Trim().Trim('<', '>'),
                        imageMatch.Groups["alt"].Value.Trim(),
                        width,
                        sourceDirectory);
                    continue;
                }

                if (trimmed.StartsWith("|", StringComparison.Ordinal))
                {
                    var tableLines = new List<string>();
                    while (index < lines.Length &&
                           lines[index].Trim().StartsWith("|", StringComparison.Ordinal))
                    {
                        tableLines.Add(lines[index].Trim());
                        index++;
                    }
                    index--;
                    DrawMarkdownTable(tableLines, width);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    GUILayout.Space(5);
                    continue;
                }

                var headingLevel = MarkdownHeadingLevel(trimmed);
                if (headingLevel > 0)
                {
                    var heading = trimmed.Substring(headingLevel).Trim();
                    if (headingLevel == 2)
                        currentSection = heading;
                    if (headingLevel <= 3 && requirementScope != null)
                    {
                        requirementScope.Dispose();
                        requirementScope = null;
                    }
                    if (headingLevel == 3 &&
                        heading.StartsWith("Requirement:", StringComparison.OrdinalIgnoreCase))
                    {
                        GUILayout.Space(4);
                        requirementScope = new EditorGUILayout.VerticalScope(MarkdownRequirementStyle());
                    }
                    var style = MarkdownHeadingStyle(headingLevel);
                    DrawRichLabel(heading, style, width);
                    continue;
                }

                if (trimmed == "---" || trimmed == "***")
                {
                    var rect = GUILayoutUtility.GetRect(width, 8);
                    if (Event.current.type == EventType.Repaint)
                    {
                        EditorGUI.DrawRect(
                            new Rect(rect.x, rect.y + 3, rect.width, 1),
                            new Color(0.38f, 0.38f, 0.38f, 0.65f));
                    }
                    continue;
                }

                if (trimmed.StartsWith(">", StringComparison.Ordinal))
                {
                    var quote = trimmed.TrimStart('>', ' ');
                    if (quote.Contains("[!SPEC-GAP]"))
                    {
                        var gapLines = new List<string> { quote };
                        var cursor = index + 1;
                        while (cursor < lines.Length && lines[cursor].Trim().StartsWith(">", StringComparison.Ordinal))
                        {
                            var nextQuote = lines[cursor].Trim().TrimStart('>', ' ');
                            if (nextQuote.Contains("[!SPEC-GAP]"))
                                break;
                            gapLines.Add(nextQuote);
                            cursor++;
                        }
                        DrawSpecGapQuote(gapLines, width);
                        index = cursor - 1;
                        continue;
                    }
                    var type = quote.Contains("[!SPEC-GAP]") ? MessageType.Warning : MessageType.Info;
                    EditorGUILayout.HelpBox(StripMarkdownMarkers(quote), type);
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^[-*+]\s+"))
                {
                    var body = Regex.Replace(trimmed, @"^[-*+]\s+", string.Empty);
                    if (currentSection.Equals("Purpose", StringComparison.OrdinalIgnoreCase))
                        body = RenderReadableCapabilityReferences(body);
                    DrawRichLabel(
                        "• " + body,
                        MarkdownBodyStyle(18),
                        width - 18);
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^\d+\.\s+"))
                {
                    DrawRichLabel(trimmed, MarkdownBodyStyle(18), width - 18);
                    continue;
                }

                var renderedBody = currentSection.Equals("Purpose", StringComparison.OrdinalIgnoreCase)
                    ? RenderReadableCapabilityReferences(trimmed)
                    : trimmed;
                DrawRichLabel(renderedBody, MarkdownBodyStyle(), width);
            }

            if (inCodeBlock && code.Length > 0)
                DrawCodeBlock(code.ToString(), width);
            requirementScope?.Dispose();
        }

        private void DrawMarkdownImage(string relativePath, string altText, float width, string sourceDirectory)
        {
            var path = ResolveMarkdownImagePath(relativePath, sourceDirectory);
            if (string.IsNullOrWhiteSpace(path))
            {
                EditorGUILayout.HelpBox(
                    $"{Or(altText, "Image")}: {relativePath}",
                    MessageType.Warning);
                return;
            }

            if (!_markdownImageCache.TryGetValue(path, out var texture) || texture == null)
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    name = $"zWorkFlow Markdown: {Path.GetFileName(path)}"
                };
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    DestroyImmediate(texture);
                    EditorGUILayout.HelpBox($"{Or(altText, "Image")}: {relativePath}", MessageType.Warning);
                    return;
                }
                _markdownImageCache[path] = texture;
            }

            var drawWidth = Mathf.Min(width, texture.width);
            var drawHeight = drawWidth * texture.height / Mathf.Max(1f, texture.width);
            var rect = GUILayoutUtility.GetRect(drawWidth, drawHeight, GUILayout.MaxWidth(drawWidth));
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            GUILayout.Space(5f);
        }

        private string ResolveMarkdownImagePath(string relativePath, string sourceDirectory)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(_projectRoot))
                return null;

            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var candidates = new List<string>();
            if (Path.IsPathRooted(normalized))
                candidates.Add(normalized);
            else
            {
                if (!string.IsNullOrWhiteSpace(sourceDirectory))
                    candidates.Add(Path.Combine(sourceDirectory, normalized));
                candidates.Add(Path.Combine(_projectRoot, normalized));
                candidates.Add(Path.Combine(_projectRoot, "zWorkFlow", normalized));
            }

            var root = Path.GetFullPath(_projectRoot).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            var comparison = Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            foreach (var candidate in candidates)
            {
                try
                {
                    var fullPath = Path.GetFullPath(candidate);
                    if (fullPath.StartsWith(root, comparison) && File.Exists(fullPath))
                        return fullPath;
                }
                catch (Exception)
                {
                    // Invalid Markdown paths are rendered as a warning instead of breaking the Workbench.
                }
            }
            return null;
        }

        private void DisposeMarkdownImages()
        {
            foreach (var texture in _markdownImageCache.Values)
            {
                if (texture != null)
                    DestroyImmediate(texture);
            }
            _markdownImageCache.Clear();
        }

        private void DrawSpecGapQuote(IReadOnlyList<string> lines, float width)
        {
            var header = lines.FirstOrDefault() ?? string.Empty;
            var missing = QuoteField(lines, "缺失依赖：", "Missing dependency:");
            var edge = QuoteField(lines, "依赖边：", "Dependency edge:");
            var expectedCategory = QuoteField(lines, "期望分类：", "Expected category:");
            ParseDependencyEdge(edge, out var from, out var relation, out var to);
            var missingName = ReadableCapabilityName(to);
            var category = CategoryLabel(expectedCategory);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MaxWidth(width)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"⚠ {Or(missingName, L("gap.missingDependency"))}",
                        ReportWarningStyle(),
                        GUILayout.MinWidth(120));
                    GUILayout.Label(category, CategoryStyle(expectedCategory), GUILayout.Width(92));
                    GUILayout.FlexibleSpace();
                    var status = Regex.Replace(header, @"^\[!SPEC-GAP\]\s*", string.Empty);
                    if (!string.IsNullOrWhiteSpace(status))
                        GUILayout.Label(status, ReportMutedStyle());
                }
                if (!string.IsNullOrWhiteSpace(missing))
                    EditorGUILayout.LabelField(StripMarkdownMarkers(missing), ReportMiniWrapStyle());
                if (!string.IsNullOrWhiteSpace(edge))
                {
                    var readableEdge = string.IsNullOrWhiteSpace(to)
                        ? edge
                        : $"{ReadableCapabilityName(from)}  →  {DependencyRelationLabel(relation)}  →  {ReadableCapabilityName(to)}";
                    EditorGUILayout.LabelField(readableEdge, ReportMutedStyle());
                }
            }
        }

        private static string QuoteField(IEnumerable<string> lines, params string[] prefixes)
        {
            foreach (var line in lines ?? Array.Empty<string>())
            foreach (var prefix in prefixes)
            {
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return line.Substring(prefix.Length).Trim();
            }
            return string.Empty;
        }

        private static void ParseDependencyEdge(string edge, out string from, out string relation, out string to)
        {
            from = relation = to = string.Empty;
            var match = Regex.Match(
                edge ?? string.Empty,
                @"^(?<from>.+?)--(?<relation>requires|integrates-with|extends|depends-on|uses)--(?<to>.+)$",
                RegexOptions.IgnoreCase);
            if (!match.Success)
                return;
            from = match.Groups["from"].Value.Trim();
            relation = match.Groups["relation"].Value.Trim();
            to = match.Groups["to"].Value.Trim();
        }

        private static string DependencyRelationLabel(string relation) => relation?.ToLowerInvariant() switch
        {
            "requires" => "依赖",
            "integrates-with" => "接入",
            "extends" => "扩展",
            "uses" => "使用",
            "depends-on" => "依赖",
            _ => Or(relation, "关联")
        };

        private string ReadableCapabilityName(string capability)
        {
            if (string.IsNullOrWhiteSpace(capability))
                return string.Empty;
            var name = DependencyDisplayName(capability);
            if (!string.Equals(name, capability, StringComparison.OrdinalIgnoreCase))
                return name;
            var selectedNode = _selectedImportRun?.Nodes?.FirstOrDefault(item =>
                string.Equals(item.id, capability, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(selectedNode?.label))
                return selectedNode.label;
            var draft = _draftSpecGroups.FirstOrDefault(item =>
                string.Equals(item.capability, capability, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(draft?.Title))
                return draft.Title;
            var change = _openChanges.FirstOrDefault(item =>
                string.Equals(item.Capability, capability, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(change?.Title))
                return change.Title;
            return Regex.Replace(capability, "[-_]", " ");
        }

        private string RenderReadableCapabilityReferences(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;
            var ids = _dependencyNodes.Select(item => item.id)
                .Concat(_selectedImportRun?.Nodes?.Select(item => item.id) ?? Array.Empty<string>())
                .Concat(_specFiles.Select(item => item.Capability))
                .Concat(_draftSpecGroups.Select(item => item.capability))
                .Concat(_openChanges.Select(item => item.Capability))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(item => item.Length);
            var result = text;
            foreach (var id in ids)
            {
                var name = ReadableCapabilityName(id);
                if (string.IsNullOrWhiteSpace(name) || string.Equals(name, id, StringComparison.OrdinalIgnoreCase))
                    continue;
                result = Regex.Replace(result, $"`{Regex.Escape(id)}`", name, RegexOptions.IgnoreCase);
                result = Regex.Replace(
                    result,
                    $@"(?<![A-Za-z0-9_-]){Regex.Escape(id)}(?![A-Za-z0-9_-])",
                    name,
                    RegexOptions.IgnoreCase);
            }
            return result;
        }

        private static int MarkdownHeadingLevel(string line)
        {
            var count = 0;
            while (count < line.Length && count < 6 && line[count] == '#')
                count++;
            return count > 0 && count < line.Length && line[count] == ' ' ? count : 0;
        }

        private static GUIStyle MarkdownHeadingStyle(int level)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                wordWrap = true,
                richText = true,
                fontSize = level switch
                {
                    1 => 20,
                    2 => 17,
                    3 => 14,
                    _ => 12
                },
                margin = new RectOffset(0, 0, level <= 2 ? 10 : 6, 3)
            };
            style.normal.textColor = AgentWorkbenchTheme.HeadingColor(level, AgentWorkbenchTheme.IsDarkMode);
            return style;
        }

        private static GUIStyle MarkdownRequirementStyle()
        {
            var style = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 8, 10),
                margin = new RectOffset(0, 0, 4, 8)
            };
            return style;
        }

        private static GUIStyle MarkdownBodyStyle(int leftPadding = 0)
        {
            var style = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                richText = true,
                padding = new RectOffset(leftPadding, 4, 2, 2)
            };
            style.normal.textColor = AgentWorkbenchTheme.MarkdownBodyColor();
            return style;
        }

        private static void DrawRichLabel(string text, GUIStyle style, float width)
        {
            var rendered = RenderInlineMarkdown(text);
            var height = Mathf.Max(
                EditorGUIUtility.singleLineHeight,
                style.CalcHeight(new GUIContent(rendered), Mathf.Max(120, width)));
            EditorGUILayout.LabelField(
                rendered,
                style,
                GUILayout.MinHeight(height),
                GUILayout.MaxWidth(width));
        }

        private static string RenderInlineMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var result = Regex.Replace(text, @"\*\*(.+?)\*\*", "<b>$1</b>");
            result = Regex.Replace(result, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "<i>$1</i>");
            result = Regex.Replace(result, @"`([^`]+)`", "<color=#8FC7FF>$1</color>");
            return result;
        }

        private static string StripMarkdownMarkers(string text)
        {
            return Regex.Replace(
                Regex.Replace(text ?? string.Empty, @"\*\*(.+?)\*\*", "$1"),
                @"`([^`]+)`",
                "$1");
        }

        private static void DrawCodeBlock(string content, float width)
        {
            var style = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                padding = new RectOffset(8, 8, 6, 6)
            };
            var height = Mathf.Clamp(
                style.CalcHeight(new GUIContent(content), Mathf.Max(160, width)),
                44,
                360);
            EditorGUILayout.SelectableLabel(
                content.TrimEnd(),
                style,
                GUILayout.MinHeight(height),
                GUILayout.MaxWidth(width));
        }

        private static void DrawMarkdownTable(IReadOnlyList<string> lines, float width)
        {
            if (lines == null || lines.Count == 0)
                return;
            var rows = lines
                .Select(line => line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray())
                .Where(cells => !cells.All(cell => Regex.IsMatch(cell, @"^:?-{3,}:?$")))
                .ToList();
            if (rows.Count == 0)
                return;

            var columns = rows.Max(row => row.Length);
            var cellWidth = Mathf.Max(40, (width - (columns - 1) * 4) / columns);
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var style = MarkdownBodyStyle();
                if (rowIndex == 0)
                    style.fontStyle = FontStyle.Bold;
                var rowHeight = rows[rowIndex]
                    .Select(cell => style.CalcHeight(
                        new GUIContent(RenderInlineMarkdown(cell)),
                        cellWidth))
                    .DefaultIfEmpty(EditorGUIUtility.singleLineHeight)
                    .Max() + 6;
                using (new EditorGUILayout.HorizontalScope("box", GUILayout.MinHeight(rowHeight)))
                {
                    foreach (var cell in rows[rowIndex])
                    {
                        EditorGUILayout.LabelField(
                            RenderInlineMarkdown(cell),
                            style,
                            GUILayout.Width(cellWidth),
                            GUILayout.MinHeight(rowHeight - 4));
                    }
                }
            }
        }

        private static void DrawStringList(string label, IReadOnlyCollection<string> values)
        {
            if (values == null || values.Count == 0)
                return;
            EditorGUILayout.LabelField(label, ReportMiniHeaderStyle());
            foreach (var value in values)
                EditorGUILayout.LabelField($"• {value}", ReportMiniWrapStyle());
        }

        private void DrawEvidenceList(string label, IReadOnlyCollection<CodeEvidence> values)
        {
            if (values == null || values.Count == 0)
                return;

            EditorGUILayout.LabelField(label, ReportMiniHeaderStyle());
            foreach (var value in values)
            {
                if (!TryResolveEvidence(value, out var assetPath))
                {
                    EditorGUILayout.LabelField($"• {EvidenceDisplayLabel(value, null)}", ReportMiniWrapStyle());
                    continue;
                }

                var style = EvidenceLinkStyle();
                var content = new GUIContent(
                    $"• {EvidenceDisplayLabel(value, assetPath)}",
                    $"{L("evidence.tooltip")}\nGUID: {value.guid}\n{assetPath}");
                var height = Mathf.Max(
                    EditorGUIUtility.singleLineHeight,
                    style.CalcHeight(content, Mathf.Max(180, position.width - 410)));
                if (GUILayout.Button(content, style, GUILayout.Height(height), GUILayout.ExpandWidth(true)))
                    HighlightEvidence(value, assetPath);
            }
        }

        private IReadOnlyCollection<CodeEvidence> BuildCodeEvidence(
            ImportSpecVerification verification,
            IReadOnlyCollection<string> sourceReferences = null,
            string fallbackFeature = null)
        {
            if (verification == null)
                return Array.Empty<CodeEvidence>();

            var result = new List<CodeEvidence>();
            if (verification.codeEvidence != null)
            {
                foreach (var item in verification.codeEvidence.Where(item => item != null))
                {
                    if (string.IsNullOrWhiteSpace(item.feature))
                        item.feature = fallbackFeature;
                    RefreshEvidenceIntegrity(item);
                    result.Add(item);
                }
            }

            var referenceLines = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in sourceReferences ?? Array.Empty<string>())
            {
                if (!TryCreateLegacyCodeEvidence(reference, out var parsedReference) || parsedReference.line <= 0)
                    continue;
                referenceLines[EvidenceIdentity(parsedReference)] = parsedReference.line;
            }

            foreach (var legacyValue in verification.evidence ?? Array.Empty<string>())
            {
                if (!TryCreateLegacyCodeEvidence(legacyValue, out var evidence))
                    evidence = new CodeEvidence { displayPath = legacyValue, feature = fallbackFeature };
                if (string.IsNullOrWhiteSpace(evidence.feature))
                    evidence.feature = fallbackFeature;
                if (evidence.line <= 0 && referenceLines.TryGetValue(EvidenceIdentity(evidence), out var line))
                    evidence.line = line;
                if (evidence.line <= 0 && !string.IsNullOrWhiteSpace(evidence.guid))
                    evidence.line = 1;
                RefreshEvidenceIntegrity(evidence);
                var existing = result.FirstOrDefault(item => string.Equals(
                    EvidenceIdentity(item),
                    EvidenceIdentity(evidence),
                    StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    if (existing.line <= 0)
                        existing.line = evidence.line;
                    if (string.IsNullOrWhiteSpace(existing.displayPath))
                        existing.displayPath = evidence.displayPath;
                    if (string.IsNullOrWhiteSpace(existing.feature))
                        existing.feature = evidence.feature;
                    continue;
                }
                result.Add(evidence);
            }

            return result
                .Where(item => item != null &&
                               (!string.IsNullOrWhiteSpace(item.guid) || !string.IsNullOrWhiteSpace(item.displayPath)))
                .GroupBy(EvidenceItemIdentity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.line).First())
                .ToArray();
        }

        private bool TryCreateLegacyCodeEvidence(string evidence, out CodeEvidence result)
        {
            result = null;
            if (!TryResolveLegacyEvidencePath(evidence, out var assetPath))
                return false;

            result = new CodeEvidence
            {
                guid = AssetDatabase.AssetPathToGUID(assetPath),
                displayPath = assetPath,
                line = ExtractEvidenceLine(evidence)
            };
            return true;
        }

        private bool TryResolveLegacyEvidencePath(string evidence, out string assetPath)
        {
            assetPath = null;
            if (string.IsNullOrWhiteSpace(evidence))
                return false;

            var matches = Regex.Matches(
                evidence,
                @"(?<path>(?:[A-Za-z]:)?[\\/\p{L}\p{N}_ .-]+\.(?:cs|asmdef|json|md|uxml|uss|shader))",
                RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var candidate = match.Groups["path"].Value.Trim().Trim('`', '"', '\'', '(', ')', '[', ']');
                foreach (var fullPath in EvidencePathCandidates(candidate))
                {
                    if (!File.Exists(fullPath))
                        continue;
                    var relative = RelativeTo(_projectRoot, fullPath);
                    if (relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                        continue;
                    assetPath = relative;
                    return true;
                }
            }
            return false;
        }

        private static int ExtractEvidenceLine(string evidence)
        {
            if (string.IsNullOrWhiteSpace(evidence))
                return 0;
            var match = Regex.Match(evidence, @"(?::|#L)(?<line>\d+)\s*$", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups["line"].Value, out var line) ? line : 0;
        }

        private static string EvidenceIdentity(CodeEvidence evidence)
        {
            if (evidence == null)
                return string.Empty;
            return !string.IsNullOrWhiteSpace(evidence.guid)
                ? evidence.guid
                : Or(evidence.displayPath, string.Empty).Replace('\\', '/');
        }

        private static string EvidenceItemIdentity(CodeEvidence evidence) =>
            $"{EvidenceIdentity(evidence)}:{evidence?.line ?? 0}:{Or(evidence?.feature, string.Empty)}";

        private static string EvidenceDisplayLabel(CodeEvidence evidence, string resolvedAssetPath)
        {
            var path = Or(resolvedAssetPath, Or(evidence?.displayPath, L("evidence.missingPath")));
            var location = evidence?.line > 0 ? $"{path}:{evidence.line}" : path;
            var description = string.IsNullOrWhiteSpace(evidence?.feature)
                ? location
                : $"{location}   -- {evidence.feature}";
            var status = EvidenceStatusLabel(evidence?.effectiveStatus);
            return string.IsNullOrWhiteSpace(status) ? description : $"{description}   [{status}]";
        }

        private void RefreshEvidenceIntegrity(CodeEvidence evidence)
        {
            if (evidence == null)
                return;

            evidence.currentFileHash = string.Empty;
            if (!TryResolveEvidence(evidence, out var assetPath))
            {
                evidence.effectiveStatus = "missing";
                return;
            }

            var fullPath = Path.Combine(_projectRoot, assetPath);
            if (!File.Exists(fullPath))
            {
                evidence.effectiveStatus = "missing";
                return;
            }

            using (var stream = File.OpenRead(fullPath))
            using (var sha = SHA256.Create())
            {
                evidence.currentFileHash = string.Concat(
                    sha.ComputeHash(stream).Select(value => value.ToString("x2")));
            }

            if (string.Equals(evidence.status, "invalid", StringComparison.OrdinalIgnoreCase))
                evidence.effectiveStatus = "invalid";
            else if (string.IsNullOrWhiteSpace(evidence.fileHash))
                evidence.effectiveStatus = "unverified";
            else if (!string.Equals(
                         evidence.fileHash,
                         evidence.currentFileHash,
                         StringComparison.OrdinalIgnoreCase))
                evidence.effectiveStatus = "modified";
            else
                evidence.effectiveStatus = "verified";
        }

        private static string EvidenceStatusLabel(string status)
        {
            switch (Or(status, string.Empty).ToLowerInvariant())
            {
                case "verified": return L("evidence.verified");
                case "modified": return L("evidence.modified");
                case "missing": return L("evidence.missing");
                case "invalid": return L("evidence.invalid");
                case "unverified": return L("evidence.unverified");
                default: return string.Empty;
            }
        }

        private static bool TryResolveEvidence(CodeEvidence evidence, out string assetPath)
        {
            assetPath = null;
            if (evidence == null || string.IsNullOrWhiteSpace(evidence.guid))
                return false;
            assetPath = AssetDatabase.GUIDToAssetPath(evidence.guid);
            return !string.IsNullOrWhiteSpace(assetPath);
        }

        private IEnumerable<string> EvidencePathCandidates(string candidate)
        {
            var normalized = candidate.Replace('/', Path.DirectorySeparatorChar);
            var variants = new List<string> { normalized };
            for (var index = 0; index < normalized.Length - 1; index++)
            {
                if (char.IsWhiteSpace(normalized[index]))
                    variants.Add(normalized.Substring(index + 1).TrimStart());
            }

            foreach (var variant in variants.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
            {
                if (Path.IsPathRooted(variant))
                {
                    yield return variant;
                    continue;
                }

                yield return Path.Combine(_projectRoot, variant);
                if (!variant.StartsWith("Assets" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    yield return Path.Combine(_projectRoot, "Assets", variant);
            }
        }

        private static GUIStyle EvidenceLinkStyle()
        {
            return new GUIStyle(EditorStyles.linkLabel)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                padding = new RectOffset(0, 2, 1, 1),
                margin = new RectOffset(0, 0, 1, 1)
            };
        }

        private static void HighlightEvidence(CodeEvidence evidence, string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
                return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            if (evidence.line > 0)
                AssetDatabase.OpenAsset(asset, evidence.line);
        }

        private void DrawReadableGap(SpecGap gap)
        {
            var title = Or(gap.summary, GapTypeLabel(gap.type, gap.expectedCategory));
            var status = GapStatusLabel(gap.status);
            var detail = Or(gap.implementationImpact, Or(gap.impact, gap.recommendation));
            if (string.Equals(gap.type, "missing-dependency", StringComparison.OrdinalIgnoreCase))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            $"⚠ {ReadableCapabilityName(Or(gap.missingNodeId, gap.requirement))}",
                            ReportWarningStyle());
                        GUILayout.Label(
                            CategoryLabel(gap.expectedCategory),
                            CategoryStyle(gap.expectedCategory),
                            GUILayout.Width(92));
                    }
                    EditorGUILayout.LabelField($"{status} · {title}", ReportMiniWrapStyle());
                    if (!string.IsNullOrWhiteSpace(detail))
                        EditorGUILayout.LabelField($"{L("gap.impact")}：{detail}", ReportMutedStyle());
                }
                return;
            }
            var message = string.IsNullOrWhiteSpace(detail)
                ? $"{status} · {title}"
                : $"{status} · {title}\n{L("gap.impact")}：{detail}";
            EditorGUILayout.HelpBox(
                message,
                gap.blocksImplementation ? MessageType.Error : MessageType.Warning);
        }

        private static string GapStatusLabel(string status)
        {
            return status?.ToLowerInvariant() switch
            {
                "resolved" => L("gap.resolved"),
                "accepted" => L("gap.accepted"),
                "open" => L("gap.open"),
                _ => L("gap.confirm")
            };
        }

        private static string GapTypeLabel(string type, string expectedCategory)
        {
            if (string.Equals(type, "requirement-ambiguity", StringComparison.OrdinalIgnoreCase))
                return L("gap.ambiguity");
            if (string.Equals(type, "missing-dependency", StringComparison.OrdinalIgnoreCase))
                return $"{L("gap.missingDependency")} · {CategoryLabel(expectedCategory)}";
            return L("gap.unknown");
        }

        private static GUIStyle ReportPanelStyle()
        {
            var style = new GUIStyle(EditorStyles.helpBox)
            {
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(0, 0, 0, 0)
            };
            style.normal.background = AgentWorkbenchTheme.IsDarkMode
                ? AgentWorkbenchTheme.DarkPanelTexture
                : AgentWorkbenchTheme.LightPanelTexture;
            return style;
        }

        private static GUIStyle ReportSectionStyle()
        {
            var style = new GUIStyle(EditorStyles.helpBox)
            {
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(10, 10, 8, 10),
                margin = new RectOffset(0, 0, 0, 8)
            };
            style.normal.background = AgentWorkbenchTheme.IsDarkMode
                ? AgentWorkbenchTheme.DarkSectionTexture
                : AgentWorkbenchTheme.LightSectionTexture;
            return style;
        }

        private static GUIStyle ReportHeaderStyle()
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                wordWrap = true,
                fontSize = 12,
                margin = new RectOffset(0, 0, 2, 6)
            };
            style.normal.textColor = ThemePrimaryTextColor();
            return style;
        }

        private static GUIStyle ReportTitleStyle()
        {
            var style = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                margin = new RectOffset(0, 8, 3, 5)
            };
            style.normal.textColor = ThemePrimaryTextColor();
            return style;
        }

        private static GUIStyle ChangeSectionTitleStyle()
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 14,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 4, 0, 0)
            };
            style.normal.textColor = ThemePrimaryTextColor();
            return style;
        }

        private static GUIStyle ReportMutedStyle()
        {
            var style = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                margin = new RectOffset(0, 0, 4, 4)
            };
            style.normal.textColor = AgentWorkbenchTheme.IsDarkMode
                ? new Color(0.72f, 0.72f, 0.72f)
                : new Color(0.34f, 0.34f, 0.34f);
            return style;
        }

        private static GUIStyle ReportMiniHeaderStyle()
        {
            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                wordWrap = true,
                margin = new RectOffset(0, 0, 5, 3)
            };
            style.normal.textColor = ThemePrimaryTextColor();
            return style;
        }

        private static GUIStyle ReportWarningStyle()
        {
            var style = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontStyle = FontStyle.Bold,
                margin = new RectOffset(0, 0, 4, 4)
            };
            style.normal.textColor = ThemePrimaryTextColor();
            return style;
        }

        private static GUIStyle ReportMiniWrapStyle()
        {
            var style = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                wordWrap = true,
                margin = new RectOffset(0, 0, 2, 3)
            };
            style.normal.textColor = AgentWorkbenchTheme.IsDarkMode
                ? new Color(0.78f, 0.81f, 0.86f)
                : new Color(0.25f, 0.28f, 0.33f);
            return style;
        }

        private static Color ThemePrimaryTextColor() => AgentWorkbenchTheme.IsDarkMode
            ? AgentWorkbenchTheme.DarkPrimaryText
            : AgentWorkbenchTheme.LightPrimaryText;

        private static GUIStyle ReportActionButtonStyle()
        {
            return new GUIStyle(EditorStyles.miniButton)
            {
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 5, 5),
                margin = new RectOffset(3, 0, 1, 1),
                fixedWidth = 0,
                fixedHeight = 0,
                stretchWidth = true,
                stretchHeight = true
            };
        }

        private static GUIStyle MainEntryButtonStyle()
        {
            return new GUIStyle(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fixedHeight = MainEntryButtonHeight,
                stretchHeight = false,
                padding = new RectOffset(8, 8, 6, 6)
            };
        }

        private static GUIStyle TabButtonStyle()
        {
            return new GUIStyle(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fixedHeight = TabButtonHeight,
                stretchHeight = false,
                padding = new RectOffset(8, 8, 5, 5)
            };
        }

        private static int DrawUniformTabs(
            int selectedIndex,
            IReadOnlyList<string> labels,
            GUIStyle style,
            float height,
            Action<int> onSelectedClick = null)
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(height)))
            {
                for (var index = 0; index < labels.Count; index++)
                {
                    var selected = index == selectedIndex;
                    var active = GUILayout.Toggle(
                        selected,
                        labels[index],
                        style,
                        GUILayout.Height(height),
                        GUILayout.ExpandWidth(true));
                    if (active && !selected)
                        selectedIndex = index;
                    else if (selected && !active)
                        onSelectedClick?.Invoke(index);
                    if (index + 1 < labels.Count)
                        GUILayout.Space(1f);
                }
            }
            return selectedIndex;
        }

        private static GUIStyle ReportButtonStyle(bool selected)
        {
            var style = new GUIStyle(selected ? EditorStyles.toolbarButton : EditorStyles.miniButton)
            {
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                padding = new RectOffset(10, 8, 6, 6),
                margin = new RectOffset(0, 0, 1, 1),
                fixedWidth = 0,
                fixedHeight = 0,
                stretchWidth = true,
                stretchHeight = true
            };
            return style;
        }

        private static float ReportButtonHeight(string label, GUIStyle style, float width)
        {
            return Mathf.Clamp(style.CalcHeight(new GUIContent(label), width), 44, 96);
        }

        private bool ImportChangeExists(DesignImportRun run, DesignImportSpec spec)
        {
            return run != null && spec != null &&
                   Directory.Exists(Path.Combine(
                       _openSpecPath,
                       "changes",
                       ImportChangeId(run, spec)));
        }

        private bool CanApproveDraft(DesignImportSpec spec, out string blocker)
        {
            blocker = string.Empty;
            if (spec != null && string.Equals(spec.Category, "game-rule", StringComparison.OrdinalIgnoreCase))
            {
                blocker = L("import.ruleFollowsFeatureApproval");
                return false;
            }
            if (spec == null || string.IsNullOrWhiteSpace(spec.DraftChangePath) ||
                !Directory.Exists(spec.DraftChangePath))
            {
                blocker = L("import.missingDraftArtifacts");
                return false;
            }
            foreach (var name in new[] { "proposal.md", "design.md", "tasks.md", "change-review.json", "dependencies.json", "gaps.json", "sources.json" })
            {
                if (File.Exists(Path.Combine(spec.DraftChangePath, name)))
                    continue;
                blocker = AgentWorkbenchText.Format("import.missingArtifact", name);
                return false;
            }

            var changeReviewPath = Path.Combine(spec.DraftChangePath, "change-review.json");
            var changeReview = JsonUtility.FromJson<ChangeReview>(File.ReadAllText(changeReviewPath, Encoding.UTF8));
            var changeBlockingIssue = (changeReview?.reviewIssues ?? Array.Empty<ReviewIssue>()).FirstOrDefault(item =>
                item.blocksApproval &&
                !string.Equals(item.status, "resolved", StringComparison.OrdinalIgnoreCase) &&
                !(string.Equals(item.status, "accepted", StringComparison.OrdinalIgnoreCase) &&
                  !string.Equals(item.severity, "blocking", StringComparison.OrdinalIgnoreCase)));
            if (changeBlockingIssue != null)
            {
                blocker = Or(changeBlockingIssue.summary, L("review.issueOpen"));
                return false;
            }

            var pairedSpecs = (_selectedImportRun?.Specs ?? new List<DesignImportSpec>())
                .Where(item => item != null && string.Equals(
                    item.DraftChangePath,
                    spec.DraftChangePath,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (pairedSpecs.Count == 0)
                pairedSpecs.Add(spec);

            var conflictGroup = pairedSpecs
                .Select(item => _draftSpecGroups.FirstOrDefault(group => string.Equals(
                    group.capability,
                    item.Capability,
                    StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(group => group?.Versions.Count > 1);
            if (conflictGroup != null)
            {
                blocker = L("import.resolveConflictFirst");
                return false;
            }

            var blockingIssue = pairedSpecs.SelectMany(item => item.ReviewIssues).FirstOrDefault(item =>
                item.blocksApproval &&
                !string.Equals(item.status, "resolved", StringComparison.OrdinalIgnoreCase) &&
                !(string.Equals(item.status, "accepted", StringComparison.OrdinalIgnoreCase) &&
                  !string.Equals(item.severity, "blocking", StringComparison.OrdinalIgnoreCase)));
            if (blockingIssue != null)
            {
                blocker = Or(blockingIssue.summary, L("review.issueOpen"));
                return false;
            }

            foreach (var evidence in pairedSpecs.SelectMany(item => item.VerificationEvidence))
            {
                RefreshEvidenceIntegrity(evidence);
                if (string.Equals(evidence.effectiveStatus, "verified", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(evidence.effectiveStatus, "modified", StringComparison.OrdinalIgnoreCase))
                    continue;
                blocker = AgentWorkbenchText.Format(
                    "import.evidenceUnavailable",
                    Or(evidence.displayPath, evidence.guid),
                    EvidenceStatusLabel(evidence.effectiveStatus));
                return false;
            }

            foreach (var owner in pairedSpecs)
            {
                foreach (var dependency in owner.Dependencies)
                {
                    if (File.Exists(Path.Combine(spec.DraftChangePath, "specs", dependency.to, "spec.md")))
                        continue;
                    if (_specFiles.Any(item => string.Equals(
                            item.Capability,
                            dependency.to,
                            StringComparison.OrdinalIgnoreCase)))
                        continue;
                    var dependencyChange = FindChangeByCapability(dependency.to);
                    if (IsApprovedChangeProvider(dependencyChange))
                        continue;
                    var accepted = !dependency.blocksImplementation && owner.ReviewIssues.Any(item =>
                        string.Equals(item.status, "accepted", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(item.severity, "blocking", StringComparison.OrdinalIgnoreCase) &&
                        (string.Equals(item.sourceId, dependency.gapId, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(item.sourceId, dependency.id, StringComparison.OrdinalIgnoreCase)));
                    if (accepted)
                        continue;
                    if (dependencyChange != null)
                    {
                        blocker = AgentWorkbenchText.Format(
                            "import.dependencyChangeNotApproved",
                            DependencyDisplayName(dependency.to, _selectedImportRun?.Nodes));
                        return false;
                    }
                    blocker = AgentWorkbenchText.Format(
                        "import.dependencyNotFormal",
                        DependencyDisplayName(dependency.to, _selectedImportRun?.Nodes));
                    return false;
                }
            }
            return true;
        }

        private void AddImportSpecToChanges(DesignImportRun run, DesignImportSpec spec)
        {
            if (run == null || spec == null)
                return;
            if (!CanApproveDraft(spec, out var blocker))
            {
                ShowNotification(new GUIContent(blocker));
                return;
            }

            var changeId = ImportChangeId(run, spec);
            var changeRoot = Path.Combine(_openSpecPath, "changes", changeId);
            if (Directory.Exists(changeRoot))
            {
                ShowNotification(new GUIContent(L("import.alreadyChange")));
                return;
            }

            var draftRoot = spec.DraftChangePath;
            if (string.IsNullOrWhiteSpace(draftRoot) || !Directory.Exists(draftRoot))
            {
                ShowNotification(new GUIContent(L("import.missingDraftArtifacts")));
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    L("import.approveTitle"),
                    AgentWorkbenchText.Format("import.approveConfirm", spec.Title),
                    L("common.confirm"),
                    L("common.cancel")))
                return;

            MarkDraftApproved(draftRoot, spec, "implementation-change");
            Directory.CreateDirectory(Path.GetDirectoryName(changeRoot) ?? _openSpecPath);
            Directory.Move(draftRoot, changeRoot);
            RemoveTransferredDraftVersion(spec);
            spec.DraftChangePath = null;
            ReloadOpenSpec();
            ReloadDesignImports();
            ShowNotification(new GUIContent(AgentWorkbenchText.Format("import.moved", changeId)));
        }

        private void MarkDraftApproved(string draftRoot, DesignImportSpec spec, string approvalKind)
        {
            var path = Path.Combine(draftRoot, "change-review.json");
            if (!File.Exists(path))
                return;
            var review = JsonUtility.FromJson<ChangeReview>(File.ReadAllText(path, Encoding.UTF8)) ?? new ChangeReview();
            review.schemaVersion = 5;
            review.approvalStatus = approvalKind;
            review.approvedBy = Or(_maintainer, "未注明");
            review.approvedAt = DateTime.Now.ToString("o");
            if (!string.Equals(review.category, "paired", StringComparison.OrdinalIgnoreCase))
                review.codeReadiness = DeriveCodeReadiness(spec);
            review.readiness = "ready";
            review.reviewIssues = (review.reviewIssues ?? Array.Empty<ReviewIssue>())
                .Concat(spec.ReviewIssues ?? new List<ReviewIssue>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.id))
                .GroupBy(item => item.id, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.Last())
                .ToArray();
            File.WriteAllText(path, JsonUtility.ToJson(review, true), Encoding.UTF8);
        }

        private static string DeriveCodeReadiness(DesignImportSpec spec)
        {
            if (spec?.Category == "game-rule")
                return "not-applicable";
            if (spec == null)
                return "unimplemented";
            if (spec.VerificationDifferences.Count > 0 ||
                string.Equals(spec.VerificationStatus, "partial", StringComparison.OrdinalIgnoreCase))
                return "partial";
            if (string.Equals(spec.VerificationStatus, "verified", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(spec.VerificationStatus, "implemented", StringComparison.OrdinalIgnoreCase))
                return "implemented";
            return "unimplemented";
        }

        private static void LoadDraftChangeArtifacts(DesignImportSpec spec)
        {
            if (spec == null || string.IsNullOrWhiteSpace(spec.DraftChangePath) ||
                !Directory.Exists(spec.DraftChangePath))
                return;
            var proposalPath = Path.Combine(spec.DraftChangePath, "proposal.md");
            var designPath = Path.Combine(spec.DraftChangePath, "design.md");
            var tasksPath = Path.Combine(spec.DraftChangePath, "tasks.md");
            spec.ProposalContent = File.Exists(proposalPath)
                ? File.ReadAllText(proposalPath, Encoding.UTF8)
                : string.Empty;
            spec.DesignContent = File.Exists(designPath)
                ? File.ReadAllText(designPath, Encoding.UTF8)
                : string.Empty;
            spec.TasksContent = File.Exists(tasksPath)
                ? File.ReadAllText(tasksPath, Encoding.UTF8)
                : string.Empty;
            spec.Tasks = File.Exists(tasksPath)
                ? ParseTasks(File.ReadAllLines(tasksPath, Encoding.UTF8))
                : new List<ChangeTask>();
            var reviewPath = Path.Combine(spec.DraftChangePath, "change-review.json");
            if (File.Exists(reviewPath))
            {
                try
                {
                    var review = JsonUtility.FromJson<ChangeReview>(File.ReadAllText(reviewPath, Encoding.UTF8));
                    spec.ImplementationNotes = review?.implementationNotes;
                }
                catch
                {
                    spec.ImplementationNotes = string.Empty;
                }
            }
        }

        private static void WriteJsonArray<T>(string path, IEnumerable<T> values)
        {
            var items = values?.Select(value => JsonUtility.ToJson(value, true)).ToArray() ?? Array.Empty<string>();
            var json = items.Length == 0
                ? "[]\n"
                : "[\n" + string.Join(",\n", items.Select(item => Regex.Replace(item, "(?m)^", "  "))) + "\n]\n";
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        private static Dictionary<string, string> ParseRequirementBlocks(string markdown)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(markdown))
                return result;

            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                const string prefix = "### Requirement:";
                if (!lines[index].StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                var title = lines[index].Substring(prefix.Length).Trim();
                var end = index + 1;
                while (end < lines.Length &&
                       !lines[end].StartsWith(prefix, StringComparison.Ordinal) &&
                       !lines[end].StartsWith("## ", StringComparison.Ordinal))
                    end++;
                result[title] = string.Join("\n", lines.Skip(index).Take(end - index)).Trim();
                index = end - 1;
            }
            return result;
        }

        private static string NormalizeMarkdown(string markdown)
        {
            return Regex.Replace(markdown ?? string.Empty, @"\s+", " ").Trim();
        }

        private static string ImportChangeId(DesignImportRun run, DesignImportSpec spec)
        {
            if (!string.IsNullOrWhiteSpace(spec.ChangeId))
                return spec.ChangeId;
            var version = string.IsNullOrWhiteSpace(spec.VersionId) ? "draft" : spec.VersionId;
            var value = $"design-draft-{spec.Capability}-{version}".ToLowerInvariant();
            return Regex.Replace(value, @"[^a-z0-9]+", "-").Trim('-');
        }

        private void RemoveTransferredDraftVersion(DesignImportSpec spec)
        {
            var changePath = spec.DraftChangePath;
            foreach (var group in _draftSpecGroups.ToList())
            {
                group.Versions.RemoveAll(version =>
                    string.Equals(AbsoluteProjectPath(version.draftChangePath), changePath, StringComparison.OrdinalIgnoreCase));
                if (group.Versions.Count == 0)
                    _draftSpecGroups.Remove(group);
                else
                {
                    group.selectedVersionId = group.Versions[0].id;
                    group.status = group.Versions.Count > 1 ? "conflict" : "ready";
                }
            }
            SaveDraftStore();
        }

    }
}
#endif
