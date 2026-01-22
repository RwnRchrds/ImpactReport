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
    int? Top)
{
    public bool All => Top is null;

    public static ImpactReportOptions Parse(string[] args)
    {
        if (args.Length == 0 || ArgParser.HasFlag(args, "-h", "--help"))
        {
            Console.WriteLine(HelpText.Text);
            Environment.Exit(0);
        }

        var sln = ArgParser.GetRequired(args, "--sln");
        if (!File.Exists(sln))
            throw new FileNotFoundException("Solution not found", sln);

        var output = ArgParser.GetOptional(args, "--out") ?? "impact-report.md";
        var areasPath = ArgParser.GetOptional(args, "--areas");
        var max = ArgParser.GetOptionalInt(args, "--max", 10);

        var changed = ArgParser.HasFlag(args, "--changed");
        var baseRef = ArgParser.GetOptional(args, "--base") ?? "origin/main";
        var includeTests = ArgParser.HasFlag(args, "--include-tests");

        var includeZero = ArgParser.HasFlag(args, "--include-zero");
        var all = ArgParser.HasFlag(args, "--all");

        // if --all is set => no top limit
        var top = all ? (int?)null : ArgParser.GetOptionalInt(args, "--top", 25);

        // default min-refs is 1 (hide zeros), unless includeZero is explicitly set
        var minRefsDefault = includeZero ? 0 : 1;
        var minRefs = ArgParser.GetOptionalInt(args, "--min-refs", minRefsDefault);

        var minProjects = ArgParser.GetOptionalInt(args, "--min-projects", 1);

        string? type = null;
        string? method = null;

        if (!changed)
        {
            type = ArgParser.GetRequired(args, "--type");
            method = ArgParser.GetRequired(args, "--method");
        }

        return new ImpactReportOptions(
            SolutionPath: sln,
            OutputPath: output,
            AreaMap: AreaMap.LoadOrEmpty(areasPath),
            MaxCallSitesPerProject: max,
            ChangedMode: changed,
            BaseRef: baseRef,
            IncludeTests: includeTests,
            TypeName: type,
            MethodName: method,

            IncludeZero: includeZero,
            MinRefs: minRefs,
            MinProjects: minProjects,
            Top: top);
    }
}