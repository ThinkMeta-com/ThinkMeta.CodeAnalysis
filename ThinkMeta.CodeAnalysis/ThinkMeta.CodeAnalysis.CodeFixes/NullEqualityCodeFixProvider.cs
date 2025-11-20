using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;

namespace ThinkMeta.CodeAnalysis;

/// <summary>
/// Provides a code fix for diagnostics that identify equality comparisons to null, replacing them with pattern matching
/// null checks in C# code.
/// </summary>
/// <remarks>This code fix provider targets diagnostics with the ID "NSTOBAA002" and suggests using pattern
/// matching (e.g., 'is null' or 'is not null') instead of traditional equality operators for null checks. The fix is
/// applied in batch mode when using Fix All. This provider is intended for use with Roslyn analyzers and supports C#
/// language files.</remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullEqualityCodeFixProvider)), Shared]
public class NullEqualityCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ["TM0001"];

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root?.FindNode(diagnostic.Location.SourceSpan) is not BinaryExpressionSyntax node)
            return;

        var replacement = node.IsKind(SyntaxKind.EqualsExpression)
            ? $"{node.Left} is null"
            : $"{node.Left} is not null";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Use pattern matching null check",
                createChangedDocument: c => ReplaceWithPatternAsync(context.Document, node, replacement, c),
                equivalenceKey: "UsePatternMatchingNullCheck"),
            diagnostic);
    }

    private static async Task<Document> ReplaceWithPatternAsync(Document document, BinaryExpressionSyntax node, string replacement, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var newNode = SyntaxFactory.ParseExpression(replacement)
            .WithTriviaFrom(node);

        var newRoot = root!.ReplaceNode(node, newNode);
        return document.WithSyntaxRoot(newRoot);
    }
}
