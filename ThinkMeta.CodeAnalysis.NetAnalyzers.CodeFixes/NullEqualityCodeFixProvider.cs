using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;

namespace ThinkMeta.CodeAnalysis.NetAnalyzers;

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

        // Support code fix for query syntax as well as normal binary expressions
        var node = root?.FindNode(diagnostic.Location.SourceSpan);
        if (node is not BinaryExpressionSyntax binaryNode)
            return;

        var left = binaryNode.Left;
        // In query syntax, left side may be a complex expression, so preserve trivia
        var replacement = binaryNode.IsKind(SyntaxKind.EqualsExpression)
            ? $"{left} is null"
            : $"{left} is not null";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Use pattern matching null check",
                createChangedDocument: c => ReplaceWithPatternAsync(context.Document, binaryNode, replacement, c),
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
