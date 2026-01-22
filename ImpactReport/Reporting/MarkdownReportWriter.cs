using ImpactReport.Analysis.Models;
using System.Text;

namespace ImpactReport.Reporting;

public static class MarkdownReportWriter
{
    public static void WriteToFile(ImpactResult result, string path)
    {
        var md = Build(result);
        File.WriteAllText(path, md, Encoding.UTF8);
    }

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

        foreach (var p in result.Projects)
        {
            sb.AppendLine($"## {p.ProjectName}");
            sb.AppendLine();
            sb.AppendLine($"References found: **{p.TotalReferences}**");
            sb.AppendLine();

            sb.AppendLine("Top namespaces:");
            foreach (var ns in p.TopNamespaces)
                sb.AppendLine($"- `{ns.Namespace}` ({ns.Count})");
            sb.AppendLine();

            sb.AppendLine("Sample call sites:");
            sb.AppendLine();
            sb.AppendLine("| File | Line | Code |");
            sb.AppendLine("|------|------|------|");

            foreach (var hit in p.SampleHits)
            {
                var file = Path.GetFileName(hit.DocumentPath);
                var code = (hit.LineText ?? string.Empty).Replace("|", "\\|");
                sb.AppendLine($"| `{file}` | {hit.LineNumber} | `{code}` |");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}