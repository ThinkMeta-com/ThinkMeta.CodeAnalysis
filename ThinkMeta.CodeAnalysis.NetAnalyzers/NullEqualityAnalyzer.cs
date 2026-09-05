using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace ThinkMeta.CodeAnalysis.NetAnalyzers;

/// <summary>
/// Analyzes C# code to detect and report usage of equality or inequality comparisons with null.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NullEqualityAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _rule = new(
        id: "TM0001",
        title: "Use pattern matching for null checks",
        messageFormat: "Use '{0}' instead of '{1}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        // Enable analysis of generated code, especially for .razor-generated files
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
    }

    private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
    {
        if (!IsSupportedFile(context))
            return;

        var binaryExpr = (BinaryExpressionSyntax)context.Node;

        // Bail out if inside an expression tree
        if (IsInsideExpressionTree(context))
            return;

        if (binaryExpr.Right.IsKind(SyntaxKind.NullLiteralExpression) || binaryExpr.Left.IsKind(SyntaxKind.NullLiteralExpression)) {
            var operatorText = binaryExpr.OperatorToken.Text; // "==" or "!="
            var replacement = operatorText == "==" ? "is null" : "is not null";

            var diagnostic = Diagnostic.Create(
                _rule,
                binaryExpr.GetLocation(),
                replacement,
                operatorText + " null");

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsInsideExpressionTree(SyntaxNodeAnalysisContext context)
    {
        var semanticModel = context.SemanticModel;
        var node = context.Node;

        foreach (var ancestor in node.Ancestors()) {
            switch (ancestor) {
                case LambdaExpressionSyntax lambda:
                    var typeInfo = semanticModel.GetTypeInfo(lambda, context.CancellationToken);
                    if (IsExpressionType(typeInfo.ConvertedType))
                        return true;
                    break;

                // Query syntax (from/let/where/join/orderby) is translated by the compiler into
                // implicit lambdas that never appear as LambdaExpressionSyntax nodes. When the
                // translated method (e.g. Queryable.Where) expects an Expression<TDelegate>, the
                // clause's body is part of an expression tree just like an explicit lambda would be.
                case QueryClauseSyntax queryClause:
                    var clauseInfo = semanticModel.GetQueryClauseInfo(queryClause, context.CancellationToken);
                    if (IsExpressionTreeMethod(clauseInfo.OperationInfo.Symbol) || IsExpressionTreeMethod(clauseInfo.CastInfo.Symbol))
                        return true;
                    break;

                // Same reasoning as above, but for the "select"/"group" clause, which the semantic
                // model exposes via GetSymbolInfo instead of GetQueryClauseInfo.
                case SelectOrGroupClauseSyntax selectOrGroupClause:
                    var symbolInfo = semanticModel.GetSymbolInfo(selectOrGroupClause, context.CancellationToken);
                    if (IsExpressionTreeMethod(symbolInfo.Symbol))
                        return true;
                    break;

                default:
                    break;
            }
        }

        return false;
    }

    private static bool IsExpressionType(ITypeSymbol? type) =>
        type is not null && type.OriginalDefinition.ToString().StartsWith("System.Linq.Expressions.Expression");

    private static bool IsExpressionTreeMethod(ISymbol? symbol) =>
        symbol is IMethodSymbol method && method.Parameters.Any(p => IsExpressionType(p.Type));

    // Only analyze .g.cs files if they are generated from .razor files (Razor components).
    // All non-generated files are always supported.
    private static bool IsSupportedFile(SyntaxNodeAnalysisContext context)
    {
        var filePath = context.Node.SyntaxTree.FilePath;

        if (filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            return filePath.Contains(".razor", StringComparison.OrdinalIgnoreCase);

        return true;
    }
}