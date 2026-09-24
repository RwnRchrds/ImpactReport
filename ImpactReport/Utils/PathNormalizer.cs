namespace ImpactReport.Utils;

public static class PathNormalizer
{
    public static StringComparer Comparer =>
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    public static StringComparison Comparison =>
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    public static string Full(string path) => ToForwardSlashes(Path.GetFullPath(path));

    public static string Combine(string root, string relative) =>
        Full(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

    public static string ToForwardSlashes(string path) =>
        path.Replace('\\', '/').TrimEnd('/');
}
