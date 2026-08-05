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
    public sealed partial class AgentWorkbenchWindow
    {
        private const string SourceLanguage = "source";
        private LocalizationConfiguration _localizationConfiguration = new();
        private readonly Dictionary<string, TranslationRecord> _translationRecords =
            new(StringComparer.OrdinalIgnoreCase);

        [Serializable]
        private sealed class LocalizationConfiguration
        {
            public int schemaVersion = 2;
            public string generationLanguage = SourceLanguage;
            public LocalizedSpecTitle[] specTitles;
        }

        [Serializable]
        private sealed class LocalizedSpecTitle
        {
            public string capability;
            public string zhCN;
            public string enUS;
        }

        [Serializable]
        private sealed class TranslationManifest
        {
            public int schemaVersion = 1;
            public TranslationRecord[] entries;
        }

        [Serializable]
        private sealed class TranslationRecord
        {
            public string sourcePath;
            public string targetPath;
            public string authoritativeLanguage;
            public string targetLanguage;
            public string sourceHash;
            public string translatedHash;
            public TranslationBlock[] blocks;
        }

        [Serializable]
        private sealed class TranslationBlock
        {
            public string id;
            public string kind;
            public string sourceHash;
            public string translatedHash;
        }

        [Serializable]
        private sealed class LocalizedReviewContainer
        {
            public string title;
            public ImportSpecVerification verification;
            public ReviewIssue[] reviewIssues;
        }

        private enum LocalizedDocumentState
        {
            Authority,
            Current,
            Missing,
            Stale
        }

        private sealed class LocalizedDocument
        {
            public LocalizedDocumentState State;
            public string Content;
            public string TargetLanguage;
        }

        private string LocalizationConfigurationPath =>
            Path.Combine(_openSpecPath, "localization.json");

        private string TranslationManifestPath =>
            Path.Combine(_openSpecPath, "translations", "manifest.json");

        private void LoadLocalizationState()
        {
            _localizationConfiguration = new LocalizationConfiguration();
            if (File.Exists(LocalizationConfigurationPath))
            {
                try
                {
                    _localizationConfiguration =
                        JsonUtility.FromJson<LocalizationConfiguration>(
                            File.ReadAllText(LocalizationConfigurationPath, Encoding.UTF8)) ??
                        new LocalizationConfiguration();
                }
                catch
                {
                    _localizationConfiguration = new LocalizationConfiguration();
                }
            }
            _localizationConfiguration.schemaVersion = 2;
            _localizationConfiguration.generationLanguage = NormalizeGenerationLanguage(
                _localizationConfiguration.generationLanguage);
            _localizationConfiguration.specTitles ??= Array.Empty<LocalizedSpecTitle>();

            _translationRecords.Clear();
            if (!File.Exists(TranslationManifestPath))
                return;
            try
            {
                var manifest = JsonUtility.FromJson<TranslationManifest>(
                    File.ReadAllText(TranslationManifestPath, Encoding.UTF8));
                foreach (var entry in manifest?.entries ?? Array.Empty<TranslationRecord>())
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.sourcePath) ||
                        string.IsNullOrWhiteSpace(entry.targetLanguage))
                        continue;
                    _translationRecords[TranslationKey(entry.sourcePath, entry.targetLanguage)] = entry;
                }
            }
            catch
            {
                // A broken optional display manifest must not block canonical OpenSpec data.
            }
        }

        private void SaveLocalizationConfiguration()
        {
            _localizationConfiguration.schemaVersion = 2;
            _localizationConfiguration.specTitles ??= Array.Empty<LocalizedSpecTitle>();
            Directory.CreateDirectory(_openSpecPath);
            File.WriteAllText(
                LocalizationConfigurationPath,
                JsonUtility.ToJson(_localizationConfiguration, true) + Environment.NewLine,
                Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        private static string NormalizeGenerationLanguage(string value) => value switch
        {
            "zh-CN" => "zh-CN",
            "en-US" => "en-US",
            _ => SourceLanguage
        };

        private string CurrentWorkbenchLanguage =>
            _config?.currentLanguage == "en-US" ? "en-US" : "zh-CN";

        private string ResolveSpecDisplayTitle(
            string capability,
            string canonicalPath,
            string canonicalTitle)
        {
            if (string.IsNullOrWhiteSpace(canonicalPath) || !File.Exists(canonicalPath))
                return Or(ConfiguredSpecTitle(capability, CurrentWorkbenchLanguage), canonicalTitle);

            var canonicalContent = File.ReadAllText(canonicalPath, Encoding.UTF8);
            var localized = ResolveLocalizedDocument(canonicalPath, canonicalContent);
            if (localized.State == LocalizedDocumentState.Authority)
                return canonicalTitle;

            var configured = ConfiguredSpecTitle(capability, CurrentWorkbenchLanguage);
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            return localized.State == LocalizedDocumentState.Current
                ? ExtractMarkdownTitle(localized.Content, canonicalTitle)
                : canonicalTitle;
        }

        private bool CurrentLanguageIsAuthority(string canonicalPath)
        {
            if (string.IsNullOrWhiteSpace(canonicalPath) || !File.Exists(canonicalPath))
                return true;
            var content = File.ReadAllText(canonicalPath, Encoding.UTF8);
            return ResolveLocalizedDocument(canonicalPath, content).State == LocalizedDocumentState.Authority;
        }

        private string ConfiguredSpecTitle(string capability, string language)
        {
            if (string.IsNullOrWhiteSpace(capability))
                return string.Empty;
            var entry = (_localizationConfiguration?.specTitles ?? Array.Empty<LocalizedSpecTitle>())
                .FirstOrDefault(item => item != null && string.Equals(
                    item.capability,
                    capability,
                    StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                return string.Empty;
            return language == "en-US" ? entry.enUS : entry.zhCN;
        }

        private void SaveSpecTitle(string capability, string language, string title)
        {
            if (string.IsNullOrWhiteSpace(capability) || string.IsNullOrWhiteSpace(title))
                return;
            var entries = (_localizationConfiguration.specTitles ?? Array.Empty<LocalizedSpecTitle>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.capability))
                .ToList();
            var entry = entries.FirstOrDefault(item => string.Equals(
                item.capability,
                capability,
                StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                entry = new LocalizedSpecTitle { capability = capability };
                entries.Add(entry);
            }
            if (language == "en-US")
                entry.enUS = title;
            else
                entry.zhCN = title;
            _localizationConfiguration.specTitles = entries
                .OrderBy(item => item.capability, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            SaveLocalizationConfiguration();
        }

        private LocalizedDocument ResolveLocalizedDocument(string canonicalPath, string canonicalContent)
        {
            var requestedLanguage = _config?.currentLanguage == "en-US" ? "en-US" : "zh-CN";
            var relativePath = RelativeTo(_openSpecPath, canonicalPath).Replace('\\', '/');
            var record = _translationRecords.TryGetValue(
                TranslationKey(relativePath, requestedLanguage),
                out var candidate)
                ? candidate
                : null;
            var authorityLanguage = NormalizeDocumentLanguage(record?.authoritativeLanguage);
            if (string.IsNullOrWhiteSpace(authorityLanguage))
                authorityLanguage = DetectDocumentLanguage(canonicalPath, canonicalContent);
            if (string.Equals(authorityLanguage, requestedLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return new LocalizedDocument
                {
                    State = LocalizedDocumentState.Authority,
                    Content = canonicalContent,
                    TargetLanguage = requestedLanguage
                };
            }

            if (record == null || string.IsNullOrWhiteSpace(record.targetPath))
            {
                return new LocalizedDocument
                {
                    State = LocalizedDocumentState.Missing,
                    TargetLanguage = requestedLanguage
                };
            }

            var translatedPath = AbsoluteProjectPath(record.targetPath);
            if (!File.Exists(translatedPath))
            {
                return new LocalizedDocument
                {
                    State = LocalizedDocumentState.Missing,
                    TargetLanguage = requestedLanguage
                };
            }

            var sourceHash = Sha256(canonicalContent ?? string.Empty);
            if (!string.Equals(sourceHash, record.sourceHash, StringComparison.OrdinalIgnoreCase))
            {
                return new LocalizedDocument
                {
                    State = LocalizedDocumentState.Stale,
                    TargetLanguage = requestedLanguage
                };
            }

            var translated = File.ReadAllText(translatedPath, Encoding.UTF8);
            if (!string.IsNullOrWhiteSpace(record.translatedHash) &&
                !string.Equals(Sha256(translated), record.translatedHash, StringComparison.OrdinalIgnoreCase))
            {
                return new LocalizedDocument
                {
                    State = LocalizedDocumentState.Stale,
                    TargetLanguage = requestedLanguage
                };
            }

            return new LocalizedDocument
            {
                State = LocalizedDocumentState.Current,
                Content = translated,
                TargetLanguage = requestedLanguage
            };
        }

        private void DrawLocalizedMarkdown(string canonicalPath, string canonicalContent, float width)
        {
            var localized = ResolveLocalizedDocument(canonicalPath, canonicalContent);
            if (localized.State == LocalizedDocumentState.Authority ||
                localized.State == LocalizedDocumentState.Current)
            {
                DrawMarkdown(localized.Content, width);
                return;
            }
            DrawTranslationNotice(canonicalPath, localized.State, localized.TargetLanguage);
        }

        private void ApplyLocalizedReviewIssues(string canonicalPath, IEnumerable<ReviewIssue> canonicalIssues)
        {
            var issues = canonicalIssues?.Where(item => item != null).ToList();
            if (issues == null || issues.Count == 0 || !File.Exists(canonicalPath))
                return;
            var canonicalContent = File.ReadAllText(canonicalPath, Encoding.UTF8);
            var localized = ResolveLocalizedDocument(canonicalPath, canonicalContent);
            if (localized.State != LocalizedDocumentState.Current || string.IsNullOrWhiteSpace(localized.Content))
                return;
            try
            {
                var translated = JsonUtility.FromJson<LocalizedReviewContainer>(localized.Content);
                foreach (var translatedIssue in translated?.reviewIssues ?? Array.Empty<ReviewIssue>())
                {
                    var canonical = issues.FirstOrDefault(item => string.Equals(
                        item.id,
                        translatedIssue.id,
                        StringComparison.OrdinalIgnoreCase));
                    if (canonical == null)
                        continue;
                    canonical.displaySummary = translatedIssue.summary;
                    canonical.displayDetails = translatedIssue.details;
                }
            }
            catch
            {
                // Invalid translated JSON is display-only and cannot corrupt canonical review state.
            }
        }

        private bool DrawStructuredTranslationGate(string canonicalPath)
        {
            if (string.IsNullOrWhiteSpace(canonicalPath) || !File.Exists(canonicalPath))
                return true;
            var canonicalContent = File.ReadAllText(canonicalPath, Encoding.UTF8);
            var localized = ResolveLocalizedDocument(canonicalPath, canonicalContent);
            if (localized.State == LocalizedDocumentState.Authority ||
                localized.State == LocalizedDocumentState.Current)
                return true;
            DrawTranslationNotice(canonicalPath, localized.State, localized.TargetLanguage);
            return false;
        }

        private static string ReviewSummary(ReviewIssue issue) =>
            string.IsNullOrWhiteSpace(issue?.displaySummary) ? issue?.summary : issue.displaySummary;

        private static string ReviewDetails(ReviewIssue issue) =>
            string.IsNullOrWhiteSpace(issue?.displayDetails) ? issue?.details : issue.displayDetails;

        private string LocalizedVerificationSummary(string canonicalPath, string fallback)
        {
            if (string.IsNullOrWhiteSpace(canonicalPath) || !File.Exists(canonicalPath))
                return fallback;
            var localized = ResolveLocalizedDocument(
                canonicalPath,
                File.ReadAllText(canonicalPath, Encoding.UTF8));
            if (localized.State != LocalizedDocumentState.Current)
                return fallback;
            try
            {
                var translated = JsonUtility.FromJson<LocalizedReviewContainer>(localized.Content);
                return Or(translated?.verification?.summary, fallback);
            }
            catch
            {
                return fallback;
            }
        }

        private bool CanEditCanonicalInCurrentLanguage(string canonicalPath, string canonicalContent)
        {
            var localized = ResolveLocalizedDocument(canonicalPath, canonicalContent);
            if (localized.State == LocalizedDocumentState.Authority)
                return true;
            DrawTranslationNotice(canonicalPath, localized.State, localized.TargetLanguage, true);
            return false;
        }

        private void DrawTranslationNotice(
            string canonicalPath,
            LocalizedDocumentState state,
            string targetLanguage,
            bool editingBlocked = false)
        {
            var messageId = state == LocalizedDocumentState.Stale
                ? "translation.stale"
                : "translation.missing";
            if (editingBlocked && state == LocalizedDocumentState.Current)
                messageId = "translation.readOnly";
            EditorGUILayout.HelpBox(L(messageId), MessageType.Warning);
            if (GUILayout.Button(
                    L("translation.copyCommand"),
                    ReportActionButtonStyle(),
                    GUILayout.Height(30)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildTranslationCommand(canonicalPath, targetLanguage);
                ShowNotification(new GUIContent(L("translation.commandCopied")));
            }
        }

        private string BuildTranslationCommand(string canonicalPath, string targetLanguage)
        {
            var relative = RelativeTo(_openSpecPath, canonicalPath).Replace('\\', '/');
            var language = targetLanguage == "en-US" ? "英文" : "中文";
            return $"翻译现有Spec：{language} {relative}";
        }

        private static string TranslationKey(string relativePath, string language) =>
            $"{(relativePath ?? string.Empty).Replace('\\', '/').TrimStart('/')}|{language}";

        private static string NormalizeDocumentLanguage(string value) => value switch
        {
            "zh" => "zh-CN",
            "zh-CN" => "zh-CN",
            "en" => "en-US",
            "en-US" => "en-US",
            _ => string.Empty
        };

        private static string DetectDocumentLanguage(string path, string content)
        {
            if (string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
            {
                var directory = Path.GetDirectoryName(path) ?? string.Empty;
                var markdown = new[] { "spec.md", "proposal.md", "design.md", "tasks.md" }
                    .Select(name => Path.Combine(directory, name))
                    .Where(File.Exists)
                    .Select(candidate => File.ReadAllText(candidate, Encoding.UTF8))
                    .ToArray();
                if (markdown.Length > 0)
                    content = string.Join("\n", markdown);
            }
            return DetectTextLanguage(content);
        }

        private static string DetectTextLanguage(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "zh-CN";
            var cjk = Regex.Matches(content, @"[\u3400-\u9fff]").Count;
            var latinWords = Regex.Matches(content, @"\b[A-Za-z]{2,}\b").Count;
            return cjk >= 2 && cjk * 5 >= latinWords ? "zh-CN" : "en-US";
        }

        private static string Sha256(string content)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(content ?? string.Empty))
                .Select(value => value.ToString("x2")));
        }
    }
}
#endif
