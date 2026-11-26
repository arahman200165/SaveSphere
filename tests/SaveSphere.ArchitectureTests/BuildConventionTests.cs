using System.Text.RegularExpressions;
using SaveSphere.TestSupport;

namespace SaveSphere.ArchitectureTests;

/// <summary>ADR-0002 build conventions and the public-repository rules from CLAUDE.md.</summary>
[Requirement("ARC-08")]
[Requirement("NFR-10")]
public sealed partial class BuildConventionTests
{
    [Fact]
    public void Package_versions_are_declared_only_in_Directory_Packages_props()
    {
        foreach (var project in ProjectGraph.All)
        {
            foreach (var package in project.PackageReferences)
            {
                Assert.True(
                    package.Attribute("Version") is null && package.Attribute("VersionOverride") is null,
                    $"{project.Name}: {package.Attribute("Include")?.Value} must not set a version; use Directory.Packages.props.");
            }
        }
    }

    [Fact]
    public void Every_referenced_package_has_a_central_version()
    {
        var central = File.ReadAllText(RepositoryPaths.Combine("Directory.Packages.props"));
        var declared = PackageVersion().Matches(central).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var project in ProjectGraph.All)
        {
            foreach (var package in project.PackageReferences)
            {
                var name = package.Attribute("Include")?.Value;
                Assert.Contains(name!, declared);
            }
        }
    }

    [Fact]
    public void Central_package_management_warnings_as_errors_and_nullable_are_on()
    {
        var props = File.ReadAllText(RepositoryPaths.Combine("Directory.Build.props"));
        var packages = File.ReadAllText(RepositoryPaths.Combine("Directory.Packages.props"));

        Assert.Contains("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", props, StringComparison.Ordinal);
        Assert.Contains("<Nullable>enable</Nullable>", props, StringComparison.Ordinal);
        Assert.Contains("<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>", packages, StringComparison.Ordinal);
    }

    [Fact]
    public void The_SDK_is_pinned_without_rolling_to_a_newer_feature_band()
    {
        var globalJson = File.ReadAllText(RepositoryPaths.Combine("global.json"));

        Assert.Matches("\"version\":\\s*\"10\\.0\\.202\"", globalJson);
        Assert.Contains("\"rollForward\": \"latestPatch\"", globalJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Solution_includes_every_project()
    {
        var solution = File.ReadAllText(RepositoryPaths.Combine("SaveSphere.slnx"));

        foreach (var project in ProjectGraph.All)
        {
            Assert.Contains(Path.GetFileName(project.Path), solution, StringComparison.Ordinal);
        }
    }

    [GeneratedRegex(@"<PackageVersion\s+Include=""([^""]+)""")]
    private static partial Regex PackageVersion();
}
