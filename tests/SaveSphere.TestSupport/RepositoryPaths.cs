namespace SaveSphere.TestSupport;

public static class RepositoryPaths
{
    private static readonly Lazy<string> RootPath = new(FindRoot);

    /// <summary>Absolute path of the repository root (the folder containing SaveSphere.slnx).</summary>
    public static string Root => RootPath.Value;

    public static string Combine(params string[] relative) => Path.Combine([Root, .. relative]);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SaveSphere.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }
}
