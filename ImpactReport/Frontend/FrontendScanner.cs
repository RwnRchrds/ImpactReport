using System.Text.RegularExpressions;
using ImpactReport.Utils;

namespace ImpactReport.Frontend;

public sealed class FrontendIndex(IReadOnlyList<FrontendFile> files)
{
    private readonly ILookup<string, FrontendFile> _byEndpoint = files
        .SelectMany(f => f.ApiSegments.Select(s => (Segment: s, File: f)))
        .ToLookup(x => x.Segment, x => x.File, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<FrontendFile> Files => files;

    public int Count => files.Count;

    public IEnumerable<FrontendFile> CallersOf(string endpoint) => _byEndpoint[endpoint];

    public bool Knows(string endpoint) => _byEndpoint.Contains(endpoint);

    public IReadOnlyList<string> Endpoints =>
        _byEndpoint.Select(g => g.Key).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
}

public static class FrontendScanner
{
    private static readonly Regex ApiSegment = new(
        @"api/(?<segment>[A-Za-z0-9_]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] IgnoredDirectories =
        ["node_modules", "dist", "bin", "obj", ".git", ".angular", "coverage"];

    public static FrontendIndex Scan(string root, CancellationToken cancellationToken = default)
    {
        var files = new List<FrontendFile>();

        foreach (var path in EnumerateSources(root, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException)
            {
                continue;
            }

            var segments = ApiSegment.Matches(text)
                .Select(m => m.Groups["segment"].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (segments.Count == 0)
                continue;

            files.Add(new FrontendFile(
                AbsolutePath: PathNormalizer.Full(path),
                RelativePath: Relative(root, path),
                Feature: FeatureOf(path),
                ApiSegments: segments));
        }

        return new FrontendIndex(files);
    }

    public static string? FeatureOf(string path)
    {
        var parts = PathNormalizer.ToForwardSlashes(path).Split('/');

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("features", StringComparison.OrdinalIgnoreCase))
                return parts[i + 1];
        }

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("app", StringComparison.OrdinalIgnoreCase))
                return parts[i + 1];
        }

        return null;
    }

    public static IReadOnlyList<string> EndpointKeysFor(string? typeName, string? memberName)
    {
        var keys = new List<string>();

        var shortType = ShortName(typeName);

        if (shortType is not null && shortType.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
            keys.Add(shortType[..^"Controller".Length]);

        if (!string.IsNullOrWhiteSpace(memberName))
            keys.Add(memberName);

        return keys;
    }

    private static IEnumerable<string> EnumerateSources(string root, CancellationToken cancellationToken)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directory = stack.Pop();

            string[] subdirectories;
            string[] entries;

            try
            {
                subdirectories = Directory.GetDirectories(directory);
                entries = Directory.GetFiles(directory, "*.ts");
            }
            catch (Exception e) when (e is UnauthorizedAccessException or DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var subdirectory in subdirectories)
            {
                var name = Path.GetFileName(subdirectory);

                if (!IgnoredDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
                    stack.Push(subdirectory);
            }

            foreach (var entry in entries)
            {
                if (!entry.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase))
                    yield return entry;
            }
        }
    }

    private static string Relative(string root, string path)
    {
        var full = PathNormalizer.Full(path);
        var prefix = PathNormalizer.Full(root) + "/";

        return full.StartsWith(prefix, PathNormalizer.Comparison) ? full[prefix.Length..] : full;
    }

    private static string? ShortName(string? fullName)
    {
        if (string.IsNullOrEmpty(fullName))
            return null;

        var lastDot = fullName.LastIndexOf('.');
        return lastDot < 0 ? fullName : fullName[(lastDot + 1)..];
    }
}
