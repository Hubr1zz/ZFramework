using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class ApiPolicy
{
    private static readonly string[] BannedNamespaces =
    {
        "System.Diagnostics",
        "System.IO",
        "System.Net",
        "System.Reflection",
        "System.Runtime.InteropServices",
        "Microsoft.Win32"
    };

    private static readonly HashSet<string> BannedTypes = new(StringComparer.Ordinal)
    {
        "System.AppDomain",
        "System.Environment",
        "System.GC"
    };

    public static IReadOnlyList<string> FindViolations(CSharpCompilation compilation)
    {
        var violations = new List<string>();
        var reportedLocations = new HashSet<string>(StringComparer.Ordinal);
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (IdentifierNameSyntax identifier in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                ISymbol? symbol = model.GetSymbolInfo(identifier).Symbol;
                if (symbol == null) continue;
                INamedTypeSymbol? type = symbol as INamedTypeSymbol ?? symbol.ContainingType;
                string typeName = type?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? string.Empty;
                string namespaceName = (symbol as INamespaceSymbol)?.ToDisplayString() ??
                                       type?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                if (!BannedTypes.Contains(typeName) && !IsBannedNamespace(namespaceName)) continue;

                FileLinePositionSpan span = tree.GetLineSpan(identifier.Span);
                string location = $"{tree.FilePath}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1})";
                if (reportedLocations.Add(location))
                    violations.Add($"{location}: RTS001 API '{symbol.ToDisplayString()}' is blocked by the Editor-safe policy.");
            }
        }
        return violations;
    }

    private static bool IsBannedNamespace(string namespaceName)
    {
        for (int i = 0; i < BannedNamespaces.Length; i++)
        {
            string banned = BannedNamespaces[i];
            if (namespaceName.Equals(banned, StringComparison.Ordinal) ||
                namespaceName.StartsWith(banned + ".", StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
