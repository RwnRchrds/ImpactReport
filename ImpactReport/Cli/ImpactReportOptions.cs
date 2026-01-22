using ImpactReport.Areas;
using ImpactReport.Utils;

namespace ImpactReport.Cli;

public sealed record ImpactReportOptions(
    string SolutionPath,
    string TypeName,
    string MethodName,
    string OutputPath,
    AreaMap AreaMap,
    int MaxCallSitesPerProject)
{
    public static ImpactReportOptions Parse(string[] args)
    {
        var sln = ArgParser.GetRequired(args, "--sln");
        var type = ArgParser.GetRequired(args, "--type");
        var method = ArgParser.GetRequired(args, "--method");

        var output = ArgParser.GetOptional(args, "--out") ?? "impact-report.md";
        var areasPath = ArgParser.GetOptional(args, "--areas");
        var max = ArgParser.GetOptionalInt(args, "--max", 10);

        if (!File.Exists(sln))
            throw new FileNotFoundException("Solution not found", sln);

        var areaMap = AreaMap.LoadOrEmpty(areasPath);

        return new ImpactReportOptions(
            SolutionPath: sln,
            TypeName: type,
            MethodName: method,
            OutputPath: output,
            AreaMap: areaMap,
            MaxCallSitesPerProject: max);
    }
}