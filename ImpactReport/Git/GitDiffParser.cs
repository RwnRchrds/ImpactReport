using System.Text.RegularExpressions;

namespace ImpactReport.Git;

public static class GitDiffParser
{
    // Use: git diff --unified=0 base...HEAD -- <file>
    // Hunks look like: @@ -oldStart,oldLen +newStart,newLen @@
    private static readonly Regex HunkRegex = new(
        @"@@\s-\d+(?:,\d+)?\s\+(?<start>\d+)(?:,(?<len>\d+))?\s@@",
        RegexOptions.Compiled);

    public static IReadOnlyList<LineRange> ParseChangedNewFileLineRanges(string unifiedDiff)
    {
        var ranges = new List<LineRange>();

        foreach (Match m in HunkRegex.Matches(unifiedDiff))
        {
            var start = int.Parse(m.Groups["start"].Value);
            var lenGroup = m.Groups["len"].Value;

            // If len omitted, it's 1 line
            var len = string.IsNullOrWhiteSpace(lenGroup) ? 1 : int.Parse(lenGroup);

            // A len of 0 means "pure deletion" in new file (no new lines added)
            if (len <= 0) continue;

            ranges.Add(new LineRange(start, start + len - 1));
        }

        return ranges;
    }
}