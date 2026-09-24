namespace ImpactReport.Git;

public sealed record ChangedFile(string RelativePath, IReadOnlyList<LineRange> ChangedRanges);

public sealed record LineRange(int StartLine, int EndLine)
{
    public bool Intersects(LineRange other) =>
        !(EndLine < other.StartLine || StartLine > other.EndLine);

    public bool ContainsLine(int line) => line >= StartLine && line <= EndLine;
}
