using ImpactReport.Analysis.Models;
using ImpactReport.Areas;
using Microsoft.CodeAnalysis;

namespace ImpactReport.Analysis;

public sealed record CallGraphOptions(
    int MaxDepth,
    int MaxNodes,
    bool IncludeTests,
    int MaxCallSitesPerProject);

public sealed record CallGraphWalkResult(
    IReadOnlyList<ReferenceHit> Hits,
    IReadOnlyList<AffectedMember> Members,
    int MaxDepthReached,
    bool Truncated);

public sealed class CallGraphWalker(ReferenceFinder finder, AreaMap areaMap, CallGraphOptions options)
{
    public async Task<CallGraphWalkResult> WalkAsync(
        IMethodSymbol seed,
        CancellationToken cancellationToken = default)
    {
        var hits = new List<ReferenceHit>();
        var members = new Dictionary<string, MemberAccumulator>(StringComparer.Ordinal);
        var searched = new HashSet<string>(StringComparer.Ordinal);
        var producedHits = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal) { DispatchSet.KeyOf(seed) };

        var frontier = new List<IMethodSymbol> { seed };
        var maxDepthReached = 0;
        var truncated = false;

        for (var depth = 1; depth <= options.MaxDepth && frontier.Count > 0; depth++)
        {
            var next = new List<IMethodSymbol>();

            foreach (var method in frontier)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var methodKey = DispatchSet.KeyOf(method);
                searched.Add(methodKey);

                foreach (var location in await finder.FindAsync(method, cancellationToken).ConfigureAwait(false))
                {
                    var document = location.Document;

                    if (!options.IncludeTests && IsTestProject(document.Project))
                        continue;

                    var built = await BuildHitAsync(document, location.Location.SourceSpan.Start, depth, cancellationToken)
                        .ConfigureAwait(false);

                    if (built is null)
                        continue;

                    var (hit, caller) = built.Value;

                    hits.Add(hit);
                    producedHits.Add(methodKey);
                    maxDepthReached = Math.Max(maxDepthReached, depth);

                    Accumulate(members, hit, caller, depth);

                    if (caller is null || depth >= options.MaxDepth)
                        continue;

                    if (visited.Count >= options.MaxNodes)
                    {
                        truncated = true;
                        continue;
                    }

                    if (visited.Add(DispatchSet.KeyOf(caller)))
                        next.Add(caller);
                }
            }

            frontier = next;
        }

        var affected = members.Values
            .Select(m => m.ToAffectedMember(searched, producedHits))
            .OrderBy(m => m.NearestDepth)
            .ThenByDescending(m => m.CallSites)
            .ThenBy(m => m.TypeName, StringComparer.Ordinal)
            .ThenBy(m => m.MemberName, StringComparer.Ordinal)
            .ToList();

        return new CallGraphWalkResult(hits, affected, maxDepthReached, truncated);
    }

    private static void Accumulate(
        Dictionary<string, MemberAccumulator> members,
        ReferenceHit hit,
        IMethodSymbol? caller,
        int depth)
    {
        if (hit.ContainingType is null)
            return;

        var memberName = hit.ContainingMember ?? "(declaration)";
        var key = $"{hit.ProjectName}|{hit.ContainingType}.{memberName}";

        if (!members.TryGetValue(key, out var accumulator))
        {
            accumulator = new MemberAccumulator(
                key,
                hit.ContainingType,
                memberName,
                hit.ProjectName,
                hit.ContainingNamespace,
                caller is null ? null : DispatchSet.KeyOf(caller),
                depth);

            members[key] = accumulator;
        }

        accumulator.Add(hit, depth);
    }

    private async Task<(ReferenceHit Hit, IMethodSymbol? Caller)?> BuildHitAsync(
        Document document,
        int position,
        int depth,
        CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (position < 0 || position >= text.Length)
            return null;

        var linePosition = text.Lines.GetLinePosition(position);
        var lineText = text.Lines[linePosition.Line].ToString().Trim();

        var callSite = await CallSiteResolver.ResolveAsync(document, position, cancellationToken).ConfigureAwait(false);

        var path = document.FilePath ?? document.Name;

        var areas = areaMap.MatchAreas(
            callSite.Namespace,
            path,
            document.Project.Name,
            callSite.TypeName);

        var hit = new ReferenceHit(
            ProjectName: document.Project.Name,
            DocumentPath: path,
            LineNumber: linePosition.Line + 1,
            LineText: lineText,
            ContainingNamespace: callSite.Namespace,
            ContainingType: callSite.TypeName,
            ContainingMember: callSite.MemberName,
            Depth: depth,
            Areas: areas);

        return (hit, callSite.EnclosingMethod);
    }

    public static bool IsTestProject(Project project) =>
        project.Name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
        project.Name.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
        project.Name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase);

    private sealed class MemberAccumulator(
        string key,
        string typeName,
        string memberName,
        string projectName,
        string? containingNamespace,
        string? methodKey,
        int firstDepth)
    {
        private readonly HashSet<string> _areas = new(StringComparer.Ordinal);

        public int NearestDepth { get; private set; } = firstDepth;

        public int CallSites { get; private set; }

        public void Add(ReferenceHit hit, int depth)
        {
            CallSites++;
            NearestDepth = Math.Min(NearestDepth, depth);

            foreach (var area in hit.Areas)
                _areas.Add(area);
        }

        public AffectedMember ToAffectedMember(ISet<string> searched, ISet<string> producedHits)
        {
            var beyondDepthLimit = methodKey is not null && !searched.Contains(methodKey);
            var isEntryPoint = methodKey is null || (!beyondDepthLimit && !producedHits.Contains(methodKey));

            return new AffectedMember(
                Key: key,
                TypeName: typeName,
                MemberName: memberName,
                ProjectName: projectName,
                Namespace: containingNamespace,
                NearestDepth: NearestDepth,
                CallSites: CallSites,
                Areas: _areas.OrderBy(a => a, StringComparer.Ordinal).ToList(),
                IsEntryPoint: isEntryPoint,
                BeyondDepthLimit: beyondDepthLimit);
        }
    }
}
