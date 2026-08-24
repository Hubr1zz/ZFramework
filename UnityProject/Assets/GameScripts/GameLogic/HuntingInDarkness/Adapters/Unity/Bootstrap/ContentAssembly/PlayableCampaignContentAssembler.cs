using System.Collections.Generic;
using HuntingInDarkness.Hunt;
using UnityEngine;

namespace HuntingInDarkness.Bootstrap
{
    public static class PlayableCampaignContentAssembler
    {
        private static PlayableCampaignContentCandidate installedCandidate;
        private static bool installationFailed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            installedCandidate = null;
            installationFailed = false;
        }

        public static bool TryBuild(PlayableBootstrapSettings settings, out PlayableCampaignContentCandidate candidate, out PlayableContentDiagnosticReport report)
        {
            candidate = null;
            report = new PlayableContentDiagnosticReport();
            if (settings == null)
            {
                report.AddError("bootstrap.missing", "缺少可游玩启动配置。");
                return false;
            }
            if (!settings.CanCreateGame)
                report.AddError("bootstrap.incomplete", "启动配置缺少必需的营地、狩猎或角色内容。");
            ValidateHuntContent(settings, report);
            if (report.HasErrors) return false;
            candidate = new PlayableCampaignContentCandidate(settings);
            return true;
        }

        public static bool Install(PlayableCampaignContentCandidate candidate, out PlayableContentDiagnosticReport report)
        {
            report = new PlayableContentDiagnosticReport();
            if (candidate == null)
            {
                report.AddError("candidate.missing", "战役内容候选为空。");
                return false;
            }
            if (ReferenceEquals(installedCandidate, candidate)) return true;
            if (installedCandidate != null || installationFailed)
            {
                report.AddError("candidate.install.gate", "当前进程已经提交过另一批内容，或此前安装未能安全完成。");
                return false;
            }
            try
            {
                if (!candidate.TryInstall(out string reason) || !candidate.TryValidateInstalledContent(out reason))
                {
                    installationFailed = true;
                    report.AddError("candidate.install", reason);
                    return false;
                }
                installedCandidate = candidate;
                return true;
            }
            catch (System.Exception exception)
            {
                installationFailed = true;
                report.AddError("candidate.install.exception", exception.Message);
                return false;
            }
        }

        private static void ValidateHuntContent(PlayableBootstrapSettings settings, PlayableContentDiagnosticReport report)
        {
            string defaultReason = string.Empty;
            if (settings.HuntContent == null || !settings.HuntContent.IsAvailableForYear(1, out defaultReason))
                report.AddError("hunt.default.invalid", string.IsNullOrWhiteSpace(defaultReason) ? "默认狩猎内容无效。" : defaultReason);
            if (settings.HuntDestinations == null)
            {
                report.AddError("hunt.destinations.missing", "缺少狩猎目的地目录。");
                return;
            }

            var knownIds = new HashSet<string>(System.StringComparer.Ordinal);
            IReadOnlyList<PlayableHuntDestination> destinations = settings.HuntDestinations.Destinations;
            if (destinations == null || destinations.Count == 0)
            {
                report.AddError("hunt.destinations.empty", "狩猎目的地目录为空。");
                return;
            }
            foreach (PlayableHuntDestination destination in destinations)
            {
                if (destination == null)
                {
                    report.AddError("hunt.destination.invalid", "狩猎目的地含空白或未配置记录。");
                    continue;
                }
                string destinationId = destination.DestinationId?.Trim() ?? string.Empty;
                if (destinationId.Length > 0 && !knownIds.Add(destinationId))
                    report.AddError("hunt.destination.id.duplicate", $"狩猎目的地稳定 ID 重复：{destinationId}");
                if (!destination.IsConfigured)
                {
                    report.AddError("hunt.destination.invalid", $"狩猎目的地未完整配置：{destination.DestinationId}");
                    continue;
                }
                if (!destination.IsAvailable(destination.MinimumYear, out string reason))
                    report.AddError("hunt.destination.unavailable", $"目的地 {destination.DestinationId} 在最早年份不可用：{reason}");
            }
        }
    }
}
