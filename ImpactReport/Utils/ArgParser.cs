namespace ImpactReport.Utils;

public sealed class CommandLineException(string message) : Exception(message);

public static class ArgParser
{
    public static string GetRequired(string[] args, string name)
    {
        var value = GetOptional(args, name);
        if (string.IsNullOrWhiteSpace(value))
            throw new CommandLineException($"Missing required argument: {name} <value>");
        return value;
    }

    public static string? GetOptional(string[] args, string name)
    {
        var idx = IndexOf(args, name);
        if (idx < 0) return null;

        if (idx == args.Length - 1)
            throw new CommandLineException($"{name} requires a value.");

        var next = args[idx + 1];
        if (IsOptionName(next))
            throw new CommandLineException($"{name} requires a value (found '{next}').");

        return next;
    }

    public static int GetOptionalInt(string[] args, string name, int defaultValue, int min = int.MinValue)
    {
        var raw = GetOptional(args, name);
        if (raw is null) return defaultValue;

        if (!int.TryParse(raw, out var value))
            throw new CommandLineException($"{name} expects a whole number (got '{raw}').");

        if (value < min)
            throw new CommandLineException($"{name} must be {min} or greater (got {value}).");

        return value;
    }

    public static bool HasFlag(string[] args, params string[] names) =>
        args.Any(a => names.Any(n => a.Equals(n, StringComparison.OrdinalIgnoreCase)));

    public static void RejectUnknown(string[] args, IReadOnlySet<string> valueOptions, IReadOnlySet<string> flags)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!IsOptionName(arg))
                continue;

            if (valueOptions.Contains(arg))
            {
                i++;
                continue;
            }

            if (flags.Contains(arg))
                continue;

            var suggestion = Suggest(arg, valueOptions.Concat(flags));
            throw new CommandLineException(
                $"Unknown option: {arg}{suggestion}\nRun 'impactreport --help' to see the available options.");
        }
    }

    private static int IndexOf(string[] args, string name) =>
        Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static bool IsOptionName(string arg)
    {
        if (arg.StartsWith("--", StringComparison.Ordinal)) return true;
        return arg.Length > 1 && arg[0] == '-' && !char.IsDigit(arg[1]) && arg[1] != '.';
    }

    private static string Suggest(string arg, IEnumerable<string> known)
    {
        var best = known
            .Select(k => (Option: k, Distance: Distance(arg, k)))
            .Where(x => x.Distance <= 2)
            .OrderBy(x => x.Distance)
            .Select(x => x.Option)
            .FirstOrDefault();

        return best is null ? string.Empty : $"  (did you mean {best}?)";
    }

    private static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
