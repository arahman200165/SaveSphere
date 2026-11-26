using System.Text.RegularExpressions;

namespace SaveSphere.SharedKernel;

/// <summary>
/// An identifier assigned by an outside system, kept only as a namespaced reference to the canonical record
/// (ARC-01). SaveSphere never adopts it as its own ID.
/// </summary>
public sealed partial record ExternalRef
{
    public const string NamespacePattern = "^[a-z][a-z0-9]*([.-][a-z0-9]+)*$";

    public const int MaxExternalIdLength = 500;

    public ExternalRef(string @namespace, string externalId)
    {
        ArgumentNullException.ThrowIfNull(@namespace);
        ArgumentNullException.ThrowIfNull(externalId);
        if (@namespace.Length > 100 || !NamespaceRegex().IsMatch(@namespace))
        {
            throw new ArgumentException("A namespace is lower-case letters, digits, '.' and '-', e.g. 'import.bank-csv'.", nameof(@namespace));
        }

        if (externalId.Length is 0 or > MaxExternalIdLength)
        {
            throw new ArgumentException("An external ID must be 1 to 500 characters.", nameof(externalId));
        }

        Namespace = @namespace;
        ExternalId = externalId;
    }

    public string Namespace { get; }

    public string ExternalId { get; }

    // External IDs can be sensitive (account numbers, message IDs): never print them.
    public override string ToString() => $"{Namespace}:<redacted>";

    [GeneratedRegex(NamespacePattern, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex NamespaceRegex();
}
