using System.Text.RegularExpressions;
using SaveSphere.SharedKernel;
using SaveSphere.TestSupport;

namespace SaveSphere.UnitTests;

[Requirement("ARC-12")]
public sealed partial class PlatformSchemasTests
{
    [Fact]
    public void Defines_the_thirteen_architecture_schemas()
    {
        string[] expected =
        [
            "identity", "core", "productivity", "finance", "health", "knowledge", "inventory",
            "lifestyle", "analytics", "automation", "integrations", "sync", "audit",
        ];

        Assert.Equal(expected.Order(), PlatformSchemas.All.Order());
    }

    [Fact]
    public void Names_are_unique_lowercase_snake_case_identifiers()
    {
        Assert.Equal(PlatformSchemas.All.Count, PlatformSchemas.All.Distinct().Count());
        Assert.All(PlatformSchemas.All, name => Assert.Matches(SnakeCase(), name));
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex SnakeCase();
}
