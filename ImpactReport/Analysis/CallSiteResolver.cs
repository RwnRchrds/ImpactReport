using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ImpactReport.Analysis;

public sealed record CallSite(
    string? Namespace,
    string? TypeName,
    string? MemberName,
    IMethodSymbol? EnclosingMethod);

public static class CallSiteResolver
{
    public static async Task<CallSite> ResolveAsync(
        Document document,
        int position,
        CancellationToken cancellationToken = default)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return Unknown;

        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (model is null) return Unknown;

        var node = root.FindToken(position).Parent;

        IMethodSymbol? enclosingMethod = null;
        string? memberName = null;

        for (; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case AnonymousFunctionExpressionSyntax:
                    continue;

                case LocalFunctionStatementSyntax:
                    continue;

                case BaseMethodDeclarationSyntax or AccessorDeclarationSyntax:
                {
                    if (model.GetDeclaredSymbol(node, cancellationToken) is IMethodSymbol method)
                    {
                        enclosingMethod = method;
                        memberName = method.Name;
                        return Describe(method.ContainingType, memberName, enclosingMethod);
                    }

                    continue;
                }

                case BasePropertyDeclarationSyntax or BaseFieldDeclarationSyntax or EnumMemberDeclarationSyntax:
                {
                    var member = model.GetDeclaredSymbol(node, cancellationToken)
                                 ?? DeclaredVariable(model, node, cancellationToken);

                    if (member is not null)
                        return Describe(member.ContainingType, member.Name, enclosingMethod: null);

                    continue;
                }

                case BaseTypeDeclarationSyntax:
                {
                    if (model.GetDeclaredSymbol(node, cancellationToken) is INamedTypeSymbol type)
                        return Describe(type, memberName, enclosingMethod);

                    continue;
                }

                case CompilationUnitSyntax:
                {
                    if (model.GetDeclaredSymbol(node, cancellationToken) is { } global)
                        return new CallSite(NamespaceOf(global.ContainingNamespace), null, null, enclosingMethod);

                    return Unknown;
                }
            }
        }

        return Unknown;
    }

    private static ISymbol? DeclaredVariable(SemanticModel model, SyntaxNode node, CancellationToken cancellationToken)
    {
        var declarator = node.DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        return declarator is null ? null : model.GetDeclaredSymbol(declarator, cancellationToken);
    }

    private static CallSite Describe(INamedTypeSymbol? type, string? memberName, IMethodSymbol? enclosingMethod) =>
        new(
            Namespace: NamespaceOf(type?.ContainingNamespace),
            TypeName: type?.ToDisplayString(),
            MemberName: memberName,
            EnclosingMethod: enclosingMethod);

    private static string? NamespaceOf(INamespaceSymbol? ns) =>
        ns is { IsGlobalNamespace: false } ? ns.ToDisplayString() : null;

    private static readonly CallSite Unknown = new(null, null, null, null);
}
