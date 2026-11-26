using System.Xml.Linq;
using SaveSphere.TestSupport;

namespace SaveSphere.ArchitectureTests;

/// <summary>Reads the real .csproj files, so unused-but-declared references are caught too.</summary>
internal sealed record ProjectInfo(
    string Name,
    string Path,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<XElement> PackageReferences);

internal static class ProjectGraph
{
    private static readonly Lazy<IReadOnlyList<ProjectInfo>> AllProjects = new(Load);

    public static IReadOnlyList<ProjectInfo> All => AllProjects.Value;

    public static ProjectInfo Get(string name) => All.Single(p => p.Name == name);

    public static IEnumerable<ProjectInfo> Modules => All.Where(p => p.Name.StartsWith("SaveSphere.Modules.", StringComparison.Ordinal));

    private static List<ProjectInfo> Load() =>
        Directory.EnumerateFiles(RepositoryPaths.Root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{System.IO.Path.DirectorySeparatorChar}node_modules{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path =>
            {
                var document = XDocument.Load(path);
                var references = document.Descendants("ProjectReference")
                    .Select(e => System.IO.Path.GetFileNameWithoutExtension((string?)e.Attribute("Include") ?? string.Empty))
                    .ToList();
                return new ProjectInfo(
                    System.IO.Path.GetFileNameWithoutExtension(path),
                    path,
                    references,
                    [.. document.Descendants("PackageReference")]);
            })
            .ToList();
}
