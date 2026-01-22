using ImpactReport.Analysis;
using ImpactReport.Analysis.Models;
using System.Text;

namespace ImpactReport.Reporting;

public static class MarkdownReportWriter
{
    // --------------------------
    // Public API (single method)
    // --------------------------

    public static void WriteToFile(ImpactResult result, string path)
        => File.WriteAllText(path, Build(result), Encoding.UTF8);

    public static string Build(ImpactResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Impact Report");
        sb.AppendLine();
        sb.AppendLine($"**Changed method:** `{result.ChangedMethodDisplay}`");
        sb.AppendLine();
        sb.AppendLine($"**Projects impacted:** {result.Projects.Count}");
        sb.AppendLine();

        if (result.Areas.Count > 0)
        {
            sb.AppendLine("## Impact Areas (inferred)");
            sb.AppendLine();
            foreach (var a in result.Areas)
                sb.AppendLine($"- **{a.Area}**: {a.ReferenceCount} reference(s)");
            sb.AppendLine();
        }

        sb.AppendLine("## Projects");
        sb.AppendLine();
        sb.AppendLine("| Project | References |");
        sb.AppendLine("|--------|------------|");
        foreach (var p in result.Projects.OrderByDescending(p => p.TotalReferences))
            sb.AppendLine($"| `{p.ProjectName}` | {p.TotalReferences} |");
        sb.AppendLine();

        // Detailed per-project sections
        foreach (var p in result.Projects.OrderByDescending(p => p.TotalReferences))
            AppendProjectDetails(sb, p);

        return sb.ToString();
    }

    // --------------------------
    // Public API (multi method)
    // --------------------------

    public static void WriteToFile(MultiImpactResult multi, string path)
        => File.WriteAllText(path, Build(multi), Encoding.UTF8);

    public static string Build(MultiImpactResult multi)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Impact Report");
        sb.AppendLine();
        sb.AppendLine($"Changed methods analysed: **{multi.Methods.Count}**");
        sb.AppendLine();

        // Summary table: method -> refs -> projects -> areas
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Method | Total refs | Projects | Areas |");
        sb.AppendLine("|--------|-----------:|---------:|------|");

        foreach (var m in multi.Methods)
        {
            var totalRefs = m.Result.Projects.Sum(p => p.TotalReferences);
            var projectCount = m.Result.Projects.Count;

            var areas = m.Result.Areas.Count > 0
                ? string.Join(", ", m.Result.Areas.Select(a => a.Area))
                : "-";

            sb.AppendLine($"| `{m.MethodDisplay}` | {totalRefs} | {projectCount} | {EscapeTable(areas)} |");
        }

        sb.AppendLine();

        // Per-method details
        foreach (var m in multi.Methods)
        {
            var totalRefs = m.Result.Projects.Sum(p => p.TotalReferences);

            sb.AppendLine($"## {m.MethodDisplay}");
            sb.AppendLine();
            sb.AppendLine($"Total references found: **{totalRefs}**");
            sb.AppendLine($"Projects impacted: **{m.Result.Projects.Count}**");
            sb.AppendLine();

            if (m.Result.Areas.Count > 0)
            {
                sb.AppendLine("### Impact Areas (inferred)");
                sb.AppendLine();
                foreach (var a in m.Result.Areas)
                    sb.AppendLine($"- **{a.Area}**: {a.ReferenceCount} reference(s)");
                sb.AppendLine();
            }

            sb.AppendLine("### Projects");
            sb.AppendLine();
            sb.AppendLine("| Project | References | Top namespace |");
            sb.AppendLine("|--------|-----------:|--------------|");

            foreach (var p in m.Result.Projects.OrderByDescending(p => p.TotalReferences))
            {
                var topNs = p.TopNamespaces.FirstOrDefault().Namespace ?? "-";
                sb.AppendLine($"| `{p.ProjectName}` | {p.TotalReferences} | `{EscapeInlineCode(topNs)}` |");
            }

            sb.AppendLine();

            // Detailed per-project sections (still useful, but can be noisy)
            foreach (var p in m.Result.Projects.OrderByDescending(p => p.TotalReferences))
                AppendProjectDetails(sb, p);
        }

        return sb.ToString();
    }

    // --------------------------
    // Helpers
    // --------------------------

    private static void AppendProjectDetails(StringBuilder sb, ProjectImpact p)
    {
        sb.AppendLine($"### {p.ProjectName}");
        sb.AppendLine();
        sb.AppendLine($"References found: **{p.TotalReferences}**");
        sb.AppendLine();

        if (p.TopNamespaces.Count > 0)
        {
            sb.AppendLine("Top namespaces:");
            foreach (var ns in p.TopNamespaces)
                sb.AppendLine($"- `{EscapeInlineCode(ns.Namespace)}` ({ns.Count})");
            sb.AppendLine();
        }

        if (p.SampleHits.Count > 0)
        {
            sb.AppendLine("Sample call sites:");
            sb.AppendLine();
            sb.AppendLine("| File | Line | Code |");
            sb.AppendLine("|------|-----:|------|");

            foreach (var hit in p.SampleHits)
            {
                var file = Path.GetFileName(hit.DocumentPath);
                var code = EscapeInlineCode(hit.LineText ?? string.Empty);
                sb.AppendLine($"| `{EscapeInlineCode(file)}` | {hit.LineNumber} | `{code}` |");
            }

            sb.AppendLine();
        }
    }

    private static string EscapeTable(string value)
        => (value ?? string.Empty).Replace("|", "\\|");

    private static string EscapeInlineCode(string value)
    {
        // Markdown inline code uses backticks. If code contains backticks, escape by doubling.
        value ??= string.Empty;
        return value.Replace("`", "``").Replace("|", "\\|");
    }
}