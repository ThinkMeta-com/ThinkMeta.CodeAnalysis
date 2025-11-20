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
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
    }

    private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
    {
        var binaryExpr = (BinaryExpressionSyntax)context.Node;

        // Bail out if inside an expression tree
        if (IsInsideExpressionTree(context))
            return;

        // Check if either side is null literal
        if (binaryExpr.Right.IsKind(SyntaxKind.NullLiteralExpression) ||
            binaryExpr.Left.IsKind(SyntaxKind.NullLiteralExpression)) {
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

        foreach (var lambda in node.Ancestors().OfType<LambdaExpressionSyntax>()) {
            var typeInfo = semanticModel.GetTypeInfo(lambda, context.CancellationToken);
            var convertedType = typeInfo.ConvertedType;

            if (convertedType != null &&
                convertedType.OriginalDefinition.ToString().StartsWith("System.Linq.Expressions.Expression")) {
                return true;
            }
        }

        return false;
    }
}
