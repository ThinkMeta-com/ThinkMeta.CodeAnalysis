using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using ThinkMeta.CodeAnalysis.Annotations.Functional;

namespace ThinkMeta.CodeAnalysis.NetAnalyzers;

/// <summary>
/// Analyzes methods marked with <see cref="DeepCopyAttribute"/> to ensure all public instance members
/// of the parameter type are copied within the method body. Reports a diagnostic if any members are not copied.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DeepCopyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _rule = new(
        id: "TM0003",
        title: "DeepCopy method incomplete",
        messageFormat: "Method '{0}' does not copy the following members: {1}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethodSyntax, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethodSyntax(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodSyntax)
            return;
        var semanticModel = context.SemanticModel;
        if (semanticModel.GetDeclaredSymbol(methodSyntax, context.CancellationToken) is not IMethodSymbol methodSymbol)
            return;
        if (!HasDeepCopyAttribute(methodSymbol))
            return;
        if (methodSymbol.Parameters.Length != 1 || methodSymbol.Parameters[0].Type is not INamedTypeSymbol paramType || !paramType.IsSealed)
            return;

        var members = GetPublicInstanceMembers(paramType);
        var paramName = methodSymbol.Parameters[0].Name;
        var readMembers = new HashSet<string>();
        if (methodSyntax.Body != null)
            FindReadMembers(methodSyntax.Body, semanticModel, paramName, readMembers);
        var notRead = new List<string>();
        foreach (var member in members) {
            var missing = GetFirstMissingMemberPath(member, readMembers, semanticModel, context.CancellationToken);
            if (missing != null)
                notRead.Add(missing);
        }
        if (notRead.Count > 0) {
            var diagnostic = Diagnostic.Create(_rule, methodSymbol.Locations[0], methodSymbol.Name, string.Join(", ", notRead));
            context.ReportDiagnostic(diagnostic);
        }
    }

    // Returns null if all submembers are read, otherwise returns the full path of the first missing member
    private static string? GetFirstMissingMemberPath(ISymbol member, HashSet<string> readMembers, SemanticModel semanticModel, CancellationToken cancellationToken, string? prefix = null)
    {
        var memberName = prefix == null ? member.Name : prefix + "." + member.Name;
        if (IsCollectionType(member))
            return readMembers.Contains(memberName + ".__all_items__") || readMembers.Contains("__all_items__") ? null : memberName;
        var memberType = member switch {
            IPropertySymbol prop => prop.Type as INamedTypeSymbol,
            IFieldSymbol field => field.Type as INamedTypeSymbol,
            _ => null
        };
        if (memberType != null && (memberType.TypeKind == TypeKind.Class || memberType.TypeKind == TypeKind.Struct) && !IsSystemPrimitive(memberType)) {
            var subMembers = GetPublicInstanceMembers(memberType);
            if (subMembers.Length == 0) {
                // If the nested type has no public instance members, require the parent member itself to be read
                return readMembers.Contains(memberName) ? null : memberName;
            }
            // Only require all submembers to be read, do NOT require the parent member itself
            foreach (var sub in subMembers) {
                var missing = GetFirstMissingMemberPath(sub, readMembers, semanticModel, cancellationToken, memberName);
                if (missing != null)
                    return missing;
            }
            return null;
        }
        return readMembers.Contains(memberName) ? null : memberName;
    }

    private static bool HasDeepCopyAttribute(IMethodSymbol methodSymbol)
    {
        foreach (var attribute in methodSymbol.GetAttributes()) {
            if (attribute.AttributeClass?.Name == nameof(DeepCopyAttribute) ||
                attribute.AttributeClass?.ToDisplayString() == typeof(DeepCopyAttribute).FullName) {
                return true;
            }
        }
        return false;
    }

    private static ImmutableArray<ISymbol> GetPublicInstanceMembers(INamedTypeSymbol type)
    {
        var ignoreAttrName = "ThinkMeta.CodeAnalysis.Annotations.Functional.DeepCopyIgnoreAttribute";
        return [.. type.GetMembers()
            .Where(m => (m.Kind == SymbolKind.Property || m.Kind == SymbolKind.Field)
                        && m.DeclaredAccessibility == Accessibility.Public
                        && !m.IsStatic
                        && !m.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == ignoreAttrName))];
    }

    private static void FindReadMembers(SyntaxNode node, SemanticModel semanticModel, string paramName, HashSet<string> readMembers)
    {
        if (node == null)
            return;
        foreach (var descendant in node.DescendantNodes()) {
            if (descendant is MemberAccessExpressionSyntax memberAccess) {
                // Build the full access path
                var path = GetFullMemberPath(memberAccess, semanticModel, paramName);
                if (path != null)
                    _ = readMembers.Add(path);
            }
            if (descendant is ForEachStatementSyntax forEach) {
                var expr = forEach.Expression;
                if (expr is IdentifierNameSyntax id2 && id2.Identifier.Text == paramName) {
                    _ = readMembers.Add("__all_items__");
                }
                else if (expr is MemberAccessExpressionSyntax memberAccessExpr) {
                    var path = GetFullMemberPath(memberAccessExpr, semanticModel, paramName);
                    if (path != null)
                        _ = readMembers.Add(path + ".__all_items__");
                }
            }
        }
    }

    private static string? GetFullMemberPath(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel, string paramName)
    {
        var parts = new Stack<string>();
        ExpressionSyntax expr = memberAccess;
        while (expr is MemberAccessExpressionSyntax ma) {
            var symbol = semanticModel.GetSymbolInfo(ma.Name).Symbol;
            if (symbol is IPropertySymbol or IFieldSymbol)
                parts.Push(symbol.Name);
            expr = ma.Expression;
        }
        if (expr is IdentifierNameSyntax id && id.Identifier.Text == paramName) {
            return string.Join(".", parts);
        }
        return null;
    }

    private static bool IsMemberReadRecursive(ISymbol member, HashSet<string> readMembers, SemanticModel semanticModel, CancellationToken cancellationToken, string? prefix = null)
    {
        var memberName = prefix == null ? member.Name : prefix + "." + member.Name;
        if (IsCollectionType(member))
            return readMembers.Contains(memberName + ".__all_items__") || readMembers.Contains("__all_items__");
        var memberType = member switch {
            IPropertySymbol prop => prop.Type as INamedTypeSymbol,
            IFieldSymbol field => field.Type as INamedTypeSymbol,
            _ => null
        };
        if (memberType != null && (memberType.TypeKind == TypeKind.Class || memberType.TypeKind == TypeKind.Struct) && !IsSystemPrimitive(memberType)) {
            var subMembers = GetPublicInstanceMembers(memberType);
            if (subMembers.Length == 0) {
                // If the nested type has no public instance members, require the parent member itself to be read
                return readMembers.Contains(memberName);
            }
            // Only require all submembers to be read, do NOT require the parent member itself
            foreach (var sub in subMembers) {
                if (!IsMemberReadRecursive(sub, readMembers, semanticModel, cancellationToken, memberName))
                    return false;
            }
            return true;
        }
        return readMembers.Contains(memberName);
    }

    private static bool IsCollectionType(ISymbol member)
    {
        var type = member switch {
            IPropertySymbol prop => prop.Type,
            IFieldSymbol field => field.Type,
            _ => null
        };
        if (type == null)
            return false;
        return type.AllInterfaces.Any(i => i.ToDisplayString().StartsWith("System.Collections.Generic.IEnumerable"));
    }

    private static bool IsSystemPrimitive(INamedTypeSymbol type)
    {
        return type.SpecialType != SpecialType.None || type.ContainingNamespace?.ToDisplayString() == "System";
    }
}