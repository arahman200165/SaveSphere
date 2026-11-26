namespace SaveSphere.SharedKernel;

/// <summary>Owner of a record. Single-owner system; the identity module will define the concrete owner later.</summary>
public sealed class Owner;

public sealed record RecordRegistration(
    RecordKind Kind,
    Id<Owner> OwnerId,
    RecordSourceKind Source,
    RecordPolicy Policy,
    ExternalRef? ExternalRef = null);

public sealed record RegistrationResult<TKind>(Id<TKind> Id, bool Created);

/// <summary>
/// The thin shared identity of ARC-01. Domain modules own their facts and call this interface instead of writing
/// <c>core</c> tables. Methods stage changes (and their outbox events) on the unit of work; the caller commits.
/// </summary>
public interface IRecordRegistry
{
    /// <summary>
    /// Registers <paramref name="id"/>. If the registration carries an <see cref="ExternalRef"/> that is already
    /// registered, nothing is created and the existing canonical ID is returned, so a re-import never duplicates a record.
    /// </summary>
    Task<RegistrationResult<TKind>> RegisterAsync<TKind>(Id<TKind> id, RecordRegistration registration, CancellationToken cancellationToken = default);

    /// <summary>Links two registered records. Idempotent; a link is directional and typed (e.g. <c>evidence</c>).</summary>
    Task LinkAsync<TFrom, TTo>(Id<TFrom> source, Id<TTo> target, string linkType, CancellationToken cancellationToken = default);

    Task SetLifecycleAsync<TKind>(Id<TKind> id, RecordLifecycle lifecycle, CancellationToken cancellationToken = default);

    Task<Id<TKind>?> FindByExternalRefAsync<TKind>(ExternalRef externalRef, CancellationToken cancellationToken = default);
}
