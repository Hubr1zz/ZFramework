using System.Collections.Generic;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Bootstrap
{
    public static class PlayableCampaignContentAssembler
    {
        private static PlayableCampaignContentCandidate installedCandidate;
        private static bool installationFailed;
        private static System.Func<string, bool> installationFailureProbe;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            installedCandidate = null;
            installationFailed = false;
            installationFailureProbe = null;
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
            PlayableCampaignRuntimeSnapshot runtimeSnapshot = null;
            PlayableEventTableGeneration stagedGeneration = null;
            PlayableEventTableGeneration previousGeneration = null;
            PlayableSettlementContentPlan stagedSettlementPlan = null;
            PlayableSettlementContentPlan previousSettlementPlan = null;
            bool generationPublished = false;
            bool settlementPlanPublished = false;
            try
            {
                runtimeSnapshot = new PlayableCampaignRuntimeSnapshot();
                stagedGeneration = PlayableEventTableRuntime.PrepareGeneration(candidate.Symptoms, PlayableBloodlineRuntime.Content);
                if (stagedGeneration.HasErrors)
                    return FailAndRollback(report, "candidate.events.invalid", stagedGeneration.Diagnostic, candidate, runtimeSnapshot, stagedGeneration, previousGeneration, generationPublished, stagedSettlementPlan, previousSettlementPlan, settlementPlanPublished);
                ThrowIfInstallationFailureRequested("after-event-prepare");
                if (!candidate.TryPrepareSettlementPlan(stagedGeneration.Events, out string reason))
                    return FailAndRollback(report, "candidate.settlement.invalid", reason, candidate, runtimeSnapshot, stagedGeneration, previousGeneration, generationPublished, stagedSettlementPlan, previousSettlementPlan, settlementPlanPublished);
                stagedSettlementPlan = candidate.SettlementPlan;
                ThrowIfInstallationFailureRequested("after-settlement-prepare");
                if (!candidate.TryInstallBindings(out reason))
                {
                    return FailAndRollback(report, "candidate.install", reason, candidate, runtimeSnapshot, stagedGeneration, previousGeneration, generationPublished, stagedSettlementPlan, previousSettlementPlan, settlementPlanPublished);
                }
                ThrowIfInstallationFailureRequested("after-runtime-bindings");
                previousGeneration = PlayableEventTableRuntime.SwapGeneration(stagedGeneration);
                generationPublished = true;
                ThrowIfInstallationFailureRequested("after-event-publish");
                previousSettlementPlan = PlayableSettlementContentRuntime.CurrentPlan;
                PlayableSettlementContentRuntime.SwapPlan(stagedSettlementPlan);
                settlementPlanPublished = true;
                ThrowIfInstallationFailureRequested("after-settlement-publish");
                if (!candidate.TryValidateInstalledContent(out reason))
                {
                    return FailAndRollback(report, "candidate.install", reason, candidate, runtimeSnapshot, stagedGeneration, previousGeneration, generationPublished, stagedSettlementPlan, previousSettlementPlan, settlementPlanPublished);
                }
                ThrowIfInstallationFailureRequested("after-settlement-projection");
                candidate.MarkInstalled();
                installedCandidate = candidate;
                try
                {
                    PlayableSettlementContentRuntime.RetirePlan(previousSettlementPlan);
                }
                catch (System.Exception planRetirementException)
                {
                    Debug.LogWarning($"[PlayableBootstrap] 旧营地内容世代回收失败，当前候选仍保持已提交：{planRetirementException.Message}");
                }
                try
                {
                    PlayableEventTableRuntime.RetireGeneration(previousGeneration);
                }
                catch (System.Exception eventRetirementException)
                {
                    Debug.LogWarning($"[PlayableBootstrap] 旧事件内容世代回收失败，当前候选仍保持已提交：{eventRetirementException.Message}");
                }
                return true;
            }
            catch (System.Exception exception)
            {
                return FailAndRollback(report, "candidate.install.exception", exception.Message, candidate, runtimeSnapshot, stagedGeneration, previousGeneration, generationPublished, stagedSettlementPlan, previousSettlementPlan, settlementPlanPublished);
            }
        }

        private static bool FailAndRollback(PlayableContentDiagnosticReport report, string code, string reason, PlayableCampaignContentCandidate candidate, PlayableCampaignRuntimeSnapshot runtimeSnapshot, PlayableEventTableGeneration stagedGeneration, PlayableEventTableGeneration previousGeneration, bool generationPublished, PlayableSettlementContentPlan stagedSettlementPlan, PlayableSettlementContentPlan previousSettlementPlan, bool settlementPlanPublished)
        {
            try
            {
                Rollback(candidate, runtimeSnapshot, stagedGeneration, previousGeneration, generationPublished, stagedSettlementPlan, previousSettlementPlan, settlementPlanPublished);
                report.AddError(code, reason);
            }
            catch (System.Exception rollbackException)
            {
                installationFailed = true;
                report.AddError("candidate.install.rollback", $"{reason}；回滚失败：{rollbackException.Message}");
            }
            return false;
        }

        private static void Rollback(PlayableCampaignContentCandidate candidate, PlayableCampaignRuntimeSnapshot runtimeSnapshot, PlayableEventTableGeneration stagedGeneration, PlayableEventTableGeneration previousGeneration, bool generationPublished, PlayableSettlementContentPlan stagedSettlementPlan, PlayableSettlementContentPlan previousSettlementPlan, bool settlementPlanPublished)
        {
            System.Exception firstException = null;
            bool stagedPlanIsCurrent = ReferenceEquals(PlayableSettlementContentRuntime.CurrentPlan, stagedSettlementPlan);
            PlayableSettlementContentPlan rejectedPlan = null;
            try
            {
                rejectedPlan = settlementPlanPublished || stagedPlanIsCurrent ? PlayableSettlementContentRuntime.SwapPlan(previousSettlementPlan) : stagedSettlementPlan;
            }
            catch (System.Exception exception)
            {
                firstException ??= exception;
            }
            candidate?.ReleaseSettlementPlan(stagedSettlementPlan);
            try
            {
                if (!ReferenceEquals(PlayableSettlementContentRuntime.CurrentPlan, rejectedPlan)) PlayableSettlementContentRuntime.RetirePlan(rejectedPlan);
            }
            catch (System.Exception exception)
            {
                firstException ??= exception;
            }
            try
            {
                PlayableEventTableGeneration rejectedGeneration = generationPublished ? PlayableEventTableRuntime.SwapGeneration(previousGeneration) : stagedGeneration;
                PlayableEventTableRuntime.RetireGeneration(rejectedGeneration);
            }
            catch (System.Exception exception)
            {
                firstException ??= exception;
            }
            try
            {
                runtimeSnapshot?.Restore();
            }
            catch (System.Exception exception)
            {
                firstException ??= exception;
            }
            if (firstException != null) throw firstException;
        }

        private static void ThrowIfInstallationFailureRequested(string stage)
        {
            if (installationFailureProbe?.Invoke(stage) == true)
                throw new System.InvalidOperationException($"测试请求在 {stage} 中断内容安装。");
        }

        private static void ValidateHuntContent(PlayableBootstrapSettings settings, PlayableContentDiagnosticReport report)
        {
            string defaultReason = string.Empty;
            if (settings.HuntContent == null || !settings.HuntContent.IsAvailableForYear(1, out defaultReason))
                report.AddError("hunt.default.invalid", string.IsNullOrWhiteSpace(defaultReason) ? "默认狩猎内容无效。" : defaultReason);
            else if (!settings.HuntContent.NoiseProfile.TryValidateContinuousCoverage(1, out int defaultMissingYear))
                report.AddError("hunt.default.year-gap", $"默认狩猎风险内容从第 {defaultMissingYear} 年起中断；无终止年战役必须连续覆盖到无限年份。");
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
                else if (!destination.HuntContent.NoiseProfile.TryValidateContinuousCoverage(destination.MinimumYear, out int destinationMissingYear))
                    report.AddError("hunt.destination.year-gap", $"目的地 {destination.DestinationId} 的风险内容从第 {destinationMissingYear} 年起中断。");
            }
        }
    }
}
