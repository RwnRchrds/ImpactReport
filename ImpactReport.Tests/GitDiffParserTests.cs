using ImpactReport.Git;
using Xunit;

namespace ImpactReport.Tests;

public class GitDiffParserTests
{
    [Fact]
    public void Parses_hunk_with_explicit_length()
    {
        var ranges = GitDiffParser.ParseChangedNewFileLineRanges(
            "@@ -10,3 +12,5 @@ public void Foo()");

        var range = Assert.Single(ranges);
        Assert.Equal(12, range.StartLine);
        Assert.Equal(16, range.EndLine);
    }

    [Fact]
    public void Omitted_length_means_a_single_line()
    {
        var ranges = GitDiffParser.ParseChangedNewFileLineRanges("@@ -10 +12 @@");

        var range = Assert.Single(ranges);
        Assert.Equal(12, range.StartLine);
        Assert.Equal(12, range.EndLine);
    }

    [Fact]
    public void Pure_deletion_contributes_no_range()
    {
        var ranges = GitDiffParser.ParseChangedNewFileLineRanges("@@ -40,7 +40,0 @@");

        Assert.Empty(ranges);
    }

    [Fact]
    public void Parses_every_hunk_in_a_multi_hunk_diff()
    {
        const string diff = """
            diff --git a/Foo.cs b/Foo.cs
            index 1111111..2222222 100644
            --- a/Foo.cs
            +++ b/Foo.cs
            @@ -1,2 +1,3 @@
            +using System;
            @@ -30,0 +31,2 @@ public class Foo
            +    public int Bar() => 1;
            """;

        var ranges = GitDiffParser.ParseChangedNewFileLineRanges(diff);

        Assert.Equal(2, ranges.Count);
        Assert.Equal(new LineRange(1, 3), ranges[0]);
        Assert.Equal(new LineRange(31, 32), ranges[1]);
    }

    [Theory]
    [InlineData(10, 20, 15, 25, true)]
    [InlineData(10, 20, 20, 30, true)]
    [InlineData(10, 20, 21, 30, false)]
    [InlineData(10, 20, 12, 14, true)]
    public void Intersects_is_inclusive_at_both_ends(int aStart, int aEnd, int bStart, int bEnd, bool expected)
    {
        var a = new LineRange(aStart, aEnd);
        var b = new LineRange(bStart, bEnd);

        Assert.Equal(expected, a.Intersects(b));
        Assert.Equal(expected, b.Intersects(a));
    }
}
