using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace ThinkMeta.CodeAnalysis.NetAnalyzers;

/// <summary>
/// Analyzes methods marked with the DeepCopy attribute to ensure correct usage and enforces that all relevant types are
/// sealed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DeepCopyUsageAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _rule = new(
        id: "TM0002",
        title: "DeepCopy attribute usage violation",
        messageFormat: "{0}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Methods marked with DeepCopy must have a single sealed parameter, and all nested types and collections (not marked with DeepCopyIgnore) must also be sealed.");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        var deepCopyAttr = methodSymbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "DeepCopyAttribute");
        if (deepCopyAttr == null)
            return;

        // Check for single parameter
        if (methodSymbol.Parameters.Length != 1) {
            context.ReportDiagnostic(Diagnostic.Create(_rule, methodSymbol.Locations[0], "Method must have exactly one parameter."));
            return;
        }

        var paramType = methodSymbol.Parameters[0].Type;
        if (!paramType.IsSealed) {
            context.ReportDiagnostic(Diagnostic.Create(_rule, methodSymbol.Parameters[0].Locations[0], $"Parameter type '{paramType.Name}' must be sealed."));
            return;
        }

        // Recursively check nested types and collections
        if (!AllNestedTypesSealed(paramType, context.Compilation, null)) {
            context.ReportDiagnostic(Diagnostic.Create(_rule, methodSymbol.Parameters[0].Locations[0], $"All nested types and collections (not marked with DeepCopyIgnore) must be sealed."));
        }
    }

    private bool AllNestedTypesSealed(ITypeSymbol type, Compilation compilation, HashSet<ITypeSymbol>? visited = null)
    {
        visited ??= new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Prevent infinite recursion on cycles
        if (!visited.Add(type)) {
            return true;
        }

        // If it's a collection, check the element type(s)
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType) {
            foreach (var arg in namedType.TypeArguments) {
                if (!AllNestedTypesSealed(arg, compilation, visited)) {
                    return false;
                }
            }
            // For collections, don't check fields/properties of the collection type itself
            return true;
        }

        // For classes/structs, check all fields and properties, skipping those with DeepCopyIgnore
        if (type.TypeKind is TypeKind.Class || type.TypeKind == TypeKind.Struct) {
            foreach (var member in type.GetMembers().OfType<IPropertySymbol>()) {
                if (HasDeepCopyIgnore(member)) {
                    continue;
                }
                if (!AllNestedTypesSealed(member.Type, compilation, visited)) {
                    return false;
                }
            }
            foreach (var member in type.GetMembers().OfType<IFieldSymbol>()) {
                if (HasDeepCopyIgnore(member)) {
                    continue;
                }
                if (!AllNestedTypesSealed(member.Type, compilation, visited)) {
                    return false;
                }
            }
        }

        return type.IsSealed || type.TypeKind == TypeKind.Struct;
    }

    private static bool HasDeepCopyIgnore(ISymbol member)
    {
        return member.GetAttributes().Any(a => a.AttributeClass?.Name == "DeepCopyIgnoreAttribute");
    }
}