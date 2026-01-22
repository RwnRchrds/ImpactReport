namespace ImpactReport.Utils;

public static class ArgParser
{
    public static string GetRequired(string[] args, string name)
    {
        var value = GetOptional(args, name);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required argument: {name} <value>");
        return value;
    }

    public static string? GetOptional(string[] args, string name)
    {
        var idx = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return null;
        if (idx == args.Length - 1) return null;

        var next = args[idx + 1];
        if (next.StartsWith("--", StringComparison.Ordinal)) return null;

        return next;
    }

    public static int GetOptionalInt(string[] args, string name, int defaultValue)
    {
        var raw = GetOptional(args, name);
        return int.TryParse(raw, out var v) ? v : defaultValue;
    }

    public static bool HasFlag(string[] args, params string[] names)
    {
        return args.Any(a => names.Any(n => a.Equals(n, StringComparison.OrdinalIgnoreCase)));
    }
}