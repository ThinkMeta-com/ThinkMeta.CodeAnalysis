using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;

namespace ThinkMeta.CodeAnalysis.NetAnalyzers;

/// <summary>
/// Provides a code fix for <see cref="CloneMethodAnalyzer.MissingPropertyId"/> (TM0002) diagnostics,
/// inserting the missing property assignment into the Clone method body.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CloneMethodCodeFixProvider)), Shared]
public class CloneMethodCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [CloneMethodAnalyzer.MissingPropertyId];

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();
        if (!diagnostic.Properties.TryGetValue(CloneMethodAnalyzer.PropertyNamesKey, out var namesValue) || namesValue is null)
            return;

        var propertyNames = namesValue.Split(';');
        if (propertyNames.Length == 0)
            return;

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var identifierNode = root?.FindNode(diagnostic.Location.SourceSpan);
        var methodDecl = identifierNode?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (methodDecl is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add all missing property assignments to Clone",
                createChangedDocument: c => AddPropertyAssignmentsAsync(context.Document, methodDecl, propertyNames, c),
                equivalenceKey: "AddAllMissingCloneProperties"),
            diagnostic);
    }

    private static async Task<Document> AddPropertyAssignmentsAsync(
        Document document,
        MethodDeclarationSyntax methodDecl,
        string[] propertyNames,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
            return document;

        var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl, cancellationToken);
        var containingType = methodSymbol?.ContainingType;
        if (containingType is null)
            return document;

        // Prefer adding to an object initializer if one exists for the containing type
        var objCreation = methodDecl.DescendantNodes()
            .OfType<BaseObjectCreationExpressionSyntax>()
            .FirstOrDefault(o => {
                var type = semanticModel.GetTypeInfo(o, cancellationToken).Type;
                return type is not null && SymbolEqualityComparer.Default.Equals(type, containingType);
            });

        if (objCreation?.Initializer is not null)
            return AddToObjectInitializer(document, root, objCreation, propertyNames);

        return AddAsAssignmentStatement(document, root, methodDecl, propertyNames);
    }

    private static Document AddToObjectInitializer(
        Document document,
        SyntaxNode root,
        BaseObjectCreationExpressionSyntax objCreation,
        string[] propertyNames)
    {
        var newExpressions = objCreation.Initializer!.Expressions;

        foreach (var propertyName in propertyNames) {
            var leadingTrivia = newExpressions.Count > 0
                ? newExpressions.Last().GetLeadingTrivia()
                : SyntaxTriviaList.Empty;

            var newAssignment = SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName(propertyName),
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ThisExpression(),
                    SyntaxFactory.IdentifierName(propertyName)))
                .WithLeadingTrivia(leadingTrivia);

            newExpressions = newExpressions.Add(newAssignment);
        }

        var newInitializer = objCreation.Initializer.WithExpressions(newExpressions);
        var newRoot = root.ReplaceNode(objCreation.Initializer, newInitializer);
        return document.WithSyntaxRoot(newRoot);
    }

    private static Document AddAsAssignmentStatement(
        Document document,
        SyntaxNode root,
        MethodDeclarationSyntax methodDecl,
        string[] propertyNames)
    {
        if (methodDecl.Body is null)
            return document;

        var returnStmt = methodDecl.Body.Statements.OfType<ReturnStatementSyntax>().LastOrDefault();
        if (returnStmt is null)
            return document;

        if (returnStmt.Expression is not IdentifierNameSyntax retIdent)
            return document;

        var cloneVarName = retIdent.Identifier.Text;
        var leadingTrivia = returnStmt.GetLeadingTrivia();
        var insertIndex = methodDecl.Body.Statements.IndexOf(returnStmt);
        var newStmts = methodDecl.Body.Statements;

        for (var i = 0; i < propertyNames.Length; i++) {
            var assignmentStmt = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(cloneVarName),
                        SyntaxFactory.IdentifierName(propertyNames[i])),
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ThisExpression(),
                        SyntaxFactory.IdentifierName(propertyNames[i]))))
                .WithLeadingTrivia(leadingTrivia);

            newStmts = newStmts.Insert(insertIndex + i, assignmentStmt);
        }

        var newBody = methodDecl.Body.WithStatements(newStmts);
        var newRoot = root.ReplaceNode(methodDecl.Body, newBody);
        return document.WithSyntaxRoot(newRoot);
    }
}
