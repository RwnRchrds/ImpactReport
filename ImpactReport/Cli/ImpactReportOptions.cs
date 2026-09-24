using ImpactReport.Areas;
using ImpactReport.Utils;

namespace ImpactReport.Cli;

public sealed record ImpactReportOptions(
    string SolutionPath,
    string OutputPath,
    AreaMap AreaMap,
    int MaxCallSitesPerProject,
    bool ChangedMode,
    string BaseRef,
    bool IncludeTests,
    string? TypeName,
    string? MethodName,
    bool IncludeZero,
    int MinRefs,
    int MinProjects,
    int? Top,
    int Depth,
    int MaxNodes,
    bool Quiet,
    bool IncludeUncommitted,
    bool Frontend,
    string? FrontendRoot,
    IReadOnlyList<string> FrontendExtensions)
{
    public bool All => Top is null;

    private static readonly HashSet<string> ValueOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "--sln", "--type", "--method", "--base", "--top", "--min-refs", "--min-projects",
        "--out", "--areas", "--max", "--depth", "--max-nodes", "--frontend-root", "--frontend-ext"
    };

    private static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase)
    {
        "--changed", "--all", "--include-zero", "--include-tests", "--quiet", "--frontend", "--uncommitted", "-h", "--help"
    };

    public static ImpactReportOptions? Parse(string[] args)
    {
        if (args.Length == 0 || ArgParser.HasFlag(args, "-h", "--help"))
            return null;

        ArgParser.RejectUnknown(args, ValueOptions, Flags);

        var sln = ArgParser.GetRequired(args, "--sln");
        if (!File.Exists(sln))
            throw new CommandLineException($"Solution not found: {sln}");

        var changed = ArgParser.HasFlag(args, "--changed");
        var includeZero = ArgParser.HasFlag(args, "--include-zero");
        var all = ArgParser.HasFlag(args, "--all");
        var frontend = ArgParser.HasFlag(args, "--frontend") || ArgParser.HasFlag(args, "--frontend-root");

        string? type = null;
        string? method = null;

        if (changed)
        {
            if (ArgParser.HasFlag(args, "--type") || ArgParser.HasFlag(args, "--method"))
                throw new CommandLineException("--type/--method cannot be combined with --changed.");
        }
        else
        {
            type = ArgParser.GetRequired(args, "--type");
            method = ArgParser.GetRequired(args, "--method");
        }

        return new ImpactReportOptions(
            SolutionPath: Path.GetFullPath(sln),
            OutputPath: ArgParser.GetOptional(args, "--out") ?? "impact-report.md",
            AreaMap: AreaMap.LoadOrEmpty(ArgParser.GetOptional(args, "--areas")),
            MaxCallSitesPerProject: ArgParser.GetOptionalInt(args, "--max", 10, min: 1),
            ChangedMode: changed,
            BaseRef: ArgParser.GetOptional(args, "--base") ?? "origin/main",
            IncludeTests: ArgParser.HasFlag(args, "--include-tests"),
            TypeName: type,
            MethodName: method,
            IncludeZero: includeZero,
            MinRefs: ArgParser.GetOptionalInt(args, "--min-refs", includeZero ? 0 : 1, min: 0),
            MinProjects: ArgParser.GetOptionalInt(args, "--min-projects", 1, min: 0),
            Top: all ? null : ArgParser.GetOptionalInt(args, "--top", 25, min: 1),
            Depth: ArgParser.GetOptionalInt(args, "--depth", 3, min: 1),
            MaxNodes: ArgParser.GetOptionalInt(args, "--max-nodes", 2000, min: 1),
            Quiet: ArgParser.HasFlag(args, "--quiet"),
            IncludeUncommitted: ArgParser.HasFlag(args, "--uncommitted"),
            Frontend: frontend,
            FrontendRoot: ArgParser.GetOptional(args, "--frontend-root"),
            FrontendExtensions: ParseExtensions(ArgParser.GetOptional(args, "--frontend-ext")));
    }

    private static IReadOnlyList<string> ParseExtensions(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [".ts", ".html", ".scss", ".css"];

        var extensions = raw
            .Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.StartsWith(".", StringComparison.Ordinal) ? e : "." + e)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (extensions.Count == 0)
            throw new CommandLineException("--frontend-ext needs at least one extension, e.g. --frontend-ext .ts,.html");

        return extensions;
    }
}
