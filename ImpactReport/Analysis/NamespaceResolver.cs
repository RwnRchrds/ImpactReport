using Microsoft.CodeAnalysis;

namespace ImpactReport.Analysis;

public static class NamespaceResolver
{
    public static async Task<string?> GetContainingNamespaceAsync(Document doc, int position)
    {
        var root = await doc.GetSyntaxRootAsync();
        if (root is null) return null;

        var node = root.FindToken(position).Parent;
        if (node is null) return null;

        var model = await doc.GetSemanticModelAsync();
        if (model is null) return null;

        while (node is not null)
        {
            var symbol = model.GetDeclaredSymbol(node) ?? model.GetSymbolInfo(node).Symbol;
            if (symbol is not null)
            {
                var ns = symbol.ContainingNamespace;
                return ns is { IsGlobalNamespace: false } ? ns.ToDisplayString() : null;
            }

            node = node.Parent;
        }

        return null;
    }
}