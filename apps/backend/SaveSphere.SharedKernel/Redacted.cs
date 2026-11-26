namespace SaveSphere.SharedKernel;

/// <summary>Marker for values that must never reach logs, previews or telemetry (SEC-01, NFR-10).</summary>
public interface IRedacted;

/// <summary>Marks a property or field whose value must be masked when logged.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SensitiveAttribute : Attribute;

/// <summary>
/// Wraps a sensitive value. <see cref="ToString"/> never reveals it; callers must opt in with <see cref="Reveal"/>.
/// </summary>
public readonly record struct Redacted<T>(T Value) : IRedacted
{
    public const string Mask = "[REDACTED]";

    public T Reveal() => Value;

    public override string ToString() => Mask;
}
