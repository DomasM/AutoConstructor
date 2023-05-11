using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoConstructor.Generator.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ClassWithoutPartialCodeFixProvider)), Shared]
public class ClassWithoutPartialCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticDescriptors.ClassWithoutPartialDiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic? diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;
        TryFixDeclaration<ClassDeclarationSyntax>(context, root, diagnostic, diagnosticSpan, "Make class partial");
        TryFixDeclaration<RecordDeclarationSyntax>(context, root, diagnostic, diagnosticSpan, "Make record partial");
    }

    private static void TryFixDeclaration<T>(CodeFixContext context, SyntaxNode? root, Diagnostic diagnostic, TextSpan diagnosticSpan, string title) where T : TypeDeclarationSyntax
    {
        T? declaration = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<T>().FirstOrDefault();
        if (declaration is not null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: c => MakeTypePartialAsync(context.Document, declaration, c),
                    equivalenceKey: title),
                diagnostic);
        }
    }

    private static async Task<Document> MakeTypePartialAsync(Document document, TypeDeclarationSyntax typeDeclarationSyntax, CancellationToken cancellationToken)
    {
        SyntaxTokenList newModifiers = typeDeclarationSyntax.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        TypeDeclarationSyntax newDeclaration = typeDeclarationSyntax.WithModifiers(newModifiers);

        SyntaxNode? oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (oldRoot is not null)
        {
            SyntaxNode newRoot = oldRoot.ReplaceNode(typeDeclarationSyntax, newDeclaration);
            return document.WithSyntaxRoot(newRoot);
        }

        throw new InvalidOperationException("Cannot fix code.");
    }
}
