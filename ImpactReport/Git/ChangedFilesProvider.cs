namespace ImpactReport.Git;

public static class ChangedFilesProvider
{
    public static IReadOnlyList<ChangedFile> GetChangedCsFiles(string baseRef, string repoRoot, bool includeUncommitted = false)
    {
        var spec = DiffSpec(baseRef, repoRoot, includeUncommitted);

        var files = ListChangedPaths(spec, repoRoot)
            .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(IsAnalysableSource)
            .ToList();

        var result = new List<ChangedFile>(files.Count);

        foreach (var relativePath in files)
        {
            var diff = GitRunner.Run(
                $"-c core.quotepath=false diff --unified=0 {spec} -- \"{relativePath}\"",
                repoRoot);

            result.Add(new ChangedFile(relativePath, GitDiffParser.ParseChangedNewFileLineRanges(diff)));
        }

        return result;
    }

    public static IReadOnlyList<string> GetChangedPathsWithExtension(
        string baseRef,
        string repoRoot,
        IReadOnlyCollection<string> extensions,
        bool includeUncommitted = false)
    {
        return ListChangedPaths(DiffSpec(baseRef, repoRoot, includeUncommitted), repoRoot)
            .Where(p => extensions.Any(e => p.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            .Where(IsBuildOutput)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    private static string DiffSpec(string baseRef, string repoRoot, bool includeUncommitted)
    {
        if (!GitRunner.RefExists(baseRef, repoRoot))
        {
            throw new GitException(
                $"Git ref '{baseRef}' does not exist in this repository.\n" +
                "Pass a different ref with --base (e.g. --base origin/master), or fetch it first.");
        }

        return includeUncommitted
            ? GitRunner.MergeBase(baseRef, repoRoot)
            : $"{baseRef}...HEAD";
    }

    private static IReadOnlyList<string> ListChangedPaths(string spec, string repoRoot)
    {
        var listing = GitRunner.Run($"-c core.quotepath=false diff --name-only {spec}", repoRoot);

        return listing
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim('\r', '"'))
            .Where(p => p.Length > 0)
            .ToList();
    }

    private static bool IsBuildOutput(string path)
    {
        var segments = path.Split('/');

        return !segments.Any(s =>
            s.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("dist", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("node_modules", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAnalysableSource(string path)
    {
        if (!IsBuildOutput(path))
            return false;

        var fileName = path.Split('/')[^1];

        return !fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
               && !fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
               && !fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
               && !fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase);
    }
}
