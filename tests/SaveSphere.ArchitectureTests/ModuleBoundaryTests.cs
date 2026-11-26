using SaveSphere.TestSupport;

namespace SaveSphere.ArchitectureTests;

/// <summary>
/// Modular-monolith rules from CLAUDE.md "Fixed architecture" and ADR-0001: modules own their schema and
/// never reach into each other; cross-module interaction goes through SharedKernel contracts and the outbox.
/// </summary>
[Requirement("ARC-12")]
[Requirement("ARC-01")]
public sealed class ModuleBoundaryTests
{
    [Fact]
    public void At_least_one_module_exists_so_the_rules_below_are_not_vacuous()
    {
        Assert.NotEmpty(ProjectGraph.Modules);
    }

    [Fact]
    public void Modules_do_not_reference_other_modules()
    {
        foreach (var module in ProjectGraph.Modules)
        {
            var illegal = module.ProjectReferences
                .Where(r => r.StartsWith("SaveSphere.Modules.", StringComparison.Ordinal) && r != module.Name)
                .ToList();
            Assert.True(illegal.Count == 0, $"{module.Name} references other module(s): {string.Join(", ", illegal)}");
        }
    }

    [Fact]
    public void Modules_do_not_reference_the_host_projects()
    {
        string[] hosts = ["SaveSphere.Api", "SaveSphere.Migrator", "SaveSphere.Worker"];

        foreach (var module in ProjectGraph.Modules)
        {
            var illegal = module.ProjectReferences.Intersect(hosts).ToList();
            Assert.True(illegal.Count == 0, $"{module.Name} references host project(s): {string.Join(", ", illegal)}");
        }
    }

    [Fact]
    public void SharedKernel_depends_on_no_other_SaveSphere_project_and_no_packages()
    {
        var kernel = ProjectGraph.Get("SaveSphere.SharedKernel");

        Assert.Empty(kernel.ProjectReferences);
        Assert.Empty(kernel.PackageReferences);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_modules_or_hosts()
    {
        var infrastructure = ProjectGraph.Get("SaveSphere.Infrastructure");

        Assert.DoesNotContain(infrastructure.ProjectReferences, r => r.StartsWith("SaveSphere.Modules.", StringComparison.Ordinal));
        Assert.DoesNotContain(infrastructure.ProjectReferences, r => r is "SaveSphere.Api" or "SaveSphere.Migrator" or "SaveSphere.Worker");
    }

    [Fact]
    public void Production_projects_never_reference_test_projects()
    {
        var production = ProjectGraph.All.Where(p => p.Path.Contains($"{Path.DirectorySeparatorChar}apps{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (var project in production)
        {
            Assert.DoesNotContain(project.ProjectReferences, r => r.Contains("Tests", StringComparison.Ordinal) || r.Contains("TestSupport", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Each_module_owns_a_distinct_schema_name_matching_its_project()
    {
        // Core owns the "core" schema; later modules follow SaveSphere.Modules.<Name> -> <name> schema.
        foreach (var module in ProjectGraph.Modules)
        {
            var name = module.Name["SaveSphere.Modules.".Length..].ToLowerInvariant();
            var schemas = SaveSphere.SharedKernel.PlatformSchemas.All;
            Assert.Contains(name, schemas);
        }
    }
}
