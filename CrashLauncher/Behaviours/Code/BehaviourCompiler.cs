using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using CrashEngine.Runtime.AgentLab.Compiled;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;

namespace CrashLauncher;

// Amedo 2026-11-18
public static class BehaviourCompiler
{
    public readonly record struct CompileError(string Code, string Message, int Line, int Column, string File = "");

    private const string SourceName = "Behaviour.cs";
    private const string LineKey = "CrashEngine.Behaviour.Line", ColumnKey = "CrashEngine.Behaviour.Column";

    private static readonly Lazy<MetadataReference[]> References = new(() =>
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(File.Exists)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray());

    private const string LocatorSource = """
        namespace CrashEngine.EditedBehaviour;
        public static class Locate
        {
            public static T At<T>(int line, int column, System.Func<T> call)
            {
                try { return call(); }
                catch (System.Exception e) when (Mark(e, line, column)) { throw; }
            }

            public static void At(int line, int column, System.Action call)
            {
                try { call(); }
                catch (System.Exception e) when (Mark(e, line, column)) { throw; }
            }

            private static bool Mark(System.Exception e, int line, int column)
            {
                if (e.Data.Contains("CrashEngine.Behaviour.Line")) return false;
                e.Data["CrashEngine.Behaviour.Line"] = line;
                e.Data["CrashEngine.Behaviour.Column"] = column;
                return false;
            }
        }
        """;

    private sealed class CallLocator : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = base.VisitInvocationExpression(node)!;
            if (node.Expression is not IdentifierNameSyntax) return visited;
            var at = node.GetLocation().GetLineSpan().StartLinePosition;
            return SyntaxFactory.ParseExpression($"global::CrashEngine.EditedBehaviour.Locate.At({at.Line + 1}, {at.Character + 1}, () => {visited.WithoutTrivia().ToFullString()})")
                                .WithTriviaFrom(node);
        }
    }

    private sealed class Built(AssemblyLoadContext context, List<Type> types) : IDisposable
    {
        public List<Type> Types { get; } = types;
        public void Dispose() => context.Unload();
    }

    public static PS2BehaviourGraph? ToGraph(string code, uint id, List<CompileError> errors)
    {
        var tree = Parse(code, SourceName);
        using var built = Build([tree], errors);
        if (built is null) return null;
        if (built.Types.Count != 1)
        {
            errors.Add(new CompileError("", $"the code needs exactly one behaviour class (a class : CompiledBehaviour), it has {built.Types.Count}", 0, 0));
            return null;
        }
        return Run(built.Types[0], tree, id, errors);
    }

    public static Dictionary<uint, PS2BehaviourGraph>? ToGraphs(IReadOnlyDictionary<string, string> files, List<CompileError> errors)
    {
        var trees = files.Select(f => Parse(f.Value, f.Key)).ToList();
        using var built = Build(trees, errors);
        if (built is null) return null;
        var graphs = new Dictionary<uint, PS2BehaviourGraph>();
        foreach (var type in built.Types)
        {
            var tree = trees.FirstOrDefault(t => t.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Any(c => c.Identifier.Text == type.Name));
            if (type.GetCustomAttribute<BehaviourIdAttribute>() is not { } attribute)
            {
                errors.Add(new CompileError("", $"class {type.Name} has no [BehaviourId(...)], so it is not tied to a script", 0, 0, tree?.FilePath ?? ""));
                continue;
            }
            if (graphs.ContainsKey(attribute.Id))
            {
                errors.Add(new CompileError("", $"two classes are script 0x{attribute.Id:X4}", 0, 0, tree?.FilePath ?? ""));
                continue;
            }
            if (Run(type, tree, attribute.Id, errors) is { } graph) graphs[attribute.Id] = graph;
        }
        return errors.Count == 0 ? graphs : null;
    }

    private static SyntaxTree Parse(string code, string path) =>
        CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest), path, Encoding.UTF8);

    private static Built? Build(IReadOnlyList<SyntaxTree> trees, List<CompileError> errors)
    {
        var compilation = Compile(trees.ToArray());
        using var dll = new MemoryStream();
        using var pdb = new MemoryStream();
        var result = compilation.Emit(dll, pdb, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
        var found = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var cls in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                CrashEngine.Analyzers.BehaviourRulesAnalyzer.Check(cls, model, found.Add);
        }
        foreach (var (d, span) in found.Select(d => (d, span: d.Location.GetLineSpan()))
                                       .OrderBy(x => x.span.Path, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.span.StartLinePosition))
            errors.Add(new CompileError(d.Id, d.GetMessage(), span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1, span.Path));
        if (errors.Count > 0) return null;
        dll.Position = 0;
        pdb.Position = 0;
        var context = new AssemblyLoadContext(null, isCollectible: true);
        var types = context.LoadFromStream(dll, pdb).GetTypes()
            .Where(t => !t.IsAbstract && typeof(CompiledBehaviour).IsAssignableFrom(t)).ToList();
        return new Built(context, types);
    }

    private static PS2BehaviourGraph? Run(Type type, SyntaxTree? tree, uint id, List<CompileError> errors)
    {
        try
        {
            var behaviour = (CompiledBehaviour)Activator.CreateInstance(type)!;
            var graph = behaviour.ToGraph(id);
            var problems = CrashEngine.ScriptGen.BehaviourLimits.Check(graph);
            foreach (var problem in problems)
                {
                string? method = problem.State >= 0 ? behaviour.StateName(problem.State) : null;
                errors.Add(new CompileError("PS2", method is null ? problem.Message : $"{method}: {problem.Message}", LineOf(tree, method), 1, tree?.FilePath ?? ""));
            }
            return problems.Count == 0 ? graph : null;
        }
        catch (Exception ex)
        {
            var inner = ex is TargetInvocationException { InnerException: { } ie } ? ie : ex;
            string path = tree?.FilePath ?? "";
            var frame = new StackTrace(inner, true).GetFrames()
                .FirstOrDefault(f => path.Length > 0 && string.Equals(f.GetFileName(), path, StringComparison.OrdinalIgnoreCase));
            var (line, column) = (tree is null ? null : LocateRuntimeError(tree)) ?? (frame?.GetFileLineNumber() ?? 0, frame?.GetFileColumnNumber() ?? 0);
            errors.Add(new CompileError(inner.GetType().Name, inner.Message, line, column, path));
            return null;
        }
    }

    private static int LineOf(SyntaxTree? tree, string? stateMethod)
    {
        if (tree is null) return 0;
        var root = tree.GetRoot();
        SyntaxNode? node = stateMethod is null
            ? root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault()
            : root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.Text == stateMethod);
        return node is null ? 0 : node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    }

    private static readonly SyntaxTree GlobalUsings = CSharpSyntaxTree.ParseText(
        "global using System; global using System.Linq; global using System.Collections.Generic;", new CSharpParseOptions(LanguageVersion.Latest));

    private static CSharpCompilation Compile(params SyntaxTree[] trees) =>
        CSharpCompilation.Create("EditedBehaviour_" + Guid.NewGuid().ToString("N"), [GlobalUsings, .. trees], References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable,
                                         optimizationLevel: OptimizationLevel.Debug));

    private static (int Line, int Column)? LocateRuntimeError(SyntaxTree tree)
    {
        var options = (CSharpParseOptions)tree.Options;
        var located = CSharpSyntaxTree.Create((CSharpSyntaxNode)new CallLocator().Visit(tree.GetRoot())!, options, tree.FilePath);
        using var dll = new MemoryStream();
        if (!Compile(located, CSharpSyntaxTree.ParseText(LocatorSource, options)).Emit(dll).Success) return null;
        dll.Position = 0;
        var context = new AssemblyLoadContext(null, isCollectible: true);
        try
        {
            var type = context.LoadFromStream(dll).GetTypes().FirstOrDefault(t => !t.IsAbstract && typeof(CompiledBehaviour).IsAssignableFrom(t));
            if (type is not null) ((CompiledBehaviour)Activator.CreateInstance(type)!).ToGraph(0);
            return null;
        }
        catch (Exception ex)
        {
            var inner = ex is TargetInvocationException { InnerException: { } ie } ? ie : ex;
            return inner.Data[LineKey] is int line && inner.Data[ColumnKey] is int column ? (line, column) : null;
        }
        finally
        {
            context.Unload();
        }
    }
}
