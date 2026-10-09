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
public sealed class DataMapProperties
{
    [Property]
    public bool Equality_is_reflexive_and_hash_agrees(DataMap map)
    {
        var copy = map.Aggregate(DataMap.Empty, (acc, kv) => acc.SetItem(kv.Key, kv.Value));
        return map.Equals(copy) && copy.Equals(map) && map.GetHashCode() == copy.GetHashCode();
    }

    [Property]
    public bool Equality_ignores_insertion_order(DataMap map)
    {
        var reversed = map.Reverse().Aggregate(DataMap.Empty, (acc, kv) => acc.SetItem(kv.Key, kv.Value));
        return map.Equals(reversed) && map.GetHashCode() == reversed.GetHashCode();
    }

    [Property]
    public bool SetItem_then_read_returns_the_value(DataMap map, DataValue value) =>
        map.SetItem("k", value)["k"].Equals(value);

    [Property]
    public bool SetItem_does_not_modify_the_original(DataMap map, DataValue value)
    {
        var before = map.ToString();
        var unused = map.SetItem("a", value).SetItem("fresh", value);
        return map.ToString() == before;
    }

    [Property]
    public bool SetItem_on_a_new_key_appends_it(DataMap map, DataValue value) =>
        map.ContainsKey("fresh")
        || map.SetItem("fresh", value).Keys.SequenceEqual(map.Keys.Append("fresh"));

    [Property]
    public bool SetItem_on_an_existing_key_keeps_its_position(DataMap map, DataValue value) =>
        map.SetItem("a", value).Keys.SequenceEqual(map.ContainsKey("a") ? map.Keys : map.Keys.Append("a"));

    [Property]
    public bool Remove_drops_exactly_that_key(DataMap map)
    {
        var removed = map.Remove("a");
        return !removed.ContainsKey("a")
               && removed.Count == map.Count - (map.ContainsKey("a") ? 1 : 0)
               && removed.Keys.SequenceEqual(map.Keys.Where(k => k != "a"));
    }

    [Property]
    public bool Remove_of_a_missing_key_returns_the_same_instance(DataMap map) =>
        map.ContainsKey("zzz") || ReferenceEquals(map.Remove("zzz"), map);

    [Property]
    public bool Keys_and_Values_and_enumeration_agree(DataMap map) =>
        map.Select(kv => kv.Key).SequenceEqual(map.Keys)
        && map.Select(kv => kv.Value).SequenceEqual(map.Values)
        && map.Keys.Count() == map.Count
        && map.Keys.Distinct().Count() == map.Count;

    [Property]
    public bool GetOrNull_is_the_value_or_null(DataMap map) =>
        map.Keys.All(k => map.GetOrNull(k).Equals(map[k])) && map.GetOrNull("zzz").Equals((DataValue)DataNull.Instance);

    [Property]
    public bool Builder_last_write_wins_and_keeps_the_first_position(DataValue first, DataValue second)
    {
        var map = DataMap.CreateBuilder().Set("a", first).Set("b", "x").Set("a", second).ToDataMap();
        return map.Count == 2 && map["a"].Equals(second) && map.Keys.SequenceEqual(["a", "b"]);
    }

    [Property]
    public bool Differing_values_are_not_equal(DataMap map, DataValue a, DataValue b) =>
        a.Equals(b) || !map.SetItem("k", a).Equals(map.SetItem("k", b));

    [Fact]
    public void Missing_key_throws_and_names_the_keys_present()
    {
        var act = () => Map.Of(("a", 1), ("b", 2))["zzz"];
        act.Should().Throw<KeyNotFoundException>().WithMessage("*zzz*a*b*");
    }

    [Fact]
    public void Empty_map_is_empty()
    {
        DataMap.Empty.IsEmpty.Should().BeTrue();
        DataMap.Empty.Count.Should().Be(0);
        DataMap.Empty.ToString().Should().Be("{}");
    }

    [Fact]
    public void A_map_is_never_equal_to_null_or_another_type()
    {
        Map.Of(("a", 1)).Equals(null).Should().BeFalse();
        Map.Of(("a", 1)).Equals((object)"a").Should().BeFalse();
    }

    [Fact]
    public void Maps_of_different_sizes_or_keys_differ()
    {
        Map.Of(("a", 1)).Equals(Map.Of(("a", 1), ("b", 2))).Should().BeFalse();
        Map.Of(("a", 1)).Equals(Map.Of(("b", 1))).Should().BeFalse();
    }
}
