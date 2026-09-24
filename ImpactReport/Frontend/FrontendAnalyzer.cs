using ImpactReport.Analysis.Models;
using ImpactReport.Areas;
using ImpactReport.Utils;

namespace ImpactReport.Frontend;

public sealed record FrontendOptions(string Root, IReadOnlyList<string> Extensions);

public static class FrontendAnalyzer
{
    public static FrontendImpact Analyze(
        FrontendIndex index,
        IReadOnlyList<string> changedPaths,
        IReadOnlyList<AffectedMember> affectedMembers,
        string repoRoot,
        AreaMap areaMap)
    {
        var changes = BuildChanges(index, changedPaths, repoRoot, areaMap);
        var (screens, unmatched) = BuildScreens(index, affectedMembers, areaMap);

        return new FrontendImpact(changes, screens, unmatched, index.Count);
    }

    private static IReadOnlyList<FrontendChange> BuildChanges(
        FrontendIndex index,
        IReadOnlyList<string> changedPaths,
        string repoRoot,
        AreaMap areaMap)
    {
        var byPath = index.Files.ToDictionary(f => f.AbsolutePath, PathNormalizer.Comparer);

        return changedPaths
            .Select(relative =>
            {
                var absolute = PathNormalizer.Combine(repoRoot, relative);
                var feature = FrontendScanner.FeatureOf(relative);

                var segments = byPath.TryGetValue(absolute, out var known)
                    ? known.ApiSegments
                    : [];

                return new FrontendChange(
                    RelativePath: relative,
                    Feature: feature,
                    Areas: areaMap.MatchAreas(null, absolute, null, null),
                    ApiSegments: segments);
            })
            .OrderBy(c => c.Feature ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(c => c.RelativePath, StringComparer.Ordinal)
            .ToList();
    }

    private static (IReadOnlyList<FrontendScreen> Screens, IReadOnlyList<string> Unmatched) BuildScreens(
        FrontendIndex index,
        IReadOnlyList<AffectedMember> affectedMembers,
        AreaMap areaMap)
    {
        var byFeature = new Dictionary<string, ScreenAccumulator>(StringComparer.OrdinalIgnoreCase);
        var unmatched = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var member in affectedMembers)
        {
            foreach (var endpoint in FrontendScanner.EndpointKeysFor(member.TypeName, member.MemberName))
            {
                if (!index.Knows(endpoint))
                {
                    if (LooksLikeEndpoint(member, endpoint))
                        unmatched.Add(endpoint);

                    continue;
                }

                foreach (var file in index.CallersOf(endpoint))
                {
                    var feature = file.Feature ?? "(unknown)";

                    if (!byFeature.TryGetValue(feature, out var accumulator))
                    {
                        accumulator = new ScreenAccumulator(feature);
                        byFeature[feature] = accumulator;
                    }

                    accumulator.Add(file, endpoint, areaMap);
                }
            }
        }

        var screens = byFeature.Values
            .Select(a => a.ToScreen())
            .OrderByDescending(s => s.FileCount)
            .ThenBy(s => s.Feature, StringComparer.Ordinal)
            .ToList();

        return (screens, unmatched.ToList());
    }

    private static bool LooksLikeEndpoint(AffectedMember member, string endpoint) =>
        member.TypeName?.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) == true &&
        !endpoint.Equals(member.MemberName, StringComparison.Ordinal);

    private sealed class ScreenAccumulator(string feature)
    {
        private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly SortedSet<string> _endpoints = new(StringComparer.OrdinalIgnoreCase);
        private readonly SortedSet<string> _areas = new(StringComparer.Ordinal);

        public void Add(FrontendFile file, string endpoint, AreaMap areaMap)
        {
            _files.Add(file.AbsolutePath);
            _endpoints.Add(endpoint);

            foreach (var area in areaMap.MatchAreas(null, file.AbsolutePath, null, null))
                _areas.Add(area);
        }

        public FrontendScreen ToScreen() =>
            new(feature, _areas.ToList(), _endpoints.ToList(), _files.Count);
    }
}
