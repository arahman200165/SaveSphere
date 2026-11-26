using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SaveSphere.SharedKernel;

namespace SaveSphere.Infrastructure.Persistence;

/// <summary>Stores <see cref="Id{TKind}"/> as a native PostgreSQL <c>uuid</c>.</summary>
public sealed class IdValueConverter<TKind>()
    : ValueConverter<Id<TKind>, Guid>(id => id.Value, guid => new Id<TKind>(guid));
