namespace ImpactReport.Git;

public static class ChangedFilesProvider
{
    public static IReadOnlyList<ChangedFile> GetChangedCsFiles(string baseRef, string repoRoot)
    {
        // List changed files between base...HEAD
        var list = GitRunner.Run($"diff --name-only {baseRef}...HEAD", repoRoot);

        var files = list
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.Contains(@"/obj/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var result = new List<ChangedFile>();

        foreach (var relPath in files)
        {
            // unified=0 gives precise affected line ranges
            var diff = GitRunner.Run($"diff --unified=0 {baseRef}...HEAD -- \"{relPath}\"", repoRoot);
            var ranges = GitDiffParser.ParseChangedNewFileLineRanges(diff);

            // If file changed but only deletions, ranges could be empty. Keep it anyway (we'll get no methods).
            result.Add(new ChangedFile(relPath, ranges));
        }

        return result;
    }
}