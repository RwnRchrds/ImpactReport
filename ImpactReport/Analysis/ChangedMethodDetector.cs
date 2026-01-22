using ImpactReport.Git;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ImpactReport.Analysis;

public static class ChangedMethodDetector
{
    public static async Task<IReadOnlyList<IMethodSymbol>> FindChangedMethodsAsync(
        Solution solution,
        IReadOnlyList<ChangedFile> changedFiles,
        string repoRoot,
        bool includeTests)
    {
        // Map file paths to Documents
        var docsByPath = solution.Projects
            .Where(p => includeTests || !p.Name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase))
            .SelectMany(p => p.Documents)
            .Where(d => !string.IsNullOrWhiteSpace(d.FilePath))
            .ToDictionary(d => NormalizePath(d.FilePath!), d => d, StringComparer.OrdinalIgnoreCase);

        var methodSymbols = new Dictionary<string, IMethodSymbol>(StringComparer.Ordinal);

        foreach (var file in changedFiles)
        {
            var absPath = NormalizePath(Path.Combine(repoRoot, file.RelativePath));
            if (!docsByPath.TryGetValue(absPath, out var doc))
                continue;

            if (file.ChangedRanges.Count == 0)
                continue;

            var root = await doc.GetSyntaxRootAsync().ConfigureAwait(false);
            var model = await doc.GetSemanticModelAsync().ConfigureAwait(false);
            if (root is null || model is null)
                continue;

            // Consider methods and constructors. (Easy to extend to property accessors later.)
            var candidates = root.DescendantNodes()
                .OfType<BaseMethodDeclarationSyntax>()
                .ToList();

            foreach (var candidate in candidates)
            {
                var span = candidate.Span;
                var startLine = GetLine(doc, span.Start);
                var endLine = GetLine(doc, span.End);

                var candidateRange = new LineRange(startLine, endLine);

                // Intersects any changed range?
                if (!file.ChangedRanges.Any(r => r.Intersects(candidateRange)))
                    continue;

                var symbol = model.GetDeclaredSymbol(candidate) as IMethodSymbol;
                if (symbol is null)
                    continue;

                // Deduplicate by fully-qualified signature
                var key = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                methodSymbols[key] = symbol;
            }
        }

        return methodSymbols.Values.ToList();
    }

    private static int GetLine(Document doc, int position)
    {
        // 1-based line number
        var text = doc.GetTextAsync().GetAwaiter().GetResult();
        var linePos = text.Lines.GetLinePosition(position);
        return linePos.Line + 1;
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).Replace('/', '\\');
}