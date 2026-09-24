using System.Text;
using ImpactReport.Analysis;
using ImpactReport.Analysis.Models;
using ImpactReport.Frontend;

namespace ImpactReport.Reporting;

public static class MarkdownReportWriter
{
    public static void WriteToFile(ImpactResult result, string path)
        => WriteAllText(path, Build(result, FrontendImpact.Empty));

    public static void WriteToFile(ImpactResult result, FrontendImpact frontend, string path)
        => WriteAllText(path, Build(result, frontend));

    public static void WriteToFile(MultiImpactResult multi, string path)
        => WriteAllText(path, Build(multi, FrontendImpact.Empty));

    public static void WriteToFile(MultiImpactResult multi, FrontendImpact frontend, string path)
        => WriteAllText(path, Build(multi, frontend));

    public static string Build(ImpactResult result) => Build(result, FrontendImpact.Empty);

    public static string Build(ImpactResult result, FrontendImpact frontend)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Impact Report");
        sb.AppendLine();
        sb.AppendLine($"**Changed method:** `{Code(result.ChangedMethodDisplay)}`");
        sb.AppendLine();
        sb.AppendLine($"**Call sites:** {result.TotalReferences} across {result.Projects.Count} project(s)");
        sb.AppendLine();

        AppendHeadlineAreas(sb, result.Areas, frontend);
        AppendAreas(sb, result.Areas, "##");
        AppendTruncationNote(sb, result.Truncated);

        AppendAffectedProjects(sb, ReferenceAnalyzer.BuildAffectedProjects(result.Members));
        AppendAffectedMembers(sb, result.Members);
        AppendFrontend(sb, frontend);

        AppendProjectTable(sb, result.Projects, "##");

        foreach (var project in result.Projects)
            AppendProjectDetails(sb, project, "###");

        return sb.ToString();
    }

    public static string Build(MultiImpactResult multi) => Build(multi, FrontendImpact.Empty);

    public static string Build(MultiImpactResult multi, FrontendImpact frontend)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Impact Report");
        sb.AppendLine();
        sb.AppendLine($"Analysed **{multi.Methods.Count}** of **{multi.MethodsDetected}** changed method(s).");
        sb.AppendLine();

        AppendHeadlineAreas(sb, multi.Areas, frontend);
        AppendAreas(sb, multi.Areas, "##");
        AppendTruncationNote(sb, multi.Truncated);

        AppendAffectedProjects(sb, multi.Projects);
        AppendAffectedMembers(sb, multi.Members);
        AppendFrontend(sb, frontend);

        AppendSummaryTable(sb, multi);

        foreach (var method in multi.Methods)
            AppendMethodSection(sb, method);

        return sb.ToString();
    }

    private static void AppendHeadlineAreas(StringBuilder sb, IReadOnlyList<AreaImpact> areas, FrontendImpact frontend)
    {
        var combined = areas.Select(a => a.Area)
            .Concat(frontend.Areas)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (combined.Count == 0)
            return;

        sb.AppendLine($"**Impact Areas: {EscapeTable(string.Join(", ", combined))}**");
        sb.AppendLine();
    }

    private static void AppendFrontend(StringBuilder sb, FrontendImpact frontend)
    {
        if (!frontend.HasAnything)
            return;

        sb.AppendLine("## Frontend");
        sb.AppendLine();
        sb.AppendLine(
            $"Matched against {frontend.FilesScanned} frontend file(s) that call an API endpoint.");
        sb.AppendLine();

        if (frontend.Screens.Count > 0)
        {
            sb.AppendLine("### Screens calling affected endpoints");
            sb.AppendLine();
            sb.AppendLine("| Feature | Files | Via endpoint | Areas |");
            sb.AppendLine("|---------|------:|--------------|-------|");

            foreach (var screen in frontend.Screens)
            {
                var areas = screen.Areas.Count > 0 ? string.Join(", ", screen.Areas) : "-";

                sb.AppendLine(
                    $"| `{Code(screen.Feature)}` | {screen.FileCount} " +
                    $"| {EscapeTable(string.Join(", ", screen.ViaEndpoints))} | {EscapeTable(areas)} |");
            }

            sb.AppendLine();
        }

        if (frontend.Changes.Count > 0)
        {
            sb.AppendLine("### Frontend files changed on this branch");
            sb.AppendLine();
            sb.AppendLine("| File | Feature | Areas | Calls |");
            sb.AppendLine("|------|---------|-------|-------|");

            foreach (var change in frontend.Changes)
            {
                var areas = change.Areas.Count > 0 ? string.Join(", ", change.Areas) : "-";
                var calls = change.ApiSegments.Count > 0 ? string.Join(", ", change.ApiSegments) : "-";

                sb.AppendLine(
                    $"| `{Code(change.RelativePath)}` | {EscapeTable(change.Feature ?? "-")} " +
                    $"| {EscapeTable(areas)} | {EscapeTable(calls)} |");
            }

            sb.AppendLine();
        }

        if (frontend.UnmatchedEndpoints.Count > 0)
        {
            sb.AppendLine(
                $"<sub>No frontend caller found for {frontend.UnmatchedEndpoints.Count} affected endpoint(s): " +
                $"{EscapeTable(string.Join(", ", frontend.UnmatchedEndpoints.Take(15)))}. " +
                "These may be called by another client, a background job, or a route that renames the controller.</sub>");
            sb.AppendLine();
        }
    }

    private static void AppendAffectedProjects(StringBuilder sb, IReadOnlyList<AffectedProject> projects)
    {
        if (projects.Count == 0)
            return;

        sb.AppendLine("## Affected Projects");
        sb.AppendLine();
        sb.AppendLine("| Project | Members | Call sites | Nearest hop | Areas |");
        sb.AppendLine("|---------|--------:|-----------:|------------:|-------|");

        foreach (var project in projects)
        {
            var areas = project.Areas.Count > 0 ? string.Join(", ", project.Areas) : "-";

            sb.AppendLine(
                $"| `{Code(project.ProjectName)}` | {project.MemberCount} | {project.CallSites} " +
                $"| {project.NearestDepth} | {EscapeTable(areas)} |");
        }

        sb.AppendLine();
    }

    private static void AppendAffectedMembers(StringBuilder sb, IReadOnlyList<AffectedMember> members)
    {
        if (members.Count == 0)
            return;

        var entryPoints = members.Where(m => m.IsEntryPoint).ToList();

        if (entryPoints.Count > 0)
        {
            sb.AppendLine("## Affected Entry Points");
            sb.AppendLine();
            sb.AppendLine("Nothing in the solution calls these, so they are where the change surfaces.");
            sb.AppendLine();
            AppendMemberTable(sb, entryPoints);
        }

        sb.AppendLine("## Affected Members");
        sb.AppendLine();
        sb.AppendLine($"Every member that transitively depends on the change ({members.Count} in total).");
        sb.AppendLine();
        AppendMemberTable(sb, members);
    }

    private static void AppendMemberTable(StringBuilder sb, IReadOnlyList<AffectedMember> members)
    {
        sb.AppendLine("| Member | Project | Hop | Call sites | Areas |");
        sb.AppendLine("|--------|---------|----:|-----------:|-------|");

        foreach (var member in members)
        {
            var areas = member.Areas.Count > 0 ? string.Join(", ", member.Areas) : "-";
            var suffix = member.BeyondDepthLimit ? " *" : string.Empty;

            sb.AppendLine(
                $"| `{Code(member.Display)}`{suffix} | `{Code(member.ProjectName)}` | {member.NearestDepth} " +
                $"| {member.CallSites} | {EscapeTable(areas)} |");
        }

        sb.AppendLine();

        if (members.Any(m => m.BeyondDepthLimit))
        {
            sb.AppendLine("<sub>* sits at the `--depth` limit, so its own callers were never searched.</sub>");
            sb.AppendLine();
        }
    }

    private static void AppendSummaryTable(StringBuilder sb, MultiImpactResult multi)
    {
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Method | Call sites | Projects | Areas |");
        sb.AppendLine("|--------|-----------:|---------:|-------|");

        foreach (var method in multi.Methods)
        {
            var areas = method.Result.Areas.Count > 0
                ? string.Join(", ", method.Result.Areas.Select(a => a.Area))
                : "-";

            sb.AppendLine(
                $"| `{Code(method.MethodDisplay)}` | {method.Result.TotalReferences} " +
                $"| {method.Result.Projects.Count} | {EscapeTable(areas)} |");
        }

        sb.AppendLine();
    }

    private static void AppendMethodSection(StringBuilder sb, MethodImpact method)
    {
        sb.AppendLine($"## {EscapeTable(method.MethodDisplay)}");
        sb.AppendLine();
        sb.AppendLine($"Call sites: **{method.Result.TotalReferences}**  ");
        sb.AppendLine($"Projects impacted: **{method.Result.Projects.Count}**  ");
        sb.AppendLine($"Deepest hop reached: **{method.Result.MaxDepthReached}**");
        sb.AppendLine();

        AppendAreas(sb, method.Result.Areas, "###");
        AppendProjectTable(sb, method.Result.Projects, "###");

        foreach (var project in method.Result.Projects)
            AppendProjectDetails(sb, project, "####");
    }

    private static void AppendAreas(StringBuilder sb, IReadOnlyList<AreaImpact> areas, string heading)
    {
        if (areas.Count == 0)
            return;

        sb.AppendLine($"{heading} Impact Areas");
        sb.AppendLine();
        sb.AppendLine($"**{EscapeTable(string.Join(", ", areas.Select(a => a.Area)))}**");
        sb.AppendLine();
        sb.AppendLine("| Area | Call sites | Projects | Nearest hop |");
        sb.AppendLine("|------|-----------:|---------:|------------:|");

        foreach (var area in areas)
        {
            sb.AppendLine(
                $"| {EscapeTable(area.Area)} | {area.ReferenceCount} | {area.ProjectCount} | {area.NearestDepth} |");
        }

        sb.AppendLine();
        sb.AppendLine("<sub>Nearest hop: 1 = calls the changed code directly; higher = reached indirectly.</sub>");
        sb.AppendLine();
    }

    private static void AppendTruncationNote(StringBuilder sb, bool truncated)
    {
        if (!truncated)
            return;

        sb.AppendLine("> **Note:** the call graph walk hit `--max-nodes`, so these results are partial.");
        sb.AppendLine();
    }

    private static void AppendProjectTable(StringBuilder sb, IReadOnlyList<ProjectImpact> projects, string heading)
    {
        if (projects.Count == 0)
        {
            sb.AppendLine("No call sites found.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"{heading} Projects");
        sb.AppendLine();
        sb.AppendLine("| Project | Call sites | Top namespace |");
        sb.AppendLine("|---------|-----------:|---------------|");

        foreach (var project in projects)
        {
            var topNamespace = project.TopNamespaces.Count > 0 ? project.TopNamespaces[0].Namespace : "-";
            sb.AppendLine($"| `{Code(project.ProjectName)}` | {project.TotalReferences} | `{Code(topNamespace)}` |");
        }

        sb.AppendLine();
    }

    private static void AppendProjectDetails(StringBuilder sb, ProjectImpact project, string heading)
    {
        sb.AppendLine($"{heading} {EscapeTable(project.ProjectName)}");
        sb.AppendLine();
        sb.AppendLine($"Call sites: **{project.TotalReferences}**");
        sb.AppendLine();

        if (project.TopNamespaces.Count > 0)
        {
            sb.AppendLine("Top namespaces:");
            sb.AppendLine();

            foreach (var (@namespace, count) in project.TopNamespaces)
                sb.AppendLine($"- `{Code(@namespace)}` ({count})");

            sb.AppendLine();
        }

        if (project.SampleHits.Count == 0)
            return;

        sb.AppendLine("Sample call sites:");
        sb.AppendLine();
        sb.AppendLine("| File | Line | Hop | Caller | Code |");
        sb.AppendLine("|------|-----:|----:|--------|------|");

        foreach (var hit in project.SampleHits)
        {
            sb.AppendLine(
                $"| `{Code(Path.GetFileName(hit.DocumentPath))}` | {hit.LineNumber} | {hit.Depth} " +
                $"| `{Code(CallerName(hit))}` | `{Code(hit.LineText)}` |");
        }

        sb.AppendLine();
    }

    private static string CallerName(ReferenceHit hit)
    {
        var type = ShortTypeName(hit.ContainingType);

        if (type is null)
            return hit.ContainingMember ?? "-";

        return hit.ContainingMember is null ? type : $"{type}.{hit.ContainingMember}";
    }

    private static void WriteAllText(string path, string content)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string? ShortTypeName(string? fullName)
    {
        if (string.IsNullOrEmpty(fullName))
            return null;

        var lastDot = fullName.LastIndexOf('.');
        return lastDot < 0 ? fullName : fullName[(lastDot + 1)..];
    }

    private static string EscapeTable(string? value)
        => (value ?? string.Empty).Replace("|", "\\|");

    private static string Code(string? value)
        => (value ?? string.Empty).Replace("`", "'").Replace("|", "\\|");
}
