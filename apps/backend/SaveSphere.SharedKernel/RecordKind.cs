using System.Text.RegularExpressions;

namespace SaveSphere.SharedKernel;

/// <summary>
/// The typed kind of a registered record (ARC-01), written <c>module.kind</c> in lower-case kebab,
/// for example <c>finance.transaction</c>. The owning module names its kinds; the registry stores them as text
/// so it never has to reference a domain module.
/// </summary>
public readonly partial record struct RecordKind
{
    /// <summary>The same pattern is enforced by a database check constraint on <c>core.record.kind</c>.</summary>
    public const string Pattern = @"^[a-z][a-z0-9]*(-[a-z0-9]+)*\.[a-z][a-z0-9]*(-[a-z0-9]+)*$";

    private readonly string? _value;

    private RecordKind(string value) => _value = value;

    public string Value => _value ?? throw new InvalidOperationException("RecordKind is not initialized.");

    public static RecordKind Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return IsValid(value)
            ? new RecordKind(value)
            : throw new ArgumentException("A record kind must look like 'module.kind' in lower-case kebab.", nameof(value));
    }

    public static bool IsValid(string? value) =>
        value is { Length: > 0 and <= 100 } && KindRegex().IsMatch(value);

    public override string ToString() => Value;

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex KindRegex();
}
