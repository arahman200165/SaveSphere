using Xunit.v3;

namespace SaveSphere.TestSupport;

/// <summary>
/// Links a test to the PRD requirement(s) it verifies (QA-01 traceability). Reported as the
/// <c>Requirement</c> trait, so <c>dotnet test --filter-trait "Requirement=ARC-02"</c> runs them.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirementAttribute(string id) : Attribute, ITraitAttribute
{
    public string Id { get; } = id;

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new KeyValuePair<string, string>("Requirement", Id)];
}
