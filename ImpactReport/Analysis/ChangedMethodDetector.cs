using ImpactReport.Git;
using ImpactReport.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ImpactReport.Analysis;

public static class ChangedMethodDetector
{
    public static async Task<IReadOnlyList<IMethodSymbol>> FindChangedMethodsAsync(
        Solution solution,
        IReadOnlyList<ChangedFile> changedFiles,
        string repoRoot,
        bool includeTests,
        CancellationToken cancellationToken = default)
    {
        var docsByPath = BuildDocumentIndex(solution, includeTests);
        var methodSymbols = new Dictionary<string, IMethodSymbol>(StringComparer.Ordinal);

        foreach (var file in changedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (file.ChangedRanges.Count == 0)
                continue;

            var absolutePath = PathNormalizer.Combine(repoRoot, file.RelativePath);
            if (!docsByPath.TryGetValue(absolutePath, out var doc))
                continue;

            var root = await doc.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var model = await doc.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var text = await doc.GetTextAsync(cancellationToken).ConfigureAwait(false);

            if (root is null || model is null)
                continue;

            var candidates = root.DescendantNodes()
                .Where(n => n is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax);

            foreach (var candidate in candidates)
            {
                var span = candidate.Span;
                if (span.End > text.Length)
                    continue;

                var startLine = text.Lines.GetLinePosition(span.Start).Line + 1;
                var endLine = text.Lines.GetLinePosition(span.End).Line + 1;
                var candidateRange = new LineRange(startLine, endLine);

                if (!file.ChangedRanges.Any(r => r.Intersects(candidateRange)))
                    continue;

                if (model.GetDeclaredSymbol(candidate, cancellationToken) is not IMethodSymbol symbol)
                    continue;

                methodSymbols[DispatchSet.KeyOf(symbol)] = symbol;
            }
        }

        return methodSymbols.Values.ToList();
    }

    private static Dictionary<string, Document> BuildDocumentIndex(Solution solution, bool includeTests)
    {
        var index = new Dictionary<string, Document>(PathNormalizer.Comparer);

        var documents = solution.Projects
            .Where(p => includeTests || !CallGraphWalker.IsTestProject(p))
            .SelectMany(p => p.Documents)
            .Where(d => !string.IsNullOrWhiteSpace(d.FilePath));

        foreach (var document in documents)
        {
            var key = PathNormalizer.Full(document.FilePath!);
            index.TryAdd(key, document);
        }

        return index;
    }
}
