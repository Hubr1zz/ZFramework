using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

return args.Length == 1 && args[0] == "--server"
    ? CompilerServer.Run()
    : CompilerProgram.Run(args, new CompilerWorkspace());

internal static class CompilerServer
{
    public static int Run()
    {
        var workspace = new CompilerWorkspace();
        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            if (line == "shutdown") return 0;
            TextWriter originalOut = Console.Out, originalError = Console.Error;
            using var output = new StringWriter(); using var error = new StringWriter();
            int exit;
            try
            {
                string[] request = line.Split('\t').Select(x => Encoding.UTF8.GetString(Convert.FromBase64String(x))).ToArray();
                Console.SetOut(output); Console.SetError(error); exit = CompilerProgram.Run(request, workspace);
            }
            catch (Exception exception) { exit = 1; error.WriteLine(exception); }
            finally { Console.SetOut(originalOut); Console.SetError(originalError); }
            originalOut.WriteLine("@@RTS_RESULT\t" + exit + "\t" + Encode(output.ToString()) + "\t" + Encode(error.ToString()));
            originalOut.Flush();
        }
        return 0;
    }
    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
}

internal static class CompilerProgram
{
    public static int Run(string[] args, CompilerWorkspace workspace)
    {
        try
        {
            CompilerOptions options = CompilerOptions.Parse(args);
            Directory.CreateDirectory(options.OutputDirectory);
            string[] files = options.SourceDirectories
                .SelectMany(directory => Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (files.Length == 0)
                return Fail($"No .cs files found under: {string.Join(", ", options.SourceDirectories)}.");

            string generation = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string name = $"TEngine.RTS.UserScripts.g{generation}";
            string dllPath = Path.Combine(options.OutputDirectory, name + ".dll");
            string pdbPath = Path.Combine(options.OutputDirectory, name + ".pdb");
            Stopwatch compileTimer = Stopwatch.StartNew();
            CSharpCompilation compilation = workspace.Prepare(name, files, options.ReferencePaths,
                out int reparsedFiles, out int reusedFiles, out int reusedReferences);
            IReadOnlyList<string> policyViolations = ApiPolicy.FindViolations(compilation);
            if (policyViolations.Count > 0)
            {
                foreach (string violation in policyViolations) Console.Error.WriteLine(violation);
                return 1;
            }
            using FileStream dll = File.Create(dllPath);
            using FileStream pdb = File.Create(pdbPath);
            EmitResult result = compilation.Emit(dll, pdb,
                options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
            PrintDiagnostics(result.Diagnostics);
            if (!result.Success)
            {
                dll.Dispose(); pdb.Dispose(); File.Delete(dllPath); File.Delete(pdbPath); return 1;
            }
            Console.WriteLine($"@@RTS_METRIC total_ms={compileTimer.Elapsed.TotalMilliseconds:F1} reparsed={reparsedFiles} reused_trees={reusedFiles} reused_refs={reusedReferences}");
            Console.WriteLine(dllPath);
            return 0;
        }
        catch (Exception exception) { return Fail(exception.ToString()); }
    }

    internal static IEnumerable<string> ResolveNetStandardReferencePaths()
    {
        DirectoryInfo runtime = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory());
        string root = runtime.Parent?.Parent?.Parent?.FullName
            ?? throw new InvalidOperationException("Cannot locate the dotnet installation root.");
        string directory = Path.Combine(root, "packs", "NETStandard.Library.Ref", "2.1.0", "ref", "netstandard2.1");
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        return Directory.GetFiles(directory, "*.dll");
    }

    private static void PrintDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        foreach (Diagnostic diagnostic in diagnostics.Where(x => x.Severity >= DiagnosticSeverity.Warning))
            Console.Error.WriteLine(diagnostic);
    }

    private static int Fail(string message) { Console.Error.WriteLine(message); return 1; }
}

internal sealed class CompilerWorkspace
{
    private sealed record TreeEntry(string Hash, SyntaxTree Tree);
    private sealed record ReferenceEntry(long Length, long LastWriteTicks, MetadataReference Reference);

    private readonly Dictionary<string, TreeEntry> _trees = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReferenceEntry> _references = new(StringComparer.OrdinalIgnoreCase);
    private CSharpCompilation? _previous;
    private string _referenceSignature = string.Empty;
    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.CSharp9)
        .WithPreprocessorSymbols("TENGINE_RTS");
    private static readonly CSharpCompilationOptions CompilationOptions = new(
        OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Debug,
        deterministic: false, nullableContextOptions: NullableContextOptions.Disable);

    public CSharpCompilation Prepare(string assemblyName, IReadOnlyList<string> files,
        IReadOnlyList<string> explicitReferences, out int reparsed, out int reusedTrees, out int reusedReferences)
    {
        reparsed = 0;
        reusedTrees = 0;
        var currentTrees = new Dictionary<string, SyntaxTree>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in files)
        {
            string source = File.ReadAllText(path);
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
            if (_trees.TryGetValue(path, out TreeEntry? entry) && entry.Hash == hash)
            {
                currentTrees[path] = entry.Tree;
                reusedTrees++;
            }
            else
            {
                SyntaxTree tree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), ParseOptions, path);
                _trees[path] = new TreeEntry(hash, tree);
                currentTrees[path] = tree;
                reparsed++;
            }
        }
        foreach (string deleted in _trees.Keys.Except(files, StringComparer.OrdinalIgnoreCase).ToArray())
            _trees.Remove(deleted);

        string[] referencePaths = CompilerProgram.ResolveNetStandardReferencePaths()
            .Concat(explicitReferences).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var references = new List<MetadataReference>(referencePaths.Length);
        reusedReferences = 0;
        foreach (string path in referencePaths)
        {
            FileInfo info = new(path);
            if (_references.TryGetValue(path, out ReferenceEntry? entry) &&
                entry.Length == info.Length && entry.LastWriteTicks == info.LastWriteTimeUtc.Ticks)
            {
                references.Add(entry.Reference);
                reusedReferences++;
            }
            else
            {
                MetadataReference reference = MetadataReference.CreateFromFile(path);
                _references[path] = new ReferenceEntry(info.Length, info.LastWriteTimeUtc.Ticks, reference);
                references.Add(reference);
            }
        }
        string signature = string.Join("|", referencePaths.Select(path =>
        {
            FileInfo info = new(path);
            return path + ":" + info.Length + ":" + info.LastWriteTimeUtc.Ticks;
        }));

        if (_previous == null || !string.Equals(signature, _referenceSignature, StringComparison.Ordinal))
        {
            _previous = CSharpCompilation.Create(assemblyName, currentTrees.Values, references, CompilationOptions);
            _referenceSignature = signature;
            return _previous;
        }

        Dictionary<string, SyntaxTree> oldTrees = _previous.SyntaxTrees
            .Where(tree => !string.IsNullOrEmpty(tree.FilePath))
            .ToDictionary(tree => tree.FilePath, StringComparer.OrdinalIgnoreCase);
        CSharpCompilation next = _previous;
        foreach (KeyValuePair<string, SyntaxTree> old in oldTrees)
        {
            if (!currentTrees.TryGetValue(old.Key, out SyntaxTree? current)) next = next.RemoveSyntaxTrees(old.Value);
            else if (!ReferenceEquals(old.Value, current)) next = next.ReplaceSyntaxTree(old.Value, current);
        }
        foreach (KeyValuePair<string, SyntaxTree> current in currentTrees)
            if (!oldTrees.ContainsKey(current.Key)) next = next.AddSyntaxTrees(current.Value);
        _previous = next.WithAssemblyName(assemblyName);
        return _previous;
    }
}

internal sealed class CompilerOptions
{
    private CompilerOptions(IReadOnlyList<string> sources, string output, IReadOnlyList<string> references)
    { SourceDirectories = sources; OutputDirectory = output; ReferencePaths = references; }
    public IReadOnlyList<string> SourceDirectories { get; }
    public string OutputDirectory { get; }
    public IReadOnlyList<string> ReferencePaths { get; }

    public static CompilerOptions Parse(string[] args)
    {
        var sources = new List<string>(); string? output = null; var references = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--source": sources.Add(Read(args, ref i)); break;
                case "--output": output = Read(args, ref i); break;
                case "--reference": references.Add(Read(args, ref i)); break;
                default: throw new ArgumentException($"Unknown argument '{args[i]}'.");
            }
        }
        if (sources.Count == 0 || string.IsNullOrWhiteSpace(output))
            throw new ArgumentException("Usage: --source <dir> [--source <dir>] --output <dir> --reference <assembly>");
        output = Path.GetFullPath(output);
        for (int i = 0; i < sources.Count; i++)
        {
            sources[i] = Path.GetFullPath(sources[i]);
            if (!Directory.Exists(sources[i])) throw new DirectoryNotFoundException(sources[i]);
        }
        for (int i = 0; i < references.Count; i++)
        {
            references[i] = Path.GetFullPath(references[i]);
            if (!File.Exists(references[i])) throw new FileNotFoundException("Reference not found.", references[i]);
        }
        return new CompilerOptions(sources, output, references);
    }

    private static string Read(string[] args, ref int index)
    { if (++index >= args.Length) throw new ArgumentException("Missing argument value."); return args[index]; }
}
