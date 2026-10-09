using DataFirst.Testing;
using System.Text.Json;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class DataJsonTests
{
    [Property]
    public bool Output_is_valid_json_that_reads_back_to_the_same_value(DataValue value)
    {
        var json = DataJson.Serialize(value);
        var back = FromJson(JsonDocument.Parse(json).RootElement);
        return back.Equals(value) ? true : throw new Exception($"json {json} read back as {DataJson.Serialize(back)}");
    }

    [Property]
    public bool ToString_is_the_json(DataMap map, DataList list) =>
        map.ToString() == DataJson.Serialize(map) && list.ToString() == DataJson.Serialize(list);

    [Property]
    public bool Maps_serialise_keys_in_insertion_order(DataMap map) =>
        JsonDocument.Parse(DataJson.Serialize(map)).RootElement.EnumerateObject()
            .Select(p => p.Name).SequenceEqual(map.Keys);

    [Fact]
    public void Scalars_use_json_literals()
    {
        DataJson.Serialize(DataNull.Instance).Should().Be("null");
        DataJson.Serialize(42L).Should().Be("42");
        DataJson.Serialize(true).Should().Be("true");
        DataJson.Serialize(false).Should().Be("false");
        DataJson.Serialize(1.5).Should().Be("1.5");
        DataJson.Serialize("plain").Should().Be("\"plain\"");
    }

    [Fact]
    public void Composites_nest_without_whitespace()
    {
        DataJson.Serialize(Map.Of(("a", List.Of(1, Map.Of(("b", DataNull.Instance)))), ("c", DataMap.Empty)))
            .Should().Be("""{"a":[1,{"b":null}],"c":{}}""");
    }

    private static DataValue FromJson(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Null => DataNull.Instance,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.TryGetInt64(out var n) ? (DataValue)n : (DataValue)element.GetDouble(),
            JsonValueKind.Array => DataList.Create(element.EnumerateArray().Select(FromJson).ToList()),
            JsonValueKind.Object => element.EnumerateObject()
                .Aggregate(DataMap.CreateBuilder(), (b, p) => b.Set(p.Name, FromJson(p.Value))).ToDataMap(),
            _ => throw new InvalidOperationException()
        };
}
