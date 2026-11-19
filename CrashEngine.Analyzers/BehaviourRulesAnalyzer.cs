using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CrashEngine.Analyzers;

// Amedo 2026-11-18
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BehaviourRulesAnalyzer : DiagnosticAnalyzer
{
    public const int MaxChoices = 31;
    private const string Category = "CrashEngine.Behaviour";

    public static readonly DiagnosticDescriptor ElseAfterWhen = new("CE0001", "else after When",
        "else after {0}(...) is never used: the game runs the strongest choice of a state, so write each choice as its own if block",
        Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor OutsideChoice = new("CE0002", "Outside a choice",
        "{0}(...) has to be inside an if (When(...)) {{ }} block of a state method",
        Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor NotAState = new("CE0003", "Not a state",
        "{0} is not a state: add State({0}) to Define()",
        Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor TooManyChoices = new("CE0004", "Too many choices",
        "{0} has {1} choices (if blocks); a PS2 state holds at most " + "31",
        Category, DiagnosticSeverity.Error, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(ElseAfterWhen, OutsideChoice, NotAState, TooManyChoices);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClass(SyntaxNodeAnalysisContext context) =>
        Check((ClassDeclarationSyntax)context.Node, context.SemanticModel, context.ReportDiagnostic);

    public static void Check(ClassDeclarationSyntax cls, SemanticModel model, System.Action<Diagnostic> report)
    {
        if (cls.BaseList?.Types.Any(t => t.Type.ToString().EndsWith("CompiledBehaviour")) != true) return;
        var define = cls.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.Text == "Define");
        if (define is null) return;

        var states = new HashSet<string>();
        bool allStatesKnown = true;
        foreach (var call in define.DescendantNodes().OfType<InvocationExpressionSyntax>())
            if (CalledName(call) == "State")
            {
                if (FirstArgument(call) is { } state) states.Add(state.Identifier.Text);
                else allStatesKnown = false;
            }

        if (allStatesKnown)
            foreach (var call in cls.DescendantNodes().OfType<InvocationExpressionSyntax>())
                if (CalledName(call) is "GoTo" or "Start" && FirstArgument(call) is { } target && !states.Contains(target.Identifier.Text))
                    report(Diagnostic.Create(NotAState, target.GetLocation(), target.Identifier.Text));

        foreach (var method in cls.Members.OfType<MethodDeclarationSyntax>().Where(m => states.Contains(m.Identifier.Text)))
        {
            int choices = 0;
            foreach (var choice in method.DescendantNodes().OfType<IfStatementSyntax>())
            {
                if (ChoiceName(choice) is not { } name) continue;
                if (choice.Parent is BlockSyntax block && block.Parent == method) choices++;
                if (choice.Else is { } other) report(Diagnostic.Create(ElseAfterWhen, other.ElseKeyword.GetLocation(), name));
            }
            if (choices > MaxChoices) report(Diagnostic.Create(TooManyChoices, method.Identifier.GetLocation(), method.Identifier.Text, choices));

            foreach (var call in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!NeedsChoice(call, model) || InsideChoice(call, method)) continue;
                report(Diagnostic.Create(OutsideChoice, call.GetLocation(), CalledName(call) ?? "this"));
            }
        }
    }

    private static string? CalledName(InvocationExpressionSyntax call) => call.Expression switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        _ => null,
    };

    private static IdentifierNameSyntax? FirstArgument(InvocationExpressionSyntax call) =>
        call.ArgumentList.Arguments.Count > 0 ? call.ArgumentList.Arguments[0].Expression as IdentifierNameSyntax : null;

    private static string? ChoiceName(IfStatementSyntax choice) =>
        choice.Condition is InvocationExpressionSyntax call && CalledName(call) is "When" or "Always" ? CalledName(call) : null;

    private static bool InsideChoice(SyntaxNode node, MethodDeclarationSyntax method)
    {
        for (var at = node; at is not null && at != method; at = at.Parent)
            if (at.Parent is IfStatementSyntax choice && choice.Statement == at && ChoiceName(choice) is not null) return true;
        return false;
    }

    private static bool NeedsChoice(InvocationExpressionSyntax call, SemanticModel model)
    {
        if (CalledName(call) is "GoTo" or "Reenter" or "Cmd") return true;
        return model.GetSymbolInfo(call).Symbol is IMethodSymbol { ContainingType.Name: "BehaviourCommands" };
    }
}
