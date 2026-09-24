using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace ImpactReport.Analysis;

public sealed class ReferenceFinder(Solution solution)
{
    private readonly Dictionary<string, IReadOnlyList<ReferenceLocation>> _cache = new(StringComparer.Ordinal);

    public int SearchCount { get; private set; }

    public async Task<IReadOnlyList<ReferenceLocation>> FindAsync(
        IMethodSymbol method,
        CancellationToken cancellationToken = default)
    {
        var key = DispatchSet.KeyOf(method);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        SearchCount++;

        var keys = DispatchSet.For(method);

        var references = await SymbolFinder
            .FindReferencesAsync(method, solution, cancellationToken)
            .ConfigureAwait(false);

        var locations = new List<ReferenceLocation>();

        foreach (var reference in references)
        {
            if (reference.Definition is not IMethodSymbol definition)
                continue;

            var direct = keys.MatchesDirectly(definition);
            var ambiguous = !direct && keys.NeedsCallSiteCheck(definition);

            if (!direct && !ambiguous)
                continue;

            foreach (var location in reference.Locations)
            {
                if (location.IsImplicit || !location.Location.IsInSource || location.Document is null)
                    continue;

                if (ambiguous &&
                    !await ResolvesToThisMethodAsync(keys, location, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                locations.Add(location);
            }
        }

        _cache[key] = locations;
        return locations;
    }

    private static async Task<bool> ResolvesToThisMethodAsync(
        DispatchKeys keys,
        ReferenceLocation location,
        CancellationToken cancellationToken)
    {
        var document = location.Document;

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        if (root is null || model is null)
            return true;

        var node = root.FindToken(location.Location.SourceSpan.Start).Parent;

        for (; node is not null; node = node.Parent)
        {
            var symbol = model.GetSymbolInfo(node, cancellationToken).Symbol;

            if (symbol is IMethodSymbol invoked)
                return keys.MatchesCallSite(invoked);

            if (symbol is not null)
                break;
        }

        return true;
    }
}
