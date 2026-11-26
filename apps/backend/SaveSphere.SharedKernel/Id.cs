using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SaveSphere.SharedKernel;

/// <summary>
/// Stable typed identifier (ARC-01). <typeparamref name="TKind"/> is a compile-time marker, so
/// <c>Id&lt;Transaction&gt;</c> and <c>Id&lt;Meal&gt;</c> are distinct types that cannot be mixed up
/// even when they wrap the same <see cref="Guid"/>. New values are time-ordered UUIDv7.
/// </summary>
/// <typeparam name="TKind">Marker type naming the kind of record this ID identifies.</typeparam>
[JsonConverter(typeof(IdJsonConverterFactory))]
public readonly record struct Id<TKind>(Guid Value)
{
    public bool IsEmpty => Value == Guid.Empty;

    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

/// <summary>Factory and parsing helpers for <see cref="Id{TKind}"/>.</summary>
public static class Id
{
    /// <summary>Creates a new time-ordered (UUIDv7) identifier.</summary>
    public static Id<TKind> New<TKind>() => new(Guid.CreateVersion7());

    public static Id<TKind> From<TKind>(Guid value) => new(value);

    public static Id<TKind> Parse<TKind>(string value) =>
        new(Guid.ParseExact(value, "D"));

    public static bool TryParse<TKind>(string? value, out Id<TKind> id)
    {
        if (Guid.TryParseExact(value, "D", out var guid))
        {
            id = new Id<TKind>(guid);
            return true;
        }

        id = default;
        return false;
    }
}

internal sealed class IdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Id<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var kind = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(typeof(IdJsonConverter<>).MakeGenericType(kind))!;
    }
}

internal sealed class IdJsonConverter<TKind> : JsonConverter<Id<TKind>>
{
    public override Id<TKind> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        return Id.TryParse<TKind>(text, out var id)
            ? id
            : throw new JsonException("Value is not a valid identifier.");
    }

    public override void Write(Utf8JsonWriter writer, Id<TKind> value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
