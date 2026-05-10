using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace ThinkMeta.CodeAnalysis.NetAnalyzers;

/// <summary>
/// Analyzes <c>Clone()</c> methods to detect missing property assignments (TM0002) and
/// shallow copies of reference-type properties (TM0003).
/// </summary>
/// <remarks>
/// <para>A property is excluded from analysis if its name or any of its attribute names contains
/// one of the keywords: <c>clone</c>, <c>ignore</c>, or <c>exclude</c> (case-insensitive).</para>
/// <para>Methods containing <c>MemberwiseClone()</c> are skipped entirely, as they are intentional
/// shallow copies.</para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CloneMethodAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic ID for a property not assigned inside a Clone method.</summary>
    public const string MissingPropertyId = "TM0002";

    /// <summary>Diagnostic ID for a reference-type property assigned shallowly inside a Clone method.</summary>
    public const string ShallowCopyId = "TM0003";

    /// <summary>Diagnostic property key used to pass semicolon-separated missing property names to the code fix provider.</summary>
    public const string PropertyNamesKey = "PropertyNames";

    private static readonly string[] _excludeKeywords = ["clone", "ignore", "exclude"];

    private static readonly DiagnosticDescriptor _missingPropertyRule = new(
        id: MissingPropertyId,
        title: "Clone method is missing property assignments",
        messageFormat: "Clone method does not assign: {0}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _shallowCopyRule = new(
        id: ShallowCopyId,
        title: "Clone method performs a shallow copy",
        messageFormat: "Property '{0}' is a reference type and is assigned shallowly; consider performing a deep copy",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_missingPropertyRule, _shallowCopyRule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var methodDecl = (MethodDeclarationSyntax)context.Node;

        if (methodDecl.Identifier.Text != "Clone")
            return;

        if (methodDecl.ParameterList.Parameters.Count != 0)
            return;

        if (methodDecl.Body is null && methodDecl.ExpressionBody is null)
            return;

        var semanticModel = context.SemanticModel;
        var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl, context.CancellationToken);
        if (methodSymbol is null)
            return;

        var containingType = methodSymbol.ContainingType;
        var returnType = methodSymbol.ReturnType;

        // Return type must be the declaring type or object (e.g. ICloneable implementation)
        if (!SymbolEqualityComparer.Default.Equals(returnType, containingType) &&
            returnType.SpecialType != SpecialType.System_Object) {
            return;
        }

        // MemberwiseClone is an intentional shallow copy — skip
        if (ContainsMemberwiseClone(methodDecl))
            return;

        var cloneableMembers = GetCloneableMembers(containingType);
        if (cloneableMembers.Length == 0)
            return;

        var (assigned, shallowAssignments) = AnalyzeBody(methodDecl, containingType, semanticModel, context.CancellationToken);

        var missingNames = new List<string>();
        foreach (var member in cloneableMembers) {
            if (!assigned.Contains(member.Name)) {
                missingNames.Add(member.Name);
                continue;
            }

            if (shallowAssignments.TryGetValue(member.Name, out var shallowLocation)) {
                var memberType = member is IPropertySymbol prop ? prop.Type : ((IFieldSymbol)member).Type;
                if (memberType.IsReferenceType && memberType.SpecialType != SpecialType.System_String)
                    context.ReportDiagnostic(Diagnostic.Create(_shallowCopyRule, shallowLocation, member.Name));
            }
        }

        if (missingNames.Count > 0) {
            var formatted = string.Join(", ", missingNames.ConvertAll(n => $"'{n}'"));
            var props = ImmutableDictionary<string, string?>.Empty.Add(PropertyNamesKey, string.Join(";", missingNames));
            context.ReportDiagnostic(Diagnostic.Create(
                _missingPropertyRule,
                methodDecl.Identifier.GetLocation(),
                props,
                formatted));
        }
    }

    private static bool ContainsMemberwiseClone(MethodDeclarationSyntax methodDecl)
    {
        return methodDecl.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv => inv.Expression switch {
                IdentifierNameSyntax id => id.Identifier.Text == "MemberwiseClone",
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text == "MemberwiseClone",
                _ => false
            });
    }

    private static ImmutableArray<ISymbol> GetCloneableMembers(INamedTypeSymbol containingType)
    {
        var members = ImmutableArray.CreateBuilder<ISymbol>();
        var currentType = containingType;

        // Walk the inheritance chain to include base class members
        while (currentType is not null && currentType.SpecialType == SpecialType.None) {
            foreach (var member in currentType.GetMembers()) {
                if (member.IsStatic || member.IsImplicitlyDeclared)
                    continue;

                if (member is IPropertySymbol property) {
                    if (property.IsAbstract || property.IsIndexer || property.SetMethod is null)
                        continue;
                    if (IsExcluded(member))
                        continue;
                    members.Add(property);
                    continue;
                }

                if (member is IFieldSymbol field) {
                    if (field.IsReadOnly || field.IsConst)
                        continue;
                    if (IsExcluded(member))
                        continue;
                    members.Add(field);
                }
            }

            currentType = currentType.BaseType;
        }

        return members.ToImmutable();
    }

    private static bool IsExcluded(ISymbol member)
    {
        if (ContainsExcludeKeyword(member.Name))
            return true;

        foreach (var attr in member.GetAttributes()) {
            var attrName = attr.AttributeClass?.Name;
            if (attrName is not null && ContainsExcludeKeyword(attrName))
                return true;
        }

        return false;
    }

    private static bool ContainsExcludeKeyword(string name)
    {
        foreach (var kw in _excludeKeywords) {
            if (name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static (HashSet<string> Assigned, Dictionary<string, Location> ShallowAssignments) AnalyzeBody(
        MethodDeclarationSyntax methodDecl,
        INamedTypeSymbol containingType,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var shallow = new Dictionary<string, Location>(StringComparer.Ordinal);

        var body = (SyntaxNode?)methodDecl.Body ?? methodDecl.ExpressionBody!.Expression;

        // Object initializer pattern: new T { Prop = this.Prop, ... } or new() { ... }
        foreach (var objCreation in body.DescendantNodesAndSelf().OfType<BaseObjectCreationExpressionSyntax>()) {
            if (objCreation.Initializer is null)
                continue;

            var createdType = semanticModel.GetTypeInfo(objCreation, cancellationToken).Type;
            if (createdType is null || !SymbolEqualityComparer.Default.Equals(createdType, containingType))
                continue;

            foreach (var expr in objCreation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>()) {
                if (expr.Left is not IdentifierNameSyntax left)
                    continue;

                var name = left.Identifier.Text;
                _ = assigned.Add(name);

                if (!shallow.ContainsKey(name) &&
                    IsDirectMemberRead(expr.Right, name, containingType, semanticModel, cancellationToken)) {
                    shallow[name] = expr.Right.GetLocation();
                }
            }
        }

        // Statement-based pattern: clone.Prop = this.Prop
        foreach (var assignment in body.DescendantNodesAndSelf().OfType<AssignmentExpressionSyntax>()) {
            if (assignment.Parent is InitializerExpressionSyntax)
                continue;

            var name = assignment.Left switch {
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                IdentifierNameSyntax ident => ident.Identifier.Text,
                _ => null
            };

            if (name is null)
                continue;

            var leftSymbol = semanticModel.GetSymbolInfo(assignment.Left, cancellationToken).Symbol;
            if (leftSymbol is null || !SymbolEqualityComparer.Default.Equals(leftSymbol.ContainingType, containingType))
                continue;

            _ = assigned.Add(name);

            if (!shallow.ContainsKey(name) &&
                IsDirectMemberRead(assignment.Right, name, containingType, semanticModel, cancellationToken)) {
                shallow[name] = assignment.Right.GetLocation();
            }
        }

        return (assigned, shallow);
    }

    private static bool IsDirectMemberRead(
        ExpressionSyntax rhs,
        string memberName,
        INamedTypeSymbol containingType,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        // Explicit: this.MemberName
        if (rhs is MemberAccessExpressionSyntax ma &&
            ma.Expression is ThisExpressionSyntax &&
            ma.Name.Identifier.Text == memberName) {
            return true;
        }

        // Implicit this: MemberName
        if (rhs is IdentifierNameSyntax ident && ident.Identifier.Text == memberName) {
            var symbol = semanticModel.GetSymbolInfo(ident, cancellationToken).Symbol;
            if (symbol is not null && SymbolEqualityComparer.Default.Equals(symbol.ContainingType, containingType))
                return true;
        }

        return false;
    }
}
