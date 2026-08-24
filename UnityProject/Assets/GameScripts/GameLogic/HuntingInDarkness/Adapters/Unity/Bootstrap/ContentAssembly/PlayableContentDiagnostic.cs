using System;
using System.Collections.Generic;
using System.Linq;

namespace HuntingInDarkness.Bootstrap
{
    public enum PlayableContentDiagnosticSeverity
    {
        Error,
        Warning
    }

    public readonly struct PlayableContentDiagnostic
    {
        public PlayableContentDiagnostic(PlayableContentDiagnosticSeverity severity, string code, string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public PlayableContentDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public bool IsError => Severity == PlayableContentDiagnosticSeverity.Error;

        public override string ToString() => $"[{Severity}] {Code}: {Message}";
    }

    public sealed class PlayableContentDiagnosticReport
    {
        private readonly List<PlayableContentDiagnostic> diagnostics = new();

        public IReadOnlyList<PlayableContentDiagnostic> Diagnostics => diagnostics;
        public bool IsValid => diagnostics.All(diagnostic => !diagnostic.IsError);
        public bool HasErrors => diagnostics.Any(diagnostic => diagnostic.IsError);

        internal void AddError(string code, string message) => diagnostics.Add(new PlayableContentDiagnostic(PlayableContentDiagnosticSeverity.Error, code, message));
        internal void AddWarning(string code, string message) => diagnostics.Add(new PlayableContentDiagnostic(PlayableContentDiagnosticSeverity.Warning, code, message));

        public override string ToString()
        {
            if (diagnostics.Count == 0) return "内容装配诊断通过。";
            return string.Join(Environment.NewLine, diagnostics);
        }
    }
}
