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
        private void Reload()
        {
            // 外部 Agent 可能刚写入 .meta / review；先刷新 GUID 数据库，避免证据链接短暂失效。
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            LoadLocalizationState();
            ReloadQueue();
            ReloadOpenSpec();
            ReloadDesignImports();
            ReloadEngineeringCatalog();
            LoadDesignSourceConfiguration();
            ReloadDocumentImplementationChanges();
        }

        private void LoadDesignSourceConfiguration()
        {
            _designDocumentSources.Clear();
            _designSourceStatus = "sync.notConfigured";

            if (File.Exists(_designSourceConfigPath))
            {
                try
                {
                    var configuration = JsonUtility.FromJson<DesignSourceConfiguration>(
                        File.ReadAllText(_designSourceConfigPath, Encoding.UTF8));
                    if (configuration?.sources != null && configuration.sources.Length > 0)
                    {
                        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var source in configuration.sources)
                        {
                            if (source == null || string.IsNullOrWhiteSpace(source.id) ||
                                !Regex.IsMatch(source.id, @"^[A-Za-z0-9._-]+$") || !ids.Add(source.id) ||
                                string.IsNullOrWhiteSpace(source.path))
                                throw new InvalidDataException(L("sync.invalidSources"));
                            source.path = Path.IsPathRooted(source.path)
                                ? Path.GetFullPath(source.path)
                                : Path.GetFullPath(Path.Combine(_projectRoot, source.path));
                            _designDocumentSources.Add(source);
                        }
                        UpdateDesignSourceStatus();
                        return;
                    }
                    if (configuration != null && !string.IsNullOrWhiteSpace(configuration.source))
                    {
                        _designDocumentSources.Add(new DesignSourceEntry
                        {
                            id = "primary",
                            path = Path.IsPathRooted(configuration.source)
                                ? Path.GetFullPath(configuration.source)
                                : Path.GetFullPath(Path.Combine(_projectRoot, configuration.source))
                        });
                        SaveDesignSourceConfiguration();
                        return;
                    }
                }
                catch (Exception exception)
                {
                    _designSourceStatus = AgentWorkbenchText.Format("sync.configError", exception.Message);
                    return;
                }
            }
        }

        private void SaveDesignSourceConfiguration()
        {
            var configuration = new DesignSourceConfiguration
            {
                schemaVersion = 2,
                sources = _designDocumentSources.ToArray(),
                configuredBy = Or(_maintainer, "未注明"),
                configuredAt = DateTime.Now.ToString("o")
            };
            Directory.CreateDirectory(_openSpecPath);
            File.WriteAllText(
                _designSourceConfigPath,
                JsonUtility.ToJson(configuration, true) + Environment.NewLine,
                Encoding.UTF8);
            UpdateDesignSourceStatus();
            if (_documentBridgeConnected)
                BuildDesignDocumentTree();
        }

        private void AddDesignDocumentSource(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (_designDocumentSources.Any(source =>
                    string.Equals(Path.GetFullPath(source.path), fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                _designSourceStatus = L("sync.duplicatePath");
                return;
            }

            _designDocumentSources.Add(new DesignSourceEntry
            {
                id = Guid.NewGuid().ToString("N"),
                path = fullPath
            });
            SaveDesignSourceConfiguration();
        }

        private void UpdateDesignSourceStatus()
        {
            if (_designDocumentSources.Count == 0)
            {
                _designSourceStatus = "sync.notConfigured";
                return;
            }

            var validCount = _designDocumentSources.Count(source => Directory.Exists(source.path));
            _designSourceStatus = validCount == _designDocumentSources.Count
                ? AgentWorkbenchText.Format("sync.configuredMultiple", validCount)
                : AgentWorkbenchText.Format("sync.configuredWithMissing", validCount, _designDocumentSources.Count - validCount);
        }

        private void ReloadDocumentImplementationChanges()
        {
            _documentImplementationLedger = null;
            _changedImplementedDocuments.Clear();
            _designDocumentTreeItems.Clear();
            _documentStructureError = string.Empty;
            _documentBridgeConnected = false;
            if (string.IsNullOrWhiteSpace(_documentWorkflowRoot))
            {
                _documentChangeStatus = "sync.packageNotSelected";
                return;
            }

            if (!Directory.Exists(_documentWorkflowRoot))
            {
                _documentChangeStatus = "sync.pathMissing";
                return;
            }

            var ledgerPath = DocumentImplementationLedgerPath();
            if (!File.Exists(ledgerPath))
            {
                _documentChangeStatus = "sync.ledgerMissing";
                return;
            }

            try
            {
                _documentImplementationLedger = JsonUtility.FromJson<DocumentImplementationLedger>(
                    File.ReadAllText(ledgerPath, Encoding.UTF8));
                if (_documentImplementationLedger?.entries != null)
                {
                    foreach (var entry in _documentImplementationLedger.entries.Where(entry => entry != null))
                    {
                        entry.detectedByFingerprint = false;
                        entry.manualChangeDetected = false;
                        var documentPath = ResolveImplementationDocumentPath(entry);
                        if (!string.IsNullOrWhiteSpace(entry.implementedFingerprint) &&
                            !string.IsNullOrWhiteSpace(documentPath) && File.Exists(documentPath))
                        {
                            var currentFingerprint = StableSpecHash(File.ReadAllText(documentPath, Encoding.UTF8));
                            entry.detectedByFingerprint = !string.Equals(
                                currentFingerprint,
                                entry.implementedFingerprint,
                                StringComparison.OrdinalIgnoreCase);
                            entry.manualChangeDetected = !string.IsNullOrWhiteSpace(entry.currentFingerprint)
                                ? !string.Equals(currentFingerprint, entry.currentFingerprint,
                                    StringComparison.OrdinalIgnoreCase)
                                : entry.detectedByFingerprint && !entry.changedAfterImplementation;
                        }
                    }

                    _changedImplementedDocuments.AddRange(_documentImplementationLedger.entries
                        .Where(entry => entry != null &&
                                        (entry.changedAfterImplementation || entry.detectedByFingerprint) &&
                                        !string.IsNullOrWhiteSpace(entry.documentPath))
                        .OrderByDescending(entry => entry.changedAt)
                        .ThenBy(entry => entry.documentPath, StringComparer.OrdinalIgnoreCase));
                }

                _documentChangeStatus = _changedImplementedDocuments.Count == 0
                    ? "sync.noImplementedChanges"
                    : AgentWorkbenchText.Format("sync.implementedChangesFound", _changedImplementedDocuments.Count);
                _documentBridgeConnected = true;
                BuildDesignDocumentTree();
            }
            catch (Exception exception)
            {
                _documentChangeStatus = AgentWorkbenchText.Format("sync.ledgerError", exception.Message);
            }
        }

        private bool TrySetDocumentWorkflowRoot(string selectedPath)
        {
            if (!TryFindDocumentPackageRoot(selectedPath, out var packageRoot))
            {
                _documentPackageSelectionError = true;
                _documentPackageSelectionStatus = AgentWorkbenchText.Format(
                    "sync.packageKeyNotFound",
                    ".design-workflow/implementation-ledger.json");
                return false;
            }

            _documentWorkflowRoot = packageRoot;
            EditorPrefs.SetString(DocumentWorkflowRootPrefKey, packageRoot);
            _documentPackageSelectionError = false;
            _documentPackageSelectionStatus = AgentWorkbenchText.Format("sync.packageFound", packageRoot);
            ReloadDocumentImplementationChanges();
            return true;
        }

        private static bool TryFindDocumentPackageRoot(string selectedPath, out string packageRoot)
        {
            packageRoot = null;
            if (string.IsNullOrWhiteSpace(selectedPath))
                return false;

            var selectedDirectory = File.Exists(selectedPath)
                ? Path.GetDirectoryName(Path.GetFullPath(selectedPath))
                : Path.GetFullPath(selectedPath);
            if (string.IsNullOrWhiteSpace(selectedDirectory) || !Directory.Exists(selectedDirectory))
                return false;

            for (var current = new DirectoryInfo(selectedDirectory); current != null; current = current.Parent)
            {
                if (!File.Exists(Path.Combine(current.FullName, ".design-workflow", "implementation-ledger.json")))
                    continue;
                packageRoot = current.FullName;
                return true;
            }

            var pending = new Queue<(string path, int depth)>();
            pending.Enqueue((selectedDirectory, 0));
            var visited = 0;
            while (pending.Count > 0 && visited < 1024)
            {
                var candidate = pending.Dequeue();
                visited++;
                if (File.Exists(Path.Combine(candidate.path, ".design-workflow", "implementation-ledger.json")))
                {
                    packageRoot = candidate.path;
                    return true;
                }

                if (candidate.depth >= 4)
                    continue;
                try
                {
                    foreach (var child in Directory.EnumerateDirectories(candidate.path))
                    {
                        var name = Path.GetFileName(child);
                        if (string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name, "Library", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name, "Temp", StringComparison.OrdinalIgnoreCase))
                            continue;
                        pending.Enqueue((child, candidate.depth + 1));
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Continue checking other reachable directories.
                }
                catch (IOException)
                {
                    // Continue checking other reachable directories.
                }
            }

            return false;
        }

        private void BuildDesignDocumentTree()
        {
            _designDocumentTreeItems.Clear();
            if (!_documentBridgeConnected || string.IsNullOrWhiteSpace(_documentWorkflowRoot))
                return;

            try
            {
                var root = Path.GetFullPath(_documentWorkflowRoot);
                var markdownFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var sourceRoots = _designDocumentSources
                    .Where(source => source != null && !string.IsNullOrWhiteSpace(source.path) &&
                                     Directory.Exists(source.path) && IsPathInsideRoot(source.path, root))
                    .Select(source => Path.GetFullPath(source.path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (sourceRoots.Count == 0)
                    sourceRoots.Add(root);

                foreach (var sourceRoot in sourceRoots)
                {
                    foreach (var file in EnumerateDesignMarkdownFiles(sourceRoot))
                        markdownFiles.Add(Path.GetFullPath(file));
                }

                if (_documentImplementationLedger?.entries != null)
                {
                    foreach (var entry in _documentImplementationLedger.entries.Where(entry => entry != null))
                    {
                        var path = ResolveImplementationDocumentPath(entry);
                        if (!string.IsNullOrWhiteSpace(path))
                            markdownFiles.Add(path);
                    }
                }

                var relativeFiles = markdownFiles
                    .Select(path => new
                    {
                        absolutePath = path,
                        relativePath = Path.GetRelativePath(root, path).Replace('\\', '/')
                    })
                    .Where(item => !item.relativePath.StartsWith("../", StringComparison.Ordinal) &&
                                   !string.Equals(item.relativePath, "..", StringComparison.Ordinal) &&
                                   !string.Equals(item.relativePath, "AGENTS.md", StringComparison.OrdinalIgnoreCase) &&
                                   !string.Equals(item.relativePath, "SETUP.md", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(item => item.relativePath, StringComparer.OrdinalIgnoreCase)
                    .Take(500)
                    .ToList();

                var emittedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in relativeFiles)
                {
                    var segments = file.relativePath.Split('/');
                    var directoryPath = string.Empty;
                    for (var index = 0; index < segments.Length - 1; index++)
                    {
                        directoryPath = string.IsNullOrEmpty(directoryPath)
                            ? segments[index]
                            : directoryPath + "/" + segments[index];
                        if (!emittedDirectories.Add(directoryPath))
                            continue;
                        _designDocumentTreeItems.Add(new DesignDocumentTreeItem
                        {
                            isDirectory = true,
                            depth = index,
                            displayName = segments[index],
                            relativePath = directoryPath
                        });
                    }

                    var matchingEntries = (_documentImplementationLedger?.entries ?? Array.Empty<DocumentImplementationEntry>())
                        .Where(entry => entry != null && string.Equals(
                            NormalizeDocumentRelativePath(entry.documentPath),
                            file.relativePath,
                            StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    var progress = matchingEntries.Count == 0
                        ? 0
                        : matchingEntries.Max(ResolveImplementationProgress);
                    var changed = matchingEntries.Any(entry =>
                        entry.changedAfterImplementation || entry.detectedByFingerprint);
                    var summaries = matchingEntries
                        .Where(entry => entry.changedAfterImplementation || entry.detectedByFingerprint)
                        .Select(entry => entry.manualChangeDetected || string.IsNullOrWhiteSpace(entry.changeSummary)
                            ? L("sync.manualChange")
                            : entry.changeSummary.Trim())
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();

                    _designDocumentTreeItems.Add(new DesignDocumentTreeItem
                    {
                        depth = Math.Max(0, segments.Length - 1),
                        displayName = Path.GetFileNameWithoutExtension(file.absolutePath),
                        relativePath = file.relativePath,
                        absolutePath = file.absolutePath,
                        implementationProgress = progress,
                        implementationStatus = ResolveImplementationStatus(matchingEntries, progress),
                        changedAfterImplementation = changed,
                        changeSummary = string.Join("；", summaries)
                    });
                }

                if (markdownFiles.Count > 500)
                    _documentStructureError = AgentWorkbenchText.Format("sync.documentLimit", 500, markdownFiles.Count);
            }
            catch (Exception exception)
            {
                _documentStructureError = AgentWorkbenchText.Format("sync.documentStructureError", exception.Message);
            }
        }

        private static IEnumerable<string> EnumerateDesignMarkdownFiles(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                IEnumerable<string> files;
                IEnumerable<string> directories;
                try
                {
                    files = Directory.EnumerateFiles(current, "*.md", SearchOption.TopDirectoryOnly).ToArray();
                    directories = Directory.EnumerateDirectories(current).ToArray();
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (var file in files)
                    yield return file;
                foreach (var directory in directories)
                {
                    var name = Path.GetFileName(directory);
                    if (name.StartsWith(".", StringComparison.Ordinal) ||
                        string.Equals(name, "Library", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "Temp", StringComparison.OrdinalIgnoreCase))
                        continue;
                    pending.Push(directory);
                }
            }
        }

        private static bool IsPathInsideRoot(string path, string root)
        {
            var fullPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDocumentRelativePath(string path) =>
            (path ?? string.Empty).Replace('\\', '/').TrimStart('/');

        private static int ResolveImplementationProgress(DocumentImplementationEntry entry)
        {
            if (entry == null)
                return 0;
            if (entry.implementationProgress > 0)
                return Math.Max(0, Math.Min(100, entry.implementationProgress));
            return !string.IsNullOrWhiteSpace(entry.implementedAt) ||
                   !string.IsNullOrWhiteSpace(entry.implementedFingerprint)
                ? 100
                : 0;
        }

        private static string ResolveImplementationStatus(
            IReadOnlyCollection<DocumentImplementationEntry> entries,
            int progress)
        {
            var explicitStatus = entries
                .Select(entry => entry.implementationStatus)
                .FirstOrDefault(status => !string.IsNullOrWhiteSpace(status));
            if (!string.IsNullOrWhiteSpace(explicitStatus))
                return explicitStatus;
            if (progress >= 100)
                return "implemented";
            return progress > 0 ? "in-progress" : "not-implemented";
        }

        private string DocumentImplementationLedgerPath()
        {
            return string.IsNullOrWhiteSpace(_documentWorkflowRoot)
                ? string.Empty
                : Path.Combine(_documentWorkflowRoot, ".design-workflow", "implementation-ledger.json");
        }

        private string ResolveImplementationDocumentPath(DocumentImplementationEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.documentPath) ||
                string.IsNullOrWhiteSpace(_documentWorkflowRoot))
                return null;

            var root = Path.GetFullPath(_documentWorkflowRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var relativePath = entry.documentPath
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
            var rootPrefix = root + Path.DirectorySeparatorChar;
            return candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ? candidate : null;
        }

        private void ReloadQueue()
        {
            _items.Clear();
            _loadError = null;

            if (!File.Exists(_queuePath))
            {
                _loadError = $"未找到增量维护工作台：{_queuePath}";
                return;
            }

            var lines = File.ReadAllLines(_queuePath, Encoding.UTF8);
            var start = FindLine(lines, line =>
            {
                var heading = line.Trim();
                return string.Equals(heading, "## 待处理队列", StringComparison.Ordinal) ||
                       heading.StartsWith("## 📋 待处理队列", StringComparison.Ordinal);
            });
            var end = start < 0
                ? -1
                : FindLine(lines, line => line.TrimStart().StartsWith("## ", StringComparison.Ordinal), start + 1);
            if (start >= 0 && end < 0)
                end = lines.Length;

            if (start < 0)
            {
                _loadError = "未能在 REFACTOR_QUEUE.md 中定位「待处理队列」章节。";
                return;
            }

            for (var i = start + 1; i < end; i++)
            {
                if (lines[i].TrimStart().StartsWith("<!--", StringComparison.Ordinal))
                {
                    while (i < end && !lines[i].Contains("-->", StringComparison.Ordinal))
                        i++;
                    continue;
                }

                if (!lines[i].StartsWith("### ", StringComparison.Ordinal))
                    continue;

                var itemStart = i;
                var itemEnd = end - 1;
                for (var j = i + 1; j < end; j++)
                {
                    if (lines[j].StartsWith("### ", StringComparison.Ordinal))
                    {
                        itemEnd = j - 1;
                        break;
                    }
                }

                _items.Add(ParseItem(lines, itemStart, itemEnd));
                i = itemEnd;
            }
        }

        private void ReloadOpenSpec()
        {
            var selectedChangeName = _selectedChange?.Name;
            var selectedCapability = _selectedSpec?.Capability;
            _specFiles.Clear();
            _openChanges.Clear();
            _specGaps.Clear();
            _dependencyNodes.Clear();
            _dependencyEdges.Clear();
            _specMetadataError = null;

            if (!Directory.Exists(_openSpecPath))
                return;

            var specsPath = Path.Combine(_openSpecPath, "specs");
            if (Directory.Exists(specsPath))
            {
                foreach (var path in Directory.GetFiles(specsPath, "spec.md", SearchOption.AllDirectories).OrderBy(p => p))
                {
                    var relative = RelativeTo(specsPath, path);
                    var capability = relative.Split('/')[0];
                    if (string.Equals(capability, "spec.md", StringComparison.OrdinalIgnoreCase))
                        capability = Path.GetFileNameWithoutExtension(path);
                    var content = File.ReadAllText(path, Encoding.UTF8);
                    var reviewPath = Path.Combine(Path.GetDirectoryName(path) ?? specsPath, "spec-review.json");
                    ImportSpecReview review = null;
                    if (File.Exists(reviewPath))
                        review = JsonUtility.FromJson<ImportSpecReview>(File.ReadAllText(reviewPath, Encoding.UTF8));
                    var category = NormalizeCategory(Or(review?.category, ReadFrontmatterValue(content, "category")));
                    var canonicalTitle = Or(review?.title, ExtractMarkdownTitle(content, capability));
                    capability = Or(review?.capability, capability);
                    var title = ResolveSpecDisplayTitle(capability, path, canonicalTitle);
                    _specFiles.Add(new SpecFile(
                        title,
                        path,
                        capability,
                        category,
                        BuildCodeEvidence(review?.verification, review?.sourceReferences, title),
                        review?.sourceReferences,
                        content,
                        review?.editorGuidance,
                        review?.pairedFeatureCapability,
                        review?.pairedRuleCapability,
                        review?.implementationOutline,
                        canonicalTitle));
                }
            }

            _selectedSpec = _specFiles.FirstOrDefault(spec => spec.Capability == selectedCapability) ??
                            _specFiles.FirstOrDefault();

            LoadSpecMetadata();

            var changesPath = Path.Combine(_openSpecPath, "changes");
            if (!Directory.Exists(changesPath))
                return;

            foreach (var dir in Directory.GetDirectories(changesPath).OrderBy(p => p))
            {
                if (string.Equals(Path.GetFileName(dir), "archive", StringComparison.OrdinalIgnoreCase))
                    continue;

                _openChanges.Add(LoadChangeEntry(dir));
            }

            _selectedChange = _openChanges.FirstOrDefault(change => change.Name == selectedChangeName) ??
                              _openChanges.FirstOrDefault();
        }

        private ChangeEntry LoadChangeEntry(string directory)
        {
            var reviewPath = Path.Combine(directory, "change-review.json");
            ChangeReview review = null;
            if (File.Exists(reviewPath))
            {
                try
                {
                    review = JsonUtility.FromJson<ChangeReview>(File.ReadAllText(reviewPath, Encoding.UTF8));
                }
                catch
                {
                    // Legacy OpenSpec changes remain readable without structured audit metadata.
                }
            }

            var proposalPath = Path.Combine(directory, "proposal.md");
            var designPath = Path.Combine(directory, "design.md");
            var tasksPath = Path.Combine(directory, "tasks.md");
            var proposal = File.Exists(proposalPath) ? File.ReadAllText(proposalPath, Encoding.UTF8) : string.Empty;
            var design = File.Exists(designPath) ? File.ReadAllText(designPath, Encoding.UTF8) : string.Empty;
            var tasksContent = File.Exists(tasksPath) ? File.ReadAllText(tasksPath, Encoding.UTF8) : string.Empty;
            var tasks = File.Exists(tasksPath) ? ParseTasks(File.ReadAllLines(tasksPath, Encoding.UTF8)) : new List<ChangeTask>();

            var graphPath = Path.Combine(directory, "dependencies.json");
            var graph = File.Exists(graphPath)
                ? JsonUtility.FromJson<DependencyGraph>(File.ReadAllText(graphPath, Encoding.UTF8))
                : null;
            var gaps = ReadGapArray(Path.Combine(directory, "gaps.json"));

            var specs = LoadChangeSpecs(directory);
            var firstSpec = specs.FirstOrDefault();
            var categories = new List<string>();
            foreach (var spec in specs)
                AddChangeCategory(categories, spec.Category);
            AddChangeCategory(categories, review?.category);
            if (categories.Count == 0)
                AddChangeCategory(categories, firstSpec?.Category);
            var category = categories.Count > 1
                ? "paired"
                : categories.FirstOrDefault() ?? "unclassified";
            var name = Path.GetFileName(directory);
            var capabilities = review?.capabilities?.Where(item => !string.IsNullOrWhiteSpace(item)).ToList()
                ?? LoadChangeCapabilities(directory);
            var capabilitySet = new HashSet<string>(capabilities, StringComparer.OrdinalIgnoreCase);
            var externalDependencies = (graph?.edges ?? Array.Empty<DependencyEdge>())
                .Where(edge => edge != null && !IsInternalChangeDependency(edge, capabilitySet))
                .ToList();
            var evidence = LoadChangeCodeEvidence(directory, review?.verification, review?.title);
            ApplyLocalizedReviewIssues(reviewPath, review?.reviewIssues);
            return new ChangeEntry
            {
                Name = name,
                Title = Or(
                    review?.title,
                    Regex.Replace(
                        ExtractMarkdownTitle(proposal, name),
                        @"^(?:Change|Proposal):\s*",
                        string.Empty,
                        RegexOptions.IgnoreCase)),
                Path = directory,
                Capability = Or(
                    review?.capabilities?.FirstOrDefault(),
                    Or(firstSpec?.Capability, name)),
                Capabilities = capabilities,
                Category = category,
                Categories = categories,
                Specs = specs,
                Readiness = review?.readiness,
                CodeReadiness = Or(review?.codeReadiness, review?.readiness),
                ApprovalStatus = review?.approvalStatus,
                SourceKind = review?.sourceKind,
                ImplementationNotes = review?.implementationNotes,
                SpecSyncStatus = Or(review?.specSyncStatus, "pending"),
                SyncValidation = review?.syncValidation,
                SyncTargets = review?.syncTargets?.Where(item => item != null).ToList() ?? new List<SyncTarget>(),
                ImplementationOutlines = LoadChangeImplementationOutlines(directory),
                Verification = review?.verification,
                Evidence = evidence,
                ProposalContent = proposal,
                DesignContent = design,
                TasksContent = tasksContent,
                Tasks = tasks,
                ReviewIssues = review?.reviewIssues?.ToList() ?? new List<ReviewIssue>(),
                Dependencies = externalDependencies,
                Gaps = gaps.ToList(),
                EditorGuidance = LoadEditorGuidance(directory)
            };
        }

        private List<SpecFile> LoadChangeSpecs(string changeDirectory)
        {
            var result = new List<SpecFile>();
            var specsDirectory = Path.Combine(changeDirectory, "specs");
            if (!Directory.Exists(specsDirectory))
                return result;

            foreach (var specPath in Directory.GetFiles(
                         specsDirectory,
                         "spec.md",
                         SearchOption.AllDirectories).OrderBy(path => path))
            {
                var content = File.ReadAllText(specPath, Encoding.UTF8);
                var reviewPath = Path.Combine(Path.GetDirectoryName(specPath) ?? specsDirectory, "spec-review.json");
                ImportSpecReview specReview = null;
                if (File.Exists(reviewPath))
                {
                    try
                    {
                        specReview = JsonUtility.FromJson<ImportSpecReview>(
                            File.ReadAllText(reviewPath, Encoding.UTF8));
                    }
                    catch
                    {
                        // 损坏的可选 review 回退到 Spec frontmatter 与目录名。
                    }
                }

                var capability = Or(
                    specReview?.capability,
                    new DirectoryInfo(Path.GetDirectoryName(specPath) ?? specsDirectory).Name);
                var canonicalTitle = Or(
                    specReview?.title,
                    ExtractMarkdownTitle(content, capability));
                result.Add(new SpecFile(
                    ResolveSpecDisplayTitle(capability, specPath, canonicalTitle),
                    specPath,
                    capability,
                    Or(specReview?.category, ReadFrontmatterValue(content, "category")),
                    content: content,
                    canonicalTitle: canonicalTitle));
            }

            return result;
        }

        private static List<string> LoadChangeCapabilities(string changeDirectory)
        {
            var specsDirectory = Path.Combine(changeDirectory, "specs");
            return Directory.Exists(specsDirectory)
                ? Directory.GetDirectories(specsDirectory)
                    .Select(Path.GetFileName)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList()
                : new List<string>();
        }

        private List<CodeEvidence> LoadChangeCodeEvidence(
            string changeDirectory,
            ImportSpecVerification changeVerification,
            string changeTitle)
        {
            var result = BuildCodeEvidence(changeVerification, null, changeTitle).ToList();
            var specsDirectory = Path.Combine(changeDirectory, "specs");
            if (!Directory.Exists(specsDirectory))
                return result;

            foreach (var reviewPath in Directory.GetFiles(
                         specsDirectory,
                         "spec-review.json",
                         SearchOption.AllDirectories).OrderBy(path => path))
            {
                try
                {
                    var review = JsonUtility.FromJson<ImportSpecReview>(
                        File.ReadAllText(reviewPath, Encoding.UTF8));
                    if (review?.verification == null)
                        continue;
                    result.AddRange(BuildCodeEvidence(
                        review.verification,
                        review.sourceReferences,
                        Or(review.title, changeTitle)));
                }
                catch
                {
                    // 单个 capability review 损坏时仍展示其余 Change 级代码依据。
                }
            }

            return result
                .Where(item => item != null)
                .GroupBy(EvidenceItemIdentity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static bool IsInternalChangeDependency(
            DependencyEdge edge,
            ISet<string> changeCapabilities) =>
            edge != null &&
            changeCapabilities != null &&
            changeCapabilities.Contains(edge.from) &&
            changeCapabilities.Contains(edge.to);

        private static Dictionary<string, List<string>> LoadChangeImplementationOutlines(string changeDirectory)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var specsDirectory = Path.Combine(changeDirectory, "specs");
            if (!Directory.Exists(specsDirectory))
                return result;
            foreach (var reviewPath in Directory.GetFiles(specsDirectory, "spec-review.json", SearchOption.AllDirectories))
            {
                try
                {
                    var review = JsonUtility.FromJson<ImportSpecReview>(File.ReadAllText(reviewPath, Encoding.UTF8));
                    if (review == null || string.IsNullOrWhiteSpace(review.capability) ||
                        review.implementationOutline == null)
                        continue;
                    result[review.capability] = review.implementationOutline
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .ToList();
                }
                catch
                {
                    // 单个可选实现摘要损坏时不影响 Change 列表。
                }
            }
            return result;
        }

        private static void AddChangeCategory(ICollection<string> categories, string category)
        {
            var normalized = NormalizeCategory(category);
            if (normalized == "unclassified" || categories.Contains(normalized))
                return;
            categories.Add(normalized);
        }

        private static List<CapabilityEditorGuidance> LoadEditorGuidance(string changeDirectory)
        {
            var result = new List<CapabilityEditorGuidance>();
            var specsDirectory = Path.Combine(changeDirectory, "specs");
            if (!Directory.Exists(specsDirectory))
                return result;

            foreach (var reviewPath in Directory.GetFiles(
                         specsDirectory,
                         "spec-review.json",
                         SearchOption.AllDirectories).OrderBy(path => path))
            {
                try
                {
                    var review = JsonUtility.FromJson<ImportSpecReview>(File.ReadAllText(reviewPath, Encoding.UTF8));
                    var category = NormalizeCategory(review?.category);
                    if (!IsEditorGuidanceCategory(category) || !HasEditorGuidance(review?.editorGuidance))
                        continue;
                    var capability = Or(review?.capability, new DirectoryInfo(Path.GetDirectoryName(reviewPath) ?? specsDirectory).Name);
                    result.Add(new CapabilityEditorGuidance
                    {
                        Capability = capability,
                        Title = Or(review?.title, capability),
                        Category = category,
                        Guidance = review.editorGuidance
                    });
                }
                catch
                {
                    // 单个可选指引损坏时不影响 Change 其余内容展示。
                }
            }
            return result;
        }

        private static List<ChangeTask> ParseTasks(IEnumerable<string> lines)
        {
            var result = new List<ChangeTask>();
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"^\s*-\s*\[(?<done>[xX ])\]\s*(?<text>.+)$");
                if (!match.Success)
                    continue;
                result.Add(new ChangeTask
                {
                    Completed = !string.IsNullOrWhiteSpace(match.Groups["done"].Value),
                    Text = match.Groups["text"].Value.Trim()
                });
            }
            return result;
        }

        private static SpecGap[] ReadGapArray(string path)
        {
            if (!File.Exists(path))
                return Array.Empty<SpecGap>();
            var json = File.ReadAllText(path, Encoding.UTF8);
            var wrapper = JsonUtility.FromJson<SpecGapList>($"{{\"items\":{json}}}");
            return wrapper?.items ?? Array.Empty<SpecGap>();
        }

        private static string ReadFrontmatterValue(string markdown, string key)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;
            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != "---")
                return string.Empty;
            for (var index = 1; index < lines.Length; index++)
            {
                if (lines[index].Trim() == "---")
                    break;
                var separator = lines[index].IndexOf(':');
                if (separator <= 0 || !lines[index].Substring(0, separator).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    continue;
                return lines[index].Substring(separator + 1).Trim().Trim('"', '\'');
            }
            return string.Empty;
        }

        private static string NormalizeCategory(string category)
        {
            return category switch
            {
                "architecture" => "system",
                "system" => "system",
                "feature" => "feature",
                "feature-implementation" => "feature",
                "game-rule" => "game-rule",
                "rule" => "game-rule",
                _ => "unclassified"
            };
        }

        private void LoadSpecMetadata()
        {
            try
            {
                var gapsPath = Path.Combine(_specMetadataPath, "gaps.json");
                if (File.Exists(gapsPath))
                {
                    var json = File.ReadAllText(gapsPath, Encoding.UTF8);
                    var wrapper = JsonUtility.FromJson<SpecGapList>($"{{\"items\":{json}}}");
                    if (wrapper?.items != null)
                        _specGaps.AddRange(wrapper.items);
                }

                var dependenciesPath = Path.Combine(_specMetadataPath, "dependencies.json");
                if (File.Exists(dependenciesPath))
                {
                    var graph = JsonUtility.FromJson<DependencyGraph>(
                        File.ReadAllText(dependenciesPath, Encoding.UTF8));
                    if (graph?.nodes != null)
                    {
                        foreach (var node in graph.nodes)
                            node.category = NormalizeCategory(node.category);
                        _dependencyNodes.AddRange(graph.nodes);
                    }
                    if (graph?.edges != null)
                        _dependencyEdges.AddRange(graph.edges);
                }
            }
            catch (Exception exception)
            {
                _specMetadataError = $"Spec metadata 读取失败：{exception.Message}";
            }
        }

        private void EnsureDraftStoreMigrated()
        {
            // Design import content lives only inside Draft Changes. The migration script
            // performs the one-time verified removal of the legacy drafts/specs store.
            Directory.CreateDirectory(Path.Combine(_draftStorePath, "changes"));
            LoadDraftStore();
        }

        private void LoadDraftStore()
        {
            _draftSpecGroups.Clear();
            var indexPath = Path.Combine(_draftStorePath, "index.json");
            if (!File.Exists(indexPath))
                return;
            try
            {
                var index = JsonUtility.FromJson<DraftStoreIndex>(File.ReadAllText(indexPath, Encoding.UTF8));
                if (index?.groups != null)
                    _draftSpecGroups.AddRange(index.groups);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Draft Spec index 读取失败：{exception.Message}");
            }
        }

        private void SaveDraftStore()
        {
            Directory.CreateDirectory(_draftStorePath);
            var index = new DraftStoreIndex { groups = _draftSpecGroups.OrderBy(item => item.capability).ToList() };
            File.WriteAllText(Path.Combine(_draftStorePath, "index.json"), JsonUtility.ToJson(index, true), Encoding.UTF8);
            WriteDraftRunReferences();
        }

        private void WriteDraftRunReferences()
        {
            if (!Directory.Exists(_designImportsPath))
                return;
            foreach (var runDirectory in Directory.GetDirectories(_designImportsPath).ToList())
            {
                var runId = Path.GetFileName(runDirectory);
                var refs = _draftSpecGroups.SelectMany(group => group.Versions
                        .Where(version =>
                            (version.runIds ?? Array.Empty<string>()).Contains(runId, StringComparer.OrdinalIgnoreCase) &&
                            Directory.Exists(AbsoluteProjectPath(version.draftChangePath)))
                        .Select(version => new DraftSpecReference
                        {
                            capability = group.capability,
                            changeId = Or(version.changeId, Path.GetFileName(AbsoluteProjectPath(version.draftChangePath))),
                            status = group.status
                        }))
                    .ToArray();
                if (refs.Length == 0)
                {
                    if (!RunHasLiveDraftReference(runDirectory))
                        DeleteDirectoryInsideDesignImports(runDirectory);
                    continue;
                }
                var wrapper = new DraftSpecReferenceList { items = refs };
                File.WriteAllText(Path.Combine(runDirectory, "draft-refs.json"), JsonUtility.ToJson(wrapper, true), Encoding.UTF8);
            }
        }

        private bool RunHasLiveDraftReference(string runDirectory)
        {
            var path = Path.Combine(runDirectory, "draft-refs.json");
            if (!File.Exists(path))
                return false;
            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8).Trim();
                var wrapper = json.StartsWith("[", StringComparison.Ordinal)
                    ? JsonUtility.FromJson<DraftSpecReferenceList>($"{{\"items\":{json}}}")
                    : JsonUtility.FromJson<DraftSpecReferenceList>(json);
                return (wrapper?.items ?? Array.Empty<DraftSpecReference>()).Any(reference =>
                    !string.IsNullOrWhiteSpace(reference.changeId) &&
                    Directory.Exists(Path.Combine(_draftStorePath, "changes", reference.changeId)));
            }
            catch
            {
                // 损坏的引用文件不能证明仍有 Draft；中央索引仍是主要恢复源。
                return false;
            }
        }

        private void DeleteDirectoryInsideDesignImports(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;
            var root = Path.GetFullPath(_designImportsPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (target.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !string.Equals(target, root, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(path, true);
        }

        private void PruneMissingDraftVersions()
        {
            var changed = false;
            foreach (var group in _draftSpecGroups.ToList())
            {
                var removed = group.Versions.RemoveAll(version =>
                    !Directory.Exists(AbsoluteProjectPath(version.draftChangePath)));
                changed |= removed > 0;
                if (group.Versions.Count == 0)
                {
                    _draftSpecGroups.Remove(group);
                    changed = true;
                    continue;
                }
                group.selectedVersionId = group.Versions.Any(version => string.Equals(
                    version.id,
                    group.selectedVersionId,
                    StringComparison.OrdinalIgnoreCase))
                    ? group.selectedVersionId
                    : group.Versions[0].id;
                group.status = group.Versions.Count > 1 ? "conflict" : "ready";
            }
            if (changed)
                SaveDraftStore();
            else
                WriteDraftRunReferences();
        }

        private static string StableSpecHash(string content)
        {
            var normalized = (content ?? string.Empty).Replace("\r\n", "\n").Trim();
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(normalized)).Select(value => value.ToString("x2")));
        }

        private static string SafePathSegment(string value)
        {
            var result = Regex.Replace(value ?? string.Empty, @"[^A-Za-z0-9._-]+", "-").Trim('-');
            return string.IsNullOrWhiteSpace(result) ? "draft-spec" : result;
        }

        private string AbsoluteProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(_projectRoot, path));
        }

        private static DraftSpecVersion CloneDraftVersion(DraftSpecVersion source) => new()
        {
            id = source.id,
            runIds = source.runIds?.ToArray(),
            contentHash = source.contentHash,
            changeId = source.changeId,
            specPath = source.specPath,
            reviewPath = source.reviewPath,
            draftChangePath = source.draftChangePath,
            createdAt = source.createdAt
        };

        private void DeleteDraftVersionFiles(DraftSpecVersion version)
        {
            DeleteDirectoryInsideDraftStore(AbsoluteProjectPath(version.draftChangePath));
        }

        private void DeleteDirectoryInsideDraftStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;
            var root = Path.GetFullPath(_draftStorePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(path, true);
        }

        private void ReloadDesignImports()
        {
            var selectedRunId = _selectedImportRun?.runId;
            var selectedCapability = _selectedImportSpec?.Capability;
            var selectedVersionId = _selectedImportSpec?.VersionId;
            _designImports.Clear();
            EnsureDraftStoreMigrated();
            PruneMissingDraftVersions();
            if (!Directory.Exists(_designImportsPath))
                return;

            foreach (var directory in Directory.GetDirectories(_designImportsPath))
            {
                try
                {
                    var runPath = Path.Combine(directory, "run.json");
                    if (!File.Exists(runPath))
                        continue;

                    var run = JsonUtility.FromJson<DesignImportRun>(File.ReadAllText(runPath, Encoding.UTF8));
                    if (run == null)
                        continue;
                    run.DirectoryPath = directory;

                    var gapsPath = Path.Combine(directory, "gaps.json");
                    if (File.Exists(gapsPath))
                    {
                        var json = File.ReadAllText(gapsPath, Encoding.UTF8);
                        var wrapper = JsonUtility.FromJson<SpecGapList>($"{{\"items\":{json}}}");
                        run.GapCount = wrapper?.items?.Length ?? 0;
                        run.BlockingGapCount = wrapper?.items?.Count(item =>
                            item.blocksImplementation && item.status != "resolved") ?? 0;
                        run.Gaps = wrapper?.items;
                    }

                    var dependenciesPath = Path.Combine(directory, "dependencies.json");
                    var graph = File.Exists(dependenciesPath)
                        ? JsonUtility.FromJson<DependencyGraph>(
                            File.ReadAllText(dependenciesPath, Encoding.UTF8))
                        : null;
                    run.Dependencies = graph?.edges?.ToList() ?? new List<DependencyEdge>();
                    var nodes = graph?.nodes?.ToDictionary(item => item.id, item => item) ??
                                new Dictionary<string, DependencyNode>();
                    run.Nodes = nodes.Values.ToList();
                    run.SpecGroups = LoadDraftGroupsForRun(run, nodes);
                    run.Specs = run.SpecGroups.SelectMany(item => item.Versions)
                        .Select(item => item.Spec)
                        .Where(item => item != null)
                        .ToList();
                    foreach (var spec in run.Specs)
                    {
                        LoadDraftChangeArtifacts(spec);
                    }
                    _designImports.Add(run);
                }
                catch (Exception exception)
                {
                    _designImports.Add(new DesignImportRun
                    {
                        runId = Path.GetFileName(directory),
                        status = $"读取失败：{exception.Message}",
                        DirectoryPath = directory
                    });
                }
            }

            _selectedImportRun = _designImports.FirstOrDefault(item => item.runId == selectedRunId);
            if (_selectedImportRun == null)
                _selectedImportRun = _designImports.OrderByDescending(item => item.createdAt).FirstOrDefault();
            _selectedDraftGroup = _selectedImportRun?.SpecGroups?.FirstOrDefault(item =>
                                      string.Equals(item.capability, selectedCapability, StringComparison.OrdinalIgnoreCase)) ??
                                  _selectedImportRun?.SpecGroups?.FirstOrDefault();
            var selectedVersion = _selectedDraftGroup?.Versions.FirstOrDefault(item =>
                                      string.Equals(item.id, selectedVersionId, StringComparison.OrdinalIgnoreCase)) ??
                                  SelectedVersion(_selectedDraftGroup);
            _selectedImportSpec = selectedVersion?.Spec;
        }

        private List<DraftSpecGroup> LoadDraftGroupsForRun(
            DesignImportRun run,
            IReadOnlyDictionary<string, DependencyNode> nodes)
        {
            var result = new List<DraftSpecGroup>();
            foreach (var stored in _draftSpecGroups.Where(group => group.Versions.Any(version =>
                         (version.runIds ?? Array.Empty<string>()).Contains(run.runId, StringComparer.OrdinalIgnoreCase))))
            {
                var group = new DraftSpecGroup
                {
                    capability = stored.capability,
                    title = stored.title,
                    status = stored.status,
                    selectedVersionId = stored.selectedVersionId,
                    versions = new List<DraftSpecVersion>()
                };
                // 导入记录只负责筛选到冲突组；进入组后必须展示该 capability 的全部版本。
                foreach (var storedVersion in stored.Versions)
                {
                    var version = CloneDraftVersion(storedVersion);
                    version.Spec = LoadCentralDraftSpec(run, version, nodes);
                    group.versions.Add(version);
                }
                var displayVersion = SelectedVersion(group);
                if (!string.IsNullOrWhiteSpace(displayVersion?.Spec?.Title))
                    group.title = displayVersion.Spec.Title;
                if (group.Versions.Count > 0)
                    result.Add(group);
            }
            return result.OrderBy(item => item.Title).ToList();
        }

        private DesignImportSpec LoadCentralDraftSpec(
            DesignImportRun run,
            DraftSpecVersion version,
            IReadOnlyDictionary<string, DependencyNode> nodes)
        {
            var specPath = AbsoluteProjectPath(version.specPath);
            if (!File.Exists(specPath))
                return null;
            var content = File.ReadAllText(specPath, Encoding.UTF8);
            ImportSpecReview review = null;
            var reviewPath = AbsoluteProjectPath(version.reviewPath);
            if (File.Exists(reviewPath))
            {
                try { review = JsonUtility.FromJson<ImportSpecReview>(File.ReadAllText(reviewPath, Encoding.UTF8)); }
                catch { /* 旧 review 损坏时继续展示 Markdown。 */ }
            }

            var capability = Or(review?.capability, Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(specPath))));
            var gapIds = review?.gapIds ?? Array.Empty<string>();
            var dependencyIds = review?.dependencyIds ?? Array.Empty<string>();
            nodes.TryGetValue(capability, out var node);
            var draftChangePath = AbsoluteProjectPath(version.draftChangePath);
            var localGapsPath = Path.Combine(draftChangePath, "gaps.json");
            var localGaps = File.Exists(localGapsPath)
                ? ReadGapArray(localGapsPath)
                : Array.Empty<SpecGap>();
            var localGraphPath = Path.Combine(draftChangePath, "dependencies.json");
            var localGraph = File.Exists(localGraphPath)
                ? JsonUtility.FromJson<DependencyGraph>(File.ReadAllText(localGraphPath, Encoding.UTF8))
                : null;
            var availableGaps = File.Exists(localGapsPath) ? localGaps : run.Gaps ?? Array.Empty<SpecGap>();
            var availableDependencies = localGraph?.edges?.ToList() ?? run.Dependencies ?? new List<DependencyEdge>();
            var gaps = availableGaps.Where(gap => gapIds.Length > 0
                ? gapIds.Contains(gap.id)
                : string.Equals(gap.capability, capability, StringComparison.OrdinalIgnoreCase)).ToList();
            var reviewIssues = BuildReviewIssues(
                capability,
                review?.reviewIssues,
                gaps,
                review?.verification?.differences);
            ApplyLocalizedReviewIssues(reviewPath, reviewIssues);
            var dependencyGaps = gaps.Where(item =>
                    string.Equals(item.type, "missing-dependency", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var dependencies = availableDependencies.Where(edge => dependencyIds.Length > 0
                ? dependencyIds.Contains(edge.id)
                : string.Equals(edge.from, capability, StringComparison.OrdinalIgnoreCase)).ToList();
            var canonicalTitle = Or(review?.title, ExtractMarkdownTitle(content, capability));
            var title = ResolveSpecDisplayTitle(capability, specPath, canonicalTitle);
            return new DesignImportSpec
            {
                VersionId = version.id,
                ChangeId = Or(version.changeId, Path.GetFileName(AbsoluteProjectPath(version.draftChangePath))),
                Capability = capability,
                Title = title,
                CanonicalTitle = canonicalTitle,
                Category = NormalizeCategory(Or(review?.category, "game-rule")),
                SpecPath = specPath,
                ReviewPath = reviewPath,
                SpecContent = content,
                DraftChangePath = Directory.Exists(draftChangePath)
                    ? draftChangePath
                    : null,
                Readiness = Or(review?.readiness, node?.readiness),
                VerificationStatus = review?.verification?.status,
                VerificationSummary = review?.verification?.summary,
                VerificationEvidence = BuildCodeEvidence(review?.verification, review?.sourceReferences, canonicalTitle).ToList(),
                VerificationDifferences = review?.verification?.differences?.ToList() ?? new List<string>(),
                VerifiedAt = review?.verification?.verifiedAt,
                SourceReferences = review?.sourceReferences?.ToList() ?? new List<string>(),
                Gaps = dependencyGaps,
                Dependencies = dependencies,
                ReviewIssues = reviewIssues,
                EditorGuidance = review?.editorGuidance,
                PairedFeatureCapability = review?.pairedFeatureCapability,
                PairedRuleCapability = review?.pairedRuleCapability
            };
        }

        private static List<ReviewIssue> BuildReviewIssues(
            string capability,
            IEnumerable<ReviewIssue> persisted,
            IEnumerable<SpecGap> gaps,
            IEnumerable<string> differences)
        {
            var result = (persisted ?? Array.Empty<ReviewIssue>())
                .Where(item => item != null)
                .ToList();
            foreach (var issue in result)
            {
                issue.severity = NormalizeIssueSeverity(issue.severity);
                if (issue.severity == "blocking" &&
                    string.Equals(issue.status, "accepted", StringComparison.OrdinalIgnoreCase))
                    issue.status = "open";
            }
            foreach (var gap in gaps ?? Array.Empty<SpecGap>())
            {
                if (result.Any(item => string.Equals(item.sourceId, gap.id, StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(item.id, gap.id, StringComparison.OrdinalIgnoreCase)))
                    continue;
                var type = string.Equals(gap.type, "requirement-ambiguity", StringComparison.OrdinalIgnoreCase)
                    ? "design-conflict"
                    : "dependency-missing";
                var severity = NormalizeIssueSeverity(gap.severity);
                var status = Or(gap.status, "open");
                if (severity == "blocking" && string.Equals(status, "accepted", StringComparison.OrdinalIgnoreCase))
                    status = "open";
                result.Add(new ReviewIssue
                {
                    id = "ISSUE-" + SafePathSegment(gap.id).ToUpperInvariant(),
                    type = type,
                    severity = severity,
                    status = status,
                    blocksApproval = true,
                    summary = Or(gap.summary, gap.requirement),
                    details = Or(gap.impact, gap.recommendation),
                    sourceId = gap.id,
                    acceptedBy = gap.acceptedBy,
                    acceptedAt = gap.acceptedAt,
                    acceptanceNote = gap.userRationale
                });
            }

            var index = 0;
            foreach (var difference in differences ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(difference) || result.Any(item =>
                        item.type == "implementation-delta" &&
                        string.Equals(item.summary, difference, StringComparison.Ordinal)))
                    continue;
                result.Add(new ReviewIssue
                {
                    id = $"ISSUE-{SafePathSegment(capability).ToUpperInvariant()}-IMPLEMENTATION-{++index:000}",
                    type = "implementation-delta",
                    severity = "info",
                    status = "open",
                    blocksApproval = false,
                    summary = difference,
                    details = string.Empty,
                    sourceId = $"verification:{index}"
                });
            }
            return result;
        }

        private static string NormalizeIssueSeverity(string severity) => severity?.ToLowerInvariant() switch
        {
            "error" => "blocking",
            "critical" => "blocking",
            "blocking" => "blocking",
            "warning" => "warning",
            _ => "info"
        };

        private List<DesignImportSpec> LoadImportSpecs(
            DesignImportRun run,
            IReadOnlyDictionary<string, DependencyNode> nodes)
        {
            var result = new List<DesignImportSpec>();
            var specsRoot = Path.Combine(run.DirectoryPath, "specs");
            if (!Directory.Exists(specsRoot))
                return result;

            foreach (var specPath in Directory.GetFiles(
                         specsRoot,
                         "spec.md",
                         SearchOption.AllDirectories).OrderBy(path => path))
            {
                var capability = Path.GetFileName(Path.GetDirectoryName(specPath));
                var content = File.ReadAllText(specPath, Encoding.UTF8);
                var reviewPath = Path.Combine(
                    Path.GetDirectoryName(specPath) ?? specsRoot,
                    "spec-review.json");
                ImportSpecReview review = null;
                if (File.Exists(reviewPath))
                {
                    try
                    {
                        review = JsonUtility.FromJson<ImportSpecReview>(
                            File.ReadAllText(reviewPath, Encoding.UTF8));
                    }
                    catch
                    {
                        // 兼容旧导入：结构化 review 损坏时仍展示 spec 与中央 metadata。
                    }
                }

                var gapIds = review?.gapIds ?? Array.Empty<string>();
                var dependencyIds = review?.dependencyIds ?? Array.Empty<string>();
                var gaps = (run.Gaps ?? Array.Empty<SpecGap>())
                    .Where(gap => gapIds.Length > 0
                        ? gapIds.Contains(gap.id)
                        : string.Equals(gap.capability, capability, StringComparison.Ordinal))
                    .ToList();
                var dependencies = (run.Dependencies ?? new List<DependencyEdge>())
                    .Where(edge => dependencyIds.Length > 0
                        ? dependencyIds.Contains(edge.id)
                        : string.Equals(edge.from, capability, StringComparison.Ordinal))
                    .ToList();

                nodes.TryGetValue(capability, out var node);
                var title = Or(review?.title, ExtractMarkdownTitle(content, capability));
                var item = new DesignImportSpec
                {
                    Capability = capability,
                    Title = title,
                    Category = NormalizeCategory(Or(review?.category, "game-rule")),
                    SpecPath = specPath,
                    SpecContent = content,
                    Readiness = Or(review?.readiness, node?.readiness),
                    VerificationStatus = review?.verification?.status,
                    VerificationSummary = review?.verification?.summary,
                    VerificationEvidence =
                        BuildCodeEvidence(review?.verification, review?.sourceReferences, title).ToList(),
                    VerificationDifferences =
                        review?.verification?.differences?.ToList() ?? new List<string>(),
                    VerifiedAt = review?.verification?.verifiedAt,
                    SourceReferences = review?.sourceReferences?.ToList() ?? new List<string>(),
                    Gaps = gaps,
                    Dependencies = dependencies,
                    ReviewIssues = BuildReviewIssues(
                        capability,
                        review?.reviewIssues,
                        gaps,
                        review?.verification?.differences),
                    EditorGuidance = review?.editorGuidance,
                    PairedFeatureCapability = review?.pairedFeatureCapability,
                    PairedRuleCapability = review?.pairedRuleCapability
                };
                ApplyLocalizedReviewIssues(reviewPath, item.ReviewIssues);
                var draftPath = Path.Combine(run.DirectoryPath, "draft-changes", ImportChangeId(run, item));
                item.DraftChangePath = Directory.Exists(draftPath) ? draftPath : null;
                result.Add(item);
            }

            return result;
        }

        private static string ExtractMarkdownTitle(string content, string fallback)
        {
            if (string.IsNullOrWhiteSpace(content))
                return fallback;
            var firstHeading = content.Split('\n')
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal));
            return string.IsNullOrWhiteSpace(firstHeading)
                ? fallback
                : firstHeading.Substring(2).Replace(" Specification", string.Empty).Trim();
        }

        private static QueueItem ParseItem(IReadOnlyList<string> lines, int start, int end)
        {
            var heading = lines[start].Trim();
            var priority = ExtractPriority(heading);
            var title = Regex.Replace(heading, @"^###\s*(\[优先级:\s*[^]]+\]\s*)?", string.Empty).Trim();
            var item = new QueueItem
            {
                StartLine = start,
                EndLine = end,
                Priority = priority,
                Title = title
            };

            for (var i = start + 1; i <= end; i++)
            {
                var match = FieldRegex.Match(lines[i]);
                if (!match.Success)
                    continue;

                var name = match.Groups["name"].Value.Trim();
                var value = match.Groups["value"].Value.Trim();
                switch (name)
                {
                    case "文件":
                        item.File = value;
                        break;
                    case "类型":
                        item.Type = value;
                        break;
                    case "描述":
                        item.Description = value;
                        break;
                    case "来源":
                        item.Source = value;
                        break;
                    case "状态":
                        item.Status = value;
                        break;
                    case "维护人":
                        item.Maintainer = value;
                        break;
                    case "维护时间":
                        item.MaintainedAt = value;
                        break;
                    case "维护备注":
                        item.MaintenanceNote = value;
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(item.Status))
                item.Status = "待处理";

            return item;
        }

        private void UpdateItem(QueueItem item, string status, bool updateMaintenanceFields)
        {
            if (string.IsNullOrWhiteSpace(_maintainer))
                _maintainer = "未注明";

            var lines = new List<string>(File.ReadAllLines(_queuePath, Encoding.UTF8));
            SetField(lines, item.StartLine, item.EndLine, "状态", status, afterField: "来源");

            if (updateMaintenanceFields)
            {
                SetField(lines, item.StartLine, item.EndLine, "维护人", _maintainer, afterField: "状态");
                SetField(lines, item.StartLine, item.EndLine, "维护时间", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), afterField: "维护人");
                SetField(lines, item.StartLine, item.EndLine, "维护备注", string.IsNullOrWhiteSpace(_maintenanceNote) ? "-" : _maintenanceNote, afterField: "维护时间");
            }

            File.WriteAllLines(_queuePath, lines, Encoding.UTF8);
            AssetDatabase.Refresh();
            Reload();
        }

        private void AcceptGap(SpecGap gap)
        {
            if (string.IsNullOrWhiteSpace(_maintainer))
                _maintainer = "未注明";

            gap.status = "accepted";
            gap.userRationale = _gapRationale.Trim();
            gap.deliveryBoundary = _gapDeliveryBoundary.Trim();
            gap.implementationImpact = _gapImplementationImpact.Trim();
            gap.acceptedBy = _maintainer;
            gap.acceptedAt = DateTime.Now.ToString("o");

            Directory.CreateDirectory(_specMetadataPath);
            var json = JsonUtility.ToJson(new SpecGapList { items = _specGaps.ToArray() });
            const string prefix = "{\"items\":";
            var arrayJson = json.StartsWith(prefix, StringComparison.Ordinal) && json.EndsWith("}", StringComparison.Ordinal)
                ? json.Substring(prefix.Length, json.Length - prefix.Length - 1)
                : "[]";
            File.WriteAllText(Path.Combine(_specMetadataPath, "gaps.json"), arrayJson + Environment.NewLine, Encoding.UTF8);

            _gapRationale = string.Empty;
            _gapDeliveryBoundary = string.Empty;
            _gapImplementationImpact = string.Empty;
            AssetDatabase.Refresh();
            ReloadOpenSpec();
        }

        private static void SetField(List<string> lines, int start, int end, string fieldName, string value, string afterField)
        {
            var fieldLine = FindFieldLine(lines, start, end, fieldName);
            var content = $"- **{fieldName}**: {value}";
            if (fieldLine >= 0)
            {
                lines[fieldLine] = content;
                return;
            }

            var insertAfter = FindFieldLine(lines, start, end, afterField);
            var insertIndex = insertAfter >= 0 ? insertAfter + 1 : end + 1;
            lines.Insert(insertIndex, content);
        }

        private static int FindFieldLine(IReadOnlyList<string> lines, int start, int end, string fieldName)
        {
            var prefix = $"- **{fieldName}**:";
            for (var i = start + 1; i <= end && i < lines.Count; i++)
            {
                if (lines[i].StartsWith(prefix, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static int FindLine(IReadOnlyList<string> lines, Predicate<string> predicate, int start = 0)
        {
            for (var i = Math.Max(0, start); i < lines.Count; i++)
            {
                if (predicate(lines[i]))
                    return i;
            }

            return -1;
        }

        private static string ExtractPriority(string heading)
        {
            var match = Regex.Match(heading, @"\[优先级:\s*(?<priority>[^]]+)\]");
            return match.Success ? match.Groups["priority"].Value.Trim() : "-";
        }

        private static string ResolveDefaultMaintainer(string projectRoot)
        {
            var userName = Environment.UserName;
            var maintainersPath = Path.Combine(projectRoot, ".agent-memory", "team", "MAINTAINERS.md");
            if (File.Exists(maintainersPath) && TryResolveMaintainerFromTable(maintainersPath, userName, out var maintainer))
                return maintainer;

            return string.IsNullOrWhiteSpace(userName) ? "codex" : userName;
        }

        private static bool TryResolveMaintainerFromTable(string path, string key, out string maintainer)
        {
            maintainer = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                if (!line.StartsWith("|", StringComparison.Ordinal) || line.Contains("---"))
                    continue;

                var cells = line.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
                if (cells.Length < 2)
                    continue;

                if (string.Equals(cells[0], key, StringComparison.OrdinalIgnoreCase))
                {
                    maintainer = cells[1];
                    return !string.IsNullOrWhiteSpace(maintainer);
                }
            }

            return false;
        }

        private void SelectDocument(string path)
        {
            if (!File.Exists(path) || !TryLeaveRawEditor())
                return;
            _selectedSpec = _specFiles.FirstOrDefault(spec =>
                string.Equals(spec.Path, path, StringComparison.OrdinalIgnoreCase));
            if (_selectedSpec == null)
                return;

            _specCategoryTab = _selectedSpec.Category switch
            {
                "architecture" => SpecCategoryTab.System,
                "system" => SpecCategoryTab.System,
                "feature" => SpecCategoryTab.Feature,
                "game-rule" => SpecCategoryTab.GameRule,
                _ => SpecCategoryTab.All
            };
            _specFolderFilterIndex = 0;
            _openSpecSection = OpenSpecSection.Specs;
            _specDetailScroll = Vector2.zero;
        }

        private static GUIStyle ReadinessStyle(string readiness)
        {
            var style = new GUIStyle(EditorStyles.miniBoldLabel);
            if (readiness == "blocked-by-design" || readiness == "blocked-by-integration")
                style.normal.textColor = new Color(0.9f, 0.25f, 0.2f);
            else if (readiness == "ready-with-deferred-gaps")
                style.normal.textColor = new Color(0.95f, 0.6f, 0.1f);
            else if (readiness == "ready" || readiness == "implemented")
                style.normal.textColor = new Color(0.2f, 0.7f, 0.3f);
            return style;
        }

        private static string Or(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static void OpenPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return;
            }

            if (Directory.Exists(fullPath))
            {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
                EditorUtility.RevealInFinder(fullPath);
#else
                EditorUtility.OpenWithDefaultApp(fullPath);
#endif
                return;
            }

            if (File.Exists(fullPath))
                EditorUtility.OpenWithDefaultApp(fullPath);
        }

        private void OpenMarkdownDocument(string path) => OpenMarkdownDocument(path, 0);

        private void OpenMarkdownDocument(string path, int line)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;
            var application = _config?.markdownApplicationPath?.Trim();
            if (string.IsNullOrWhiteSpace(application))
            {
                EditorUtility.OpenWithDefaultApp(path);
                return;
            }

            try
            {
                var arguments = MarkdownApplicationArguments(application, path, line);
#if UNITY_EDITOR_OSX
                var isApplicationBundle = application.EndsWith(".app", StringComparison.OrdinalIgnoreCase);
                var startInfo = isApplicationBundle
                    ? new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/usr/bin/open",
                        Arguments = $"-a {QuoteProcessArgument(application)} --args {arguments}",
                        UseShellExecute = false
                    }
                    : new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = application,
                        Arguments = arguments,
                        UseShellExecute = false
                    };
#else
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = application,
                    Arguments = arguments,
                    UseShellExecute = true
                };
#endif
                System.Diagnostics.Process.Start(startInfo);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"无法使用配置的 Markdown 软件打开文件：{exception.Message}");
                EditorUtility.OpenWithDefaultApp(path);
            }
        }

        private static string MarkdownApplicationArguments(string application, string path, int line)
        {
            var executable = Path.GetFileNameWithoutExtension(application ?? string.Empty).ToLowerInvariant();
            if (line > 0 && (executable.Contains("code") || executable.Contains("cursor") || executable.Contains("windsurf")))
                return $"--goto {QuoteProcessArgument(path + ":" + line)}";
            if (line > 0 && executable.Contains("notepad++"))
                return $"-n{line} {QuoteProcessArgument(path)}";
            if (line > 0 && executable.Contains("sublime"))
                return QuoteProcessArgument(path + ":" + line);
            if (line > 0 && executable.Contains("rider"))
                return $"--line {line} {QuoteProcessArgument(path)}";
            return QuoteProcessArgument(path);
        }

        private static string QuoteProcessArgument(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

        private static string ApplicationDirectory(string applicationPath)
        {
            if (string.IsNullOrWhiteSpace(applicationPath))
                return string.Empty;
            try
            {
                return File.Exists(applicationPath)
                    ? Path.GetDirectoryName(applicationPath) ?? string.Empty
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string RelativeTo(string root, string path)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
                return path;

            try
            {
#if UNITY_2021_2_OR_NEWER
                return Path.GetRelativePath(root, path).Replace('\\', '/');
#else
                var rootPath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                               Path.DirectorySeparatorChar;
                var rootUri = new Uri(rootPath);
                var pathUri = new Uri(Path.GetFullPath(path));
                return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString())
                    .Replace('\\', '/');
#endif
            }
            catch
            {
                return path;
            }
        }

    }
}
#endif
