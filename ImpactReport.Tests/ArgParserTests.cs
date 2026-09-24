using ImpactReport.Utils;
using Xunit;

namespace ImpactReport.Tests;

public class ArgParserTests
{
    private static readonly HashSet<string> ValueOptions =
        new(StringComparer.OrdinalIgnoreCase) { "--sln", "--top", "--base" };

    private static readonly HashSet<string> Flags =
        new(StringComparer.OrdinalIgnoreCase) { "--changed", "--all" };

    [Fact]
    public void Reads_a_value_option()
    {
        Assert.Equal("a.sln", ArgParser.GetOptional(["--sln", "a.sln"], "--sln"));
    }

    [Fact]
    public void A_value_option_with_no_value_is_an_error()
    {
        Assert.Throws<CommandLineException>(() => ArgParser.GetOptional(["--sln"], "--sln"));
        Assert.Throws<CommandLineException>(() => ArgParser.GetOptional(["--sln", "--changed"], "--sln"));
    }

    [Fact]
    public void A_non_numeric_int_is_an_error_rather_than_the_default()
    {
        var ex = Assert.Throws<CommandLineException>(
            () => ArgParser.GetOptionalInt(["--top", "abc"], "--top", 25));

        Assert.Contains("--top", ex.Message);
    }

    [Fact]
    public void An_int_below_the_minimum_is_an_error()
    {
        Assert.Throws<CommandLineException>(() => ArgParser.GetOptionalInt(["--top", "0"], "--top", 25, min: 1));
    }

    [Fact]
    public void A_missing_int_option_uses_the_default()
    {
        Assert.Equal(25, ArgParser.GetOptionalInt([], "--top", 25));
    }

    [Fact]
    public void Negative_numbers_are_values_not_option_names()
    {
        Assert.Equal("-5", ArgParser.GetOptional(["--top", "-5"], "--top"));
    }

    [Fact]
    public void Unknown_options_are_rejected()
    {
        var ex = Assert.Throws<CommandLineException>(
            () => ArgParser.RejectUnknown(["--sln", "a.sln", "--nope"], ValueOptions, Flags));

        Assert.Contains("--nope", ex.Message);
    }

    [Fact]
    public void A_typo_suggests_the_intended_option()
    {
        var ex = Assert.Throws<CommandLineException>(
            () => ArgParser.RejectUnknown(["--sln", "a.sln", "--chnaged"], ValueOptions, Flags));

        Assert.Contains("--changed", ex.Message);
    }

    [Fact]
    public void Option_values_are_not_mistaken_for_options()
    {
        ArgParser.RejectUnknown(["--base", "origin/main", "--changed"], ValueOptions, Flags);
    }

    [Fact]
    public void Known_arguments_pass_validation()
    {
        ArgParser.RejectUnknown(["--sln", "a.sln", "--changed", "--all", "--top", "10"], ValueOptions, Flags);
    }
}
