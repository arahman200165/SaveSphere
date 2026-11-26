using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SaveSphere.Modules.Core.Persistence;
using SaveSphere.SharedKernel;

namespace SaveSphere.Modules.Core.Records;

public sealed partial class RecordRegistry(CoreDbContext context, IOutbox outbox, TimeProvider clock) : IRecordRegistry
{
    public const string LinkTypePattern = "^[a-z][a-z0-9]*(-[a-z0-9]+)*$";

    public async Task<RegistrationResult<TKind>> RegisterAsync<TKind>(
        Id<TKind> id,
        RecordRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(registration.Policy);
        if (id.IsEmpty || registration.OwnerId.IsEmpty)
        {
            throw new ArgumentException("Record and owner IDs must not be empty.", nameof(id));
        }

        var kind = registration.Kind.Value;

        if (registration.ExternalRef is { } externalRef)
        {
            var existingId = await context.RecordExternalRefs
                .Where(x => x.Namespace == externalRef.Namespace && x.ExternalId == externalRef.ExternalId)
                .Select(x => (Guid?)x.RecordId)
                .SingleOrDefaultAsync(cancellationToken);
            if (existingId is { } existing)
            {
                var existingKind = await context.Records
                    .Where(x => x.Id == existing)
                    .Select(x => x.Kind)
                    .SingleAsync(cancellationToken);
                return existingKind == kind
                    ? new RegistrationResult<TKind>(Id.From<TKind>(existing), Created: false)
                    : throw new InvalidOperationException("The external reference is already registered for a different record kind.");
            }
        }

        var now = clock.GetUtcNow();
        context.Records.Add(new RegisteredRecord
        {
            Id = id.Value,
            Kind = kind,
            OwnerId = registration.OwnerId.Value,
            Source = registration.Source,
            Lifecycle = RecordLifecycle.Active,
            Sensitivity = registration.Policy.Sensitivity,
            SearchVisibility = registration.Policy.SearchVisibility,
            CreatedAt = now,
            UpdatedAt = now,
        });
        if (registration.ExternalRef is { } reference)
        {
            context.RecordExternalRefs.Add(new RecordExternalRef
            {
                Namespace = reference.Namespace,
                ExternalId = reference.ExternalId,
                RecordId = id.Value,
                CreatedAt = now,
            });
        }

        outbox.Enqueue(new RecordRegistered(id.Value, kind));
        return new RegistrationResult<TKind>(id, Created: true);
    }

    public async Task LinkAsync<TFrom, TTo>(Id<TFrom> source, Id<TTo> target, string linkType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(linkType);
        if (linkType.Length > 100 || !LinkTypeRegex().IsMatch(linkType))
        {
            throw new ArgumentException("A link type is lower-case kebab, e.g. 'evidence'.", nameof(linkType));
        }

        if (source.Value == target.Value)
        {
            throw new ArgumentException("A record cannot be linked to itself.", nameof(target));
        }

        var already = await context.RecordLinks.AnyAsync(
            x => x.FromRecordId == source.Value && x.ToRecordId == target.Value && x.LinkType == linkType,
            cancellationToken);
        if (already)
        {
            return;
        }

        context.RecordLinks.Add(new RecordLink
        {
            FromRecordId = source.Value,
            ToRecordId = target.Value,
            LinkType = linkType,
            CreatedAt = clock.GetUtcNow(),
        });
        outbox.Enqueue(new RecordsLinked(source.Value, target.Value, linkType));
    }

    public async Task SetLifecycleAsync<TKind>(Id<TKind> id, RecordLifecycle lifecycle, CancellationToken cancellationToken = default)
    {
        var record = await context.Records.SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
            ?? throw new InvalidOperationException("The record is not registered.");
        if (record.Lifecycle == lifecycle)
        {
            return;
        }

        if (record.Lifecycle == RecordLifecycle.Deleted)
        {
            throw new InvalidOperationException("A deleted record is a tombstone and cannot be restored.");
        }

        var from = record.Lifecycle;
        record.Lifecycle = lifecycle;
        record.UpdatedAt = clock.GetUtcNow();
        outbox.Enqueue(new RecordLifecycleChanged(record.Id, record.Kind, from.ToString(), lifecycle.ToString()));
    }

    public async Task<Id<TKind>?> FindByExternalRefAsync<TKind>(ExternalRef externalRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(externalRef);
        var found = await context.RecordExternalRefs
            .Where(x => x.Namespace == externalRef.Namespace && x.ExternalId == externalRef.ExternalId)
            .Select(x => (Guid?)x.RecordId)
            .SingleOrDefaultAsync(cancellationToken);
        return found is { } guid ? Id.From<TKind>(guid) : null;
    }

    [GeneratedRegex(LinkTypePattern, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex LinkTypeRegex();
}
