using System.Text.RegularExpressions;
using Serilog.Events;
using SaveSphere.SharedKernel;

namespace SaveSphere.Infrastructure.Logging;

/// <summary>
/// Central redaction rules for everything that is logged (SEC-01, NFR-10). Pure and side-effect free
/// so it can be unit-tested exhaustively. Redaction is deny-list based and defensive: when in doubt, mask.
/// </summary>
public static partial class LogRedactor
{
    public const string Mask = Redacted<string>.Mask;

    private static readonly string[] SensitiveNameFragments =
    [
        "password", "passwd", "passphrase", "pwd", "secret", "token", "authorization", "apikey",
        "connectionstring", "credential", "cookie", "privatekey", "passkey", "recoverycode", "setcookie",
    ];

    public static bool IsSensitiveName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var normalized = name.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal);

        foreach (var fragment in SensitiveNameFragments)
        {
            if (normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Masks connection strings, key=value secrets, bearer/basic credentials and JWT-like tokens in free text.</summary>
    public static string ScrubText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = ConnectionStringRegex().Replace(text, Mask);
        result = SecretPairRegex().Replace(result, "$1=" + Mask);
        result = AuthSchemeRegex().Replace(result, "$1 " + Mask);
        result = JwtRegex().Replace(result, Mask);
        return result;
    }

    public static LogEventPropertyValue Scrub(LogEventPropertyValue value) => value switch
    {
        ScalarValue { Value: string s } => new ScalarValue(ScrubText(s)),
        ScalarValue { Value: IRedacted } => new ScalarValue(Mask),
        ScalarValue scalar => scalar,
        StructureValue structure => new StructureValue(
            structure.Properties.Select(p => new LogEventProperty(
                p.Name,
                IsSensitiveName(p.Name) ? new ScalarValue(Mask) : Scrub(p.Value))),
            structure.TypeTag),
        SequenceValue sequence => new SequenceValue(sequence.Elements.Select(Scrub)),
        DictionaryValue dictionary => new DictionaryValue(
            dictionary.Elements.Select(e => new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                e.Key,
                e.Key.Value is string key && IsSensitiveName(key) ? new ScalarValue(Mask) : Scrub(e.Value)))),
        _ => new ScalarValue(Mask),
    };

    [GeneratedRegex(
        @"(?i)\b(?:host|server|data\s+source|user\s+id|username|database)\s*=\s*[^;\r\n]*(?:;\s*[\w ]+=[^;\r\n]*)*",
        RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionStringRegex();

    [GeneratedRegex(
        @"(?i)\b(password|passwd|pwd|passphrase|secret|apikey|api_key|accesskey|token)\s*=\s*[^;&\s""']*",
        RegexOptions.CultureInvariant)]
    private static partial Regex SecretPairRegex();

    [GeneratedRegex(@"(?i)\b(bearer|basic)\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.CultureInvariant)]
    private static partial Regex AuthSchemeRegex();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*", RegexOptions.CultureInvariant)]
    private static partial Regex JwtRegex();
}
