using DataFirst.Testing;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class GetSetTests
{
    [Property]
    public Property Set_then_Get_returns_what_was_written()
    {
        var gen = from pair in Gens.MapWithExistingPath
                  from value in Gens.Value
                  select (pair.Map, pair.Path, value);

        return Prop.ForAll(gen.ToArbitrary(), t =>
            _.Get(_.Set(t.Map, t.Path, t.value), t.Path).Equals(t.value));
    }

    [Property]
    public bool Set_writes_through_a_path_that_does_not_exist_yet(DataValue value)
    {
        var path = DataPath.Of("x", "y", "z");
        var written = _.Set(DataMap.Empty, path, value);
        return _.Get(written, path).Equals(value) && _.ContainsKey(written, path);
    }

    [Property]
    public Property Set_is_idempotent()
    {
        var gen = from pair in Gens.MapWithExistingPath
                  from value in Gens.Value
                  select (pair.Map, pair.Path, value);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var once = _.Set(t.Map, t.Path, t.value);
            return _.Set(once, t.Path, t.value).Equals(once);
        });
    }

    [Property]
    public Property Set_leaves_the_original_untouched()
    {
        var gen = from pair in Gens.MapWithExistingPath
                  from value in Gens.Value
                  select (pair.Map, pair.Path, value);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var before = t.Map.ToString();
            var unused = _.Set(t.Map, t.Path, t.value);
            return t.Map.ToString() == before;
        });
    }

    [Property]
    public Property A_later_write_to_the_same_path_wins()
    {
        var gen = from pair in Gens.MapWithExistingPath
                  from first in Gens.Value
                  from second in Gens.Value
                  select (pair.Map, pair.Path, first, second);

        return Prop.ForAll(gen.ToArbitrary(), t =>
            _.Set(_.Set(t.Map, t.Path, t.first), t.Path, t.second).Equals(_.Set(t.Map, t.Path, t.second)));
    }

    [Property]
    public Property Writes_to_distinct_leaf_paths_commute()
    {
        var gen = from m in Gens.Map.Where(m => _.InformationPaths(m).Count(p => p.Count > 0) >= 2)
                  let paths = _.InformationPaths(m).Where(p => p.Count > 0).ToArray()
                  from p1 in Gen.Elements(paths)
                  from p2 in Gen.Elements(paths).Where(p => !p.Overlaps(p1))
                  from v1 in Gens.Scalar
                  from v2 in Gens.Scalar
                  select (m, p1, p2, v1, v2);

        return Prop.ForAll(gen.ToArbitrary(), t =>
            _.Set(_.Set(t.m, t.p1, t.v1), t.p2, t.v2).Equals(_.Set(_.Set(t.m, t.p2, t.v2), t.p1, t.v1)));
    }

    [Property]
    public bool Setting_the_empty_path_replaces_everything(DataValue obj, DataValue value) =>
        _.Set(obj, Array.Empty<StringOrInt>(), value).Equals(value);

    [Property]
    public bool Getting_the_empty_path_returns_the_value_itself(DataValue obj) =>
        _.Get(obj, Array.Empty<StringOrInt>()).Equals(obj)
        && _.GetOrNull(obj, Array.Empty<StringOrInt>()).Equals(obj);

    [Property]
    public bool Update_is_Get_then_Set(DataMap map)
    {
        if (map.IsEmpty) return true;
        var key = map.Keys.First();
        var replace = (DataValue)"updated";
        return _.Update(map, key, _ => replace).Equals(_.Set(map, key, replace))
               && _.Update(map, key, v => v).Equals(map);
    }

    [Property]
    public bool Update_passes_the_current_value_to_the_function(DataMap map)
    {
        if (map.IsEmpty) return true;
        var key = map.Keys.First();
        DataValue? seen = null;
        _.Update(map, key, v => { seen = v; return v; });
        return seen is { } s && s.Equals(map[key]);
    }

    [Property]
    public bool GetOrNull_agrees_with_ContainsKey_and_Get(DataMap map, DataPath path)
    {
        var steps = path.ToList();
        if (steps.Count == 0) return true;
        var found = _.GetOrNull(map, steps);
        return _.ContainsKey(map, steps) ? found.Equals(_.Get(map, steps)) : found.Equals((DataValue)DataNull.Instance);
    }

    [Property]
    public bool ContainsKey_is_true_for_every_information_path(DataMap map) =>
        _.InformationPaths(map).Where(p => p.Count > 0).All(p => _.ContainsKey(map, p));

    [Property]
    public bool ContainsKey_on_a_key_matches_the_maps_own_answer(DataMap map, string key) =>
        _.ContainsKey(map, key) == map.ContainsKey(key);

    [Property]
    public bool At_returns_one_entry_per_key_in_order(DataMap map, string[] keys)
    {
        var steps = keys.Select(k => (StringOrInt)k).ToArray();
        var result = _.At(map, steps);
        return result.Count == steps.Length
               && result.Zip(steps).All(pair => pair.First.Equals(_.GetOrNull(map, pair.Second)));
    }

    [Property]
    public bool At_paths_returns_one_entry_per_path(DataMap map, DataPath[] paths)
    {
        var result = _.At(map, paths);
        return result.Count == paths.Length
               && result.Zip(paths).All(pair => pair.First.Equals(_.GetOrNull(map, pair.Second)));
    }

    [Fact]
    public void Get_reads_lists_by_int_or_numeric_string()
    {
        var list = List.Of("a", "b");
        _.Get(list, 1).Should().Be((DataValue)"b");
        _.Get(list, "1").Should().Be((DataValue)"b");
    }

    [Fact]
    public void Get_fails_with_a_message_naming_the_mismatch()
    {
        var map = Map.Of(("a", 1));
        var list = List.Of(1);

        new Action(() => _.Get(map, 0)).Should().Throw<InvalidOperationException>().WithMessage("*Cannot index a map*");
        new Action(() => _.Get(list, "x")).Should().Throw<InvalidOperationException>().WithMessage("*list*");
        new Action(() => _.Get("scalar", "a")).Should().Throw<InvalidOperationException>().WithMessage("*string*");
        new Action(() => _.Get(map, "missing")).Should().Throw<KeyNotFoundException>();
        new Action(() => _.Get(list, 5)).Should().Throw<ArgumentOutOfRangeException>();
        new Action(() => _.Get(DataNull.Instance, "a")).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Typed_Get_names_the_actual_type_on_a_mismatch()
    {
        _.Get<long>(Map.Of(("year", 1987)), "year").Should().Be(1987);
        new Action(() => _.Get<string>(Map.Of(("year", 1987)), "year"))
            .Should().Throw<InvalidOperationException>().WithMessage("*number (long)*");
    }

    [Fact]
    public void ContainsKey_is_false_rather_than_throwing_for_odd_shapes()
    {
        var map = Map.Of(("a", List.Of(1, 2)), ("leaf", 5));

        _.ContainsKey(map, Array.Empty<StringOrInt>()).Should().BeFalse();
        _.ContainsKey(map, ["leaf", "deeper"]).Should().BeFalse();
        _.ContainsKey(map, ["a", 2]).Should().BeFalse();
        _.ContainsKey(map, ["a", -1]).Should().BeFalse();
        _.ContainsKey(map, ["a", 1]).Should().BeTrue();
        _.ContainsKey(map, ["a", "1"]).Should().BeTrue();
        _.ContainsKey(map, ["a", "x"]).Should().BeFalse();
        _.ContainsKey(map, 0).Should().BeFalse();
        _.ContainsKey("scalar", "a").Should().BeFalse();
        _.ContainsKey(DataNull.Instance, 0).Should().BeFalse();
    }

    [Fact]
    public void Set_rejects_a_key_of_the_wrong_kind_for_the_container()
    {
        new Action(() => _.Set(Map.Of(("a", 1)), 0, 1)).Should().Throw<InvalidOperationException>().WithMessage("*map*");
        new Action(() => _.Set(List.Of(1), "x", 1)).Should().Throw<InvalidOperationException>().WithMessage("*list*");
        new Action(() => _.Set(DataNull.Instance, "x", 1)).Should().Throw<InvalidOperationException>();
        new Action(() => _.Set(Map.Of(("leaf", 5)), ["leaf", "deeper"], 1)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Set_treats_a_null_on_the_way_down_as_missing()
    {
        _.Set(Map.Of(("a", DataNull.Instance)), ["a", "b"], 1)
            .ShouldEqual(Map.Of(("a", Map.Of(("b", 1)))));
    }

    [Fact]
    public void Set_creates_a_list_when_the_next_step_is_an_index()
    {
        _.Set(DataMap.Empty, ["items", 1, "id"], "x")
            .ShouldEqual(Map.Of(("items", List.Of(DataNull.Instance, Map.Of(("id", "x"))))));
    }

    [Fact]
    public void Update_of_a_missing_key_throws()
    {
        new Action(() => _.Update(Map.Of(("a", 1)), "zzz", v => v)).Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Update_works_along_a_path()
    {
        var map = Map.Of(("book", Map.Of(("year", 1987))));
        _.Update(map, ["book", "year"], y => y.As<long>() + 1)
            .ShouldEqual(Map.Of(("book", Map.Of(("year", 1988)))));
    }

    [Fact]
    public void At_with_no_keys_is_empty()
    {
        _.At(Map.Of(("a", 1))).ShouldEqual(DataList.Empty);
        _.At(Map.Of(("a", 1)), Array.Empty<DataPath>()).ShouldEqual(DataList.Empty);
    }

    [Fact]
    public void Getters_read_by_key_and_by_path_and_can_be_reused()
    {
        var title = Getter.Create<string>("title");
        var firstAuthor = Getter.Create<string>(["authors", 0, "name"]);
        var a = Map.Of(("title", "A"), ("authors", List.Of(Map.Of(("name", "x")))));
        var b = Map.Of(("title", "B"), ("authors", List.Of(Map.Of(("name", "y")))));

        title.Get(a).Should().Be("A");
        title.Get(b).Should().Be("B");
        firstAuthor.Get(a).Should().Be("x");
        firstAuthor.Get(b).Should().Be("y");
        new Action(() => Getter.Create<long>("title").Get(a)).Should().Throw<InvalidOperationException>();
    }
}
