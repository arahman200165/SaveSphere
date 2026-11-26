using SaveSphere.SharedKernel;
using SaveSphere.TestSupport;

namespace SaveSphere.UnitTests;

[Requirement("ARC-01")]
public sealed class RecordKindAndExternalRefTests
{
    [Theory]
    [InlineData("finance.transaction")]
    [InlineData("lifestyle.meal")]
    [InlineData("knowledge.capture")]
    [InlineData("health.body-metric")]
    public void Record_kinds_are_module_dot_kind_in_lower_case_kebab(string value)
    {
        Assert.True(RecordKind.IsValid(value));
        Assert.Equal(value, RecordKind.Create(value).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("transaction")]
    [InlineData("Finance.Transaction")]
    [InlineData("finance.")]
    [InlineData(".transaction")]
    [InlineData("finance.transaction.extra")]
    [InlineData("finance transaction")]
    [InlineData("finance.trans_action")]
    [InlineData("1finance.transaction")]
    [InlineData("finance.transaction'; DROP TABLE core.record; --")]
    public void Malformed_record_kinds_are_rejected(string value)
    {
        Assert.False(RecordKind.IsValid(value));
        Assert.Throws<ArgumentException>(() => RecordKind.Create(value));
    }

    [Fact]
    public void An_uninitialized_record_kind_cannot_be_read()
    {
        Assert.Throws<InvalidOperationException>(() => default(RecordKind).Value);
    }

    [Fact]
    public void External_refs_keep_namespace_and_original_id()
    {
        var reference = new ExternalRef("import.bank-csv", "ROW-0042");

        Assert.Equal("import.bank-csv", reference.Namespace);
        Assert.Equal("ROW-0042", reference.ExternalId);
        Assert.Equal(new ExternalRef("import.bank-csv", "ROW-0042"), reference);
        Assert.NotEqual(new ExternalRef("import.other", "ROW-0042"), reference);
    }

    [Theory]
    [InlineData("", "x")]
    [InlineData("Import", "x")]
    [InlineData("import bank", "x")]
    [InlineData("import.bank", "")]
    public void Invalid_external_refs_are_rejected(string @namespace, string externalId)
    {
        Assert.Throws<ArgumentException>(() => new ExternalRef(@namespace, externalId));
    }

    [Fact]
    public void External_ids_are_length_limited()
    {
        Assert.Throws<ArgumentException>(() => new ExternalRef("import.bank", new string('a', ExternalRef.MaxExternalIdLength + 1)));
        _ = new ExternalRef("import.bank", new string('a', ExternalRef.MaxExternalIdLength));
    }

    [Fact]
    public void External_ids_never_appear_when_a_reference_is_printed()
    {
        var text = new ExternalRef("import.bank", "ACCT-123456789").ToString();

        Assert.DoesNotContain("ACCT-123456789", text, StringComparison.Ordinal);
    }
}
