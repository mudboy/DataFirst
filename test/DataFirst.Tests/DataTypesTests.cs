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

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class DataListProperties
{
    [Property]
    public bool Equality_and_hash_follow_content(DataList list)
    {
        var copy = DataList.Create(list.ToList());
        return list.Equals(copy) && list.GetHashCode() == copy.GetHashCode();
    }

    [Property]
    public bool Order_matters(DataValue a, DataValue b) =>
        a.Equals(b) || !List.Of(a, b).Equals(List.Of(b, a));

    [Property]
    public bool Add_appends_and_leaves_the_original(DataList list, DataValue value)
    {
        var before = list.ToString();
        var added = list.Add(value);
        return added.Count == list.Count + 1 && added[list.Count].Equals(value) && list.ToString() == before;
    }

    [Property]
    public bool SetItem_replaces_one_element(DataList list, DataValue value)
    {
        if (list.IsEmpty) return true;
        var index = list.Count / 2;
        var set = list.SetItem(index, value);
        return set.Count == list.Count
               && set[index].Equals(value)
               && Enumerable.Range(0, list.Count).Where(i => i != index).All(i => set[i].Equals(list[i]));
    }

    [Property]
    public bool Insert_grows_by_one_and_shifts_the_tail(DataList list, DataValue value)
    {
        var index = list.Count / 2;
        var inserted = list.Insert(index, value);
        return inserted.Count == list.Count + 1
               && inserted[index].Equals(value)
               && inserted.Take(index).SequenceEqual(list.Take(index))
               && inserted.Skip(index + 1).SequenceEqual(list.Skip(index));
    }

    [Property]
    public bool PadTo_pads_with_null_and_never_shrinks(DataList list, NonNegativeInt length)
    {
        var padded = list.PadTo(length.Get);
        return padded.Count == Math.Max(list.Count, length.Get)
               && padded.Take(list.Count).SequenceEqual(list)
               && padded.Skip(list.Count).All(v => v.Equals((DataValue)DataNull.Instance));
    }

    [Property]
    public bool PadTo_a_shorter_length_returns_the_same_instance(DataList list) =>
        ReferenceEquals(list.PadTo(list.Count), list);

    [Fact]
    public void Indexing_outside_the_list_throws()
    {
        var list = List.Of(1, 2);
        var below = () => list[-1];
        var above = () => list[2];
        below.Should().Throw<ArgumentOutOfRangeException>();
        above.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*2*");
    }

    [Fact]
    public void Empty_list_is_empty()
    {
        DataList.Empty.IsEmpty.Should().BeTrue();
        DataList.Empty.ToString().Should().Be("[]");
        DataList.Empty.Equals(null).Should().BeFalse();
    }
}

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class DataPathProperties
{
    [Property]
    public bool Equality_and_hash_follow_the_steps(DataPath path) =>
        path.Equals(DataPath.Of(path.ToList())) && path.GetHashCode() == DataPath.Of(path.ToList()).GetHashCode();

    [Property]
    public bool Then_a_step_appends_it(DataPath path, StringOrInt step)
    {
        var longer = path.Then(step);
        return longer.Count == path.Count + 1 && longer.Take(path.Count).SequenceEqual(path) && longer[path.Count].Equals(step);
    }

    [Property]
    public bool Then_a_path_concatenates(DataPath first, DataPath second) =>
        first.Then(second).SequenceEqual(first.Concat(second));

    [Property]
    public bool Root_is_the_identity_for_Then(DataPath path) =>
        path.Then(DataPath.Root).Equals(path) && DataPath.Root.Then(path).Equals(path);

    [Property]
    public bool Overlaps_is_reflexive(DataPath path) => path.Overlaps(path);

    [Property]
    public bool Overlaps_is_symmetric(DataPath a, DataPath b) => a.Overlaps(b) == b.Overlaps(a);

    [Property]
    public bool A_path_overlaps_everything_beneath_it(DataPath path, DataPath suffix) =>
        path.Overlaps(path.Then(suffix)) && path.Then(suffix).Overlaps(path);

    [Property]
    public bool Root_overlaps_everything(DataPath path) => DataPath.Root.Overlaps(path);

    [Property]
    public bool Paths_that_diverge_do_not_overlap(DataPath prefix, DataPath tail)
    {
        var left = prefix.Then("left").Then(tail);
        var right = prefix.Then("right").Then(tail);
        return !left.Overlaps(right);
    }

    [Property]
    public bool Overlap_matches_the_prefix_definition(DataPath a, DataPath b)
    {
        var shared = Math.Min(a.Count, b.Count);
        return a.Overlaps(b) == a.Take(shared).SequenceEqual(b.Take(shared));
    }

    [Fact]
    public void Describes_itself_with_dots_and_bracketed_indices()
    {
        DataPath.Root.ToString().Should().Be("(root)");
        DataPath.Of("catalog", "books", 0, "title").ToString().Should().Be("catalog.books.[0].title");
    }

    [Fact]
    public void A_string_step_and_the_same_number_as_an_int_step_are_different()
    {
        DataPath.Of("0").Equals(DataPath.Of(0)).Should().BeFalse();
        DataPath.Of("0").Overlaps(DataPath.Of(0)).Should().BeFalse();
    }

    [Fact]
    public void Paths_work_as_set_members()
    {
        var set = new HashSet<DataPath> { DataPath.Of("a", 1), DataPath.Of("a", 1), DataPath.Of("a", 2) };
        set.Should().HaveCount(2);
    }

    [Fact]
    public void Equals_handles_null_and_foreign_types()
    {
        DataPath.Of("a").Equals(null).Should().BeFalse();
        DataPath.Of("a").Equals((object)"a").Should().BeFalse();
    }
}

public sealed class StringOrIntTests
{
    [Property]
    public bool An_int_step_is_its_own_index(int i)
    {
        StringOrInt step = i;
        return step.TryAsIndex(out var index) && index == i;
    }

    [Property]
    public bool A_numeric_string_step_parses_as_an_index(int i)
    {
        StringOrInt step = i.ToString();
        return step.TryAsIndex(out var index) && index == i;
    }

    [Theory]
    [InlineData("")]
    [InlineData("title")]
    [InlineData("1.5")]
    [InlineData("1e3")]
    [InlineData(" ")]
    [InlineData("99999999999")]
    public void A_non_numeric_string_is_not_an_index(string text)
    {
        StringOrInt step = text;
        step.TryAsIndex(out var index).Should().BeFalse();
        index.Should().Be(0);
    }

    [Fact]
    public void Describes_keys_and_indices()
    {
        ((StringOrInt)"title").Describe().Should().Be("key 'title'");
        ((StringOrInt)3).Describe().Should().Be("index 3");
    }
}

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class DataValueTests
{
    [Fact]
    public void Unwrap_gives_the_underlying_value_for_every_case()
    {
        ((DataValue)DataNull.Instance).Unwrap().Should().BeNull();
        ((DataValue)"s").Unwrap().Should().Be("s");
        ((DataValue)5L).Unwrap().Should().Be(5L);
        ((DataValue)1.5).Unwrap().Should().Be(1.5);
        ((DataValue)true).Unwrap().Should().Be(true);
        var map = Map.Of(("a", 1));
        ReferenceEquals(((DataValue)map).Unwrap(), map).Should().BeTrue();
        var list = List.Of(1);
        ReferenceEquals(((DataValue)list).Unwrap(), list).Should().BeTrue();
    }

    [Fact]
    public void An_int_literal_becomes_a_long()
    {
        ((DataValue)1987).Unwrap().Should().BeOfType<long>();
    }

    [Fact]
    public void As_returns_the_value_at_the_right_type_and_names_the_actual_case_otherwise()
    {
        ((DataValue)"s").As<string>().Should().Be("s");
        ((DataValue)7L).As<long>().Should().Be(7L);

        var wrong = () => ((DataValue)7L).As<string>();
        wrong.Should().Throw<InvalidOperationException>().WithMessage("*String*number (long)*");

        var map = () => ((DataValue)Map.Of(("a", 1))).As<DataList>();
        map.Should().Throw<InvalidOperationException>().WithMessage("*map[1]*");
    }

    [Fact]
    public void Describe_names_each_case()
    {
        ((DataValue)DataNull.Instance).Describe().Should().Be("null");
        ((DataValue)"s").Describe().Should().Be("string");
        ((DataValue)1L).Describe().Should().Be("number (long)");
        ((DataValue)1.5).Describe().Should().Be("number (double)");
        ((DataValue)true).Describe().Should().Be("bool");
        ((DataValue)Map.Of(("a", 1), ("b", 2))).Describe().Should().Be("map[2]");
        ((DataValue)List.Of(1, 2, 3)).Describe().Should().Be("list[3]");
    }

    [Property]
    public bool Only_maps_and_lists_are_composite(DataValue value) =>
        value.IsComposite() == (value.Unwrap() is DataMap or DataList);

    [Property]
    public bool Equality_is_structural_across_a_rebuild(DataValue value) =>
        value.Equals(Rebuild(value));

    [Property]
    public bool A_long_never_equals_a_double_or_string_of_the_same_number(NonNegativeInt n)
    {
        DataValue integer = (long)n.Get;
        return !integer.Equals((DataValue)(double)n.Get) && !integer.Equals((DataValue)n.Get.ToString());
    }

    [Fact]
    public void Null_equals_only_null()
    {
        ((DataValue)DataNull.Instance).Equals((DataValue)DataNull.Instance).Should().BeTrue();
        ((DataValue)DataNull.Instance).Equals((DataValue)"null").Should().BeFalse();
        DataNull.Instance.ToString().Should().Be("null");
    }

    /// Rebuilds a value from scratch so equality cannot be satisfied by reference.
    private static DataValue Rebuild(DataValue value) =>
        value switch
        {
            DataMap m => m.Aggregate(DataMap.Empty, (acc, kv) => acc.SetItem(kv.Key, Rebuild(kv.Value))),
            DataList l => DataList.Create(l.Select(Rebuild).ToList()),
            var other => other
        };
}

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
