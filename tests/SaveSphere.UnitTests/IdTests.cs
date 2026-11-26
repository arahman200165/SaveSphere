using System.Reflection;
using System.Text.Json;
using SaveSphere.SharedKernel;
using SaveSphere.TestSupport;

namespace SaveSphere.UnitTests;

[Requirement("ARC-01")]
public sealed class IdTests
{
    // Marker types standing in for the real record kinds of ARC-01-AC01.
    private sealed class Transaction;

    private sealed class Meal;

    private sealed class Capture;

    [Fact]
    public void Ids_of_different_kinds_are_distinct_types_even_for_the_same_guid()
    {
        var guid = Guid.CreateVersion7();
        var transaction = Id.From<Transaction>(guid);
        var meal = Id.From<Meal>(guid);

        Assert.NotEqual(typeof(Id<Transaction>), typeof(Id<Meal>));
        Assert.NotEqual(typeof(Id<Meal>), typeof(Id<Capture>));
        Assert.False(((object)transaction).Equals(meal));
    }

    [Fact]
    public void Ids_of_different_kinds_have_no_implicit_or_explicit_conversion_between_them()
    {
        foreach (var type in new[] { typeof(Id<Transaction>), typeof(Id<Meal>) })
        {
            var conversions = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "op_Implicit" or "op_Explicit")
                .ToList();
            Assert.Empty(conversions);
        }
    }

    [Fact]
    public void New_creates_uuid_version_7_values()
    {
        var id = Id.New<Transaction>();

        // The version nibble is the high nibble of byte 7 in the big-endian RFC 9562 layout.
        var bytes = id.Value.ToByteArray(bigEndian: true);
        Assert.Equal(7, bytes[6] >> 4);
        Assert.False(id.IsEmpty);
    }

    [Fact]
    public void New_values_sort_in_creation_order()
    {
        var ids = new List<Id<Meal>>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(Id.New<Meal>());
            Thread.Sleep(2);
        }

        Assert.Equal(ids, ids.OrderBy(x => x.Value).ToList());
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Json_round_trips_as_a_plain_string()
    {
        var id = Id.New<Capture>();

        var json = JsonSerializer.Serialize(id);

        Assert.Equal($"\"{id}\"", json);
        Assert.Equal(id, JsonSerializer.Deserialize<Id<Capture>>(json));
    }

    [Theory]
    [InlineData("\"not-a-guid\"")]
    [InlineData("\"\"")]
    public void Json_rejects_invalid_ids(string json)
    {
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<Id<Capture>>(json));
    }

    [Fact]
    public void TryParse_accepts_canonical_text_and_rejects_everything_else()
    {
        var id = Id.New<Meal>();

        Assert.True(Id.TryParse<Meal>(id.ToString(), out var parsed));
        Assert.Equal(id, parsed);
        Assert.False(Id.TryParse<Meal>("nope", out _));
        Assert.False(Id.TryParse<Meal>(null, out _));
        Assert.False(Id.TryParse<Meal>(id.Value.ToString("N"), out _));
    }
}
