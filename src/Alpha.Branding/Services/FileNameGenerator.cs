using System.IO;
using System.Text;

namespace Alpha.Branding.Services;

public static class FileNameGenerator
{
    public const string DefaultPrefix = "AlphaPremier_Photo";
    private const int MaxComponentLength = 120;
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string Generate(string? prefix, int zeroBasedIndex, int total, string extension = "jpg")
    {
        var safePrefix = SanitizeComponent(prefix, DefaultPrefix);
        var digits = total >= 100 ? 3 : 2;
        var number = (zeroBasedIndex + 1).ToString($"D{digits}");
        var safeExtension = SanitizeExtension(extension);
        var suffix = $"_{number}.{safeExtension}";
        if (safePrefix.Length + suffix.Length > MaxComponentLength)
            safePrefix = safePrefix[..Math.Max(1, MaxComponentLength - suffix.Length)].TrimEnd(' ', '.');
        return $"{safePrefix}{suffix}";
    }

    public static string FolderName(string? prefix) => SanitizeComponent(prefix, DefaultPrefix);

    public static string SanitizeComponent(string? value, string fallback = DefaultPrefix)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
        {
            if (char.IsControl(character) || character is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|')
                continue;
            builder.Append(character);
        }
        var clean = builder.ToString().Trim().TrimEnd(' ', '.').TrimStart('.');
        if (clean.Length == 0 || ReservedNames.Contains(Path.GetFileNameWithoutExtension(clean))) clean = fallback;
        if (clean.Length > MaxComponentLength) clean = clean[..MaxComponentLength].TrimEnd(' ', '.');
        return clean.Length == 0 ? fallback : clean;
    }

    private static string SanitizeExtension(string extension)
    {
        var clean = new string(extension.Trim().TrimStart('.').Where(c => char.IsLetterOrDigit(c)).ToArray());
        return string.IsNullOrEmpty(clean) ? "jpg" : clean.ToLowerInvariant();
    }
}
