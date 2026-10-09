using DataFirst.Testing;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class GetSetProperties
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

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class ListOperationProperties
{
    [Property]
    public bool SetAt_inside_the_list_replaces_without_growing(DataList list, DataValue value)
    {
        if (list.IsEmpty) return true;
        var index = list.Count - 1;
        var set = _.SetAt(list, index, value);
        return set.Count == list.Count && set[index].Equals(value);
    }

    [Property]
    public bool SetAt_past_the_end_pads_with_nulls(DataList list, DataValue value, NonNegativeInt extra)
    {
        var index = list.Count + extra.Get;
        var set = _.SetAt(list, index, value);
        return set.Count == index + 1
               && set[index].Equals(value)
               && set.Take(list.Count).SequenceEqual(list)
               && set.Skip(list.Count).Take(extra.Get).All(v => v.Equals((DataValue)DataNull.Instance));
    }

    [Property]
    public bool InsertAt_inside_the_list_grows_by_one_and_shifts(DataList list, DataValue value)
    {
        var index = list.Count / 2;
        var inserted = _.InsertAt(list, index, value);
        return inserted.Count == list.Count + 1
               && inserted[index].Equals(value)
               && inserted.Skip(index + 1).SequenceEqual(list.Skip(index));
    }

    [Property]
    public bool InsertAt_past_the_end_pads_then_appends(DataList list, DataValue value, PositiveInt extra)
    {
        var index = list.Count + extra.Get;
        var inserted = _.InsertAt(list, index, value);
        return inserted.Count == index + 1 && inserted[index].Equals(value);
    }

    [Property]
    public bool Keys_of_a_list_are_its_indices(DataList list) =>
        _.Keys(list).SequenceEqual(Enumerable.Range(0, list.Count).Select(i => (StringOrInt)i));

    [Property]
    public bool Keys_of_a_map_are_its_keys_in_order(DataMap map) =>
        _.Keys(map).SequenceEqual(map.Keys.Select(k => (StringOrInt)k));

    [Property]
    public bool Map_over_a_list_preserves_length_and_order(DataList list) =>
        _.Map(list, v => v).SequenceEqual(list) && _.Map(list, v => "x").Count == list.Count;

    [Property]
    public bool Map_over_a_map_yields_its_values_in_order(DataMap map) =>
        _.Map(map, v => v).SequenceEqual(map.Values);

    [Property]
    public bool Filter_keeps_exactly_the_matches_in_order(DataList list)
    {
        var filtered = _.Filter(list, v => v is string);
        return filtered.SequenceEqual(list.Where(v => v is string)) && filtered.Count <= list.Count;
    }

    [Property]
    public bool Values_is_the_maps_values(DataMap map) => _.Values(map).SequenceEqual(map.Values);

    [Property]
    public bool IsEmpty_agrees_with_count_and_is_true_for_leaves(DataValue value) =>
        _.IsEmpty(value) == value switch
        {
            DataMap m => m.Count == 0,
            DataList l => l.Count == 0,
            _ => true
        };

    [Property]
    public bool IsObject_is_IsComposite(DataValue value) => _.IsObject(value) == value.IsComposite();

    [Property]
    public bool Union_has_no_duplicates_keeps_all_and_preserves_first_order(string[] first, string[] second)
    {
        var a = first.Select(s => (StringOrInt)s).ToList();
        var b = second.Select(s => (StringOrInt)s).ToList();
        var union = _.Union(a, b);

        return union.Distinct().Count() == union.Count
               && a.Concat(b).All(union.Contains)
               && union.Take(a.Distinct().Count()).SequenceEqual(a.Distinct());
    }

    [Property]
    public bool Reduce_visits_every_element_once_with_its_position(DataList list)
    {
        var seen = _.Reduce<List<(DataValue, StringOrInt)>>(
            list, (acc, v, k) => [.. acc, (v, k)], []);
        return seen.Count == list.Count
               && seen.Select(s => s.Item2).SequenceEqual(Enumerable.Range(0, list.Count).Select(i => (StringOrInt)i))
               && seen.Select(s => s.Item1).SequenceEqual(list);
    }

    [Property]
    public bool Reduce_over_a_map_passes_keys_in_order(DataMap map)
    {
        var keys = _.Reduce<List<string>>(map, (acc, _, k) => [.. acc, k switch { string name => name, _ => "" }], []);
        return keys.SequenceEqual(map.Keys);
    }

    [Fact]
    public void Collection_operations_reject_scalars()
    {
        new Action(() => _.Map("s", v => v)).Should().Throw<InvalidOperationException>().WithMessage("*string*");
        new Action(() => _.Keys(5)).Should().Throw<InvalidOperationException>().WithMessage("*no keys*");
        new Action(() => _.Reduce<int>(true, (a, _, _) => a, 0)).Should().Throw<InvalidOperationException>();
        new Action(() => _.GroupBy(5, "id")).Should().Throw<InvalidOperationException>();
    }
}

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class GroupingProperties
{
    private static readonly Gen<DataList> Rows =
        Gen.Choose(0, 8).SelectMany(n =>
            (from key in Gens.Key from word in Gens.Word from num in Gen.Choose(0, 100) select (key, word, num)).ArrayOf(n).Select(rows =>
                DataList.Create(rows.Select((r, i) => (DataValue)Map.Of(
                    ("id", r.Item1), ("name", r.Item2), ("n", r.Item3), ("seq", i))).ToList())));

    [Property]
    public Property GroupBy_partitions_the_rows() =>
        Prop.ForAll(Rows.ToArbitrary(), rows =>
        {
            var groups = _.GroupBy(rows, "id");
            var flattened = groups.Values.SelectMany(g => g.As<DataList>()).ToList();
            return flattened.Count == rows.Count
                   && groups.All(kv => kv.Value.As<DataList>().All(r => _.Get<string>(r, "id") == kv.Key))
                   && groups.Keys.SequenceEqual(rows.Select(r => _.Get<string>(r, "id")).Distinct());
        });

    [Property]
    public Property GroupBy_keeps_each_groups_original_order() =>
        Prop.ForAll(Rows.ToArbitrary(), rows =>
            _.GroupBy(rows, "id").Values.All(g =>
            {
                var seqs = g.As<DataList>().Select(r => _.Get<long>(r, "seq")).ToList();
                return seqs.SequenceEqual(seqs.OrderBy(s => s));
            }));

    [Property]
    public Property GroupBy_a_map_groups_its_values() =>
        Prop.ForAll(Rows.ToArbitrary(), rows =>
        {
            var keyed = _.KeyBy(rows, "name");
            return _.GroupBy(keyed, "id").Values.Sum(g => g.As<DataList>().Count) == keyed.Count;
        });

    [Property]
    public Property GroupBy_accepts_a_function() =>
        Prop.ForAll(Rows.ToArbitrary(), rows =>
        {
            var byParity = _.GroupBy(rows, r => (_.Get<long>(r, "n") % 2).ToString());
            return byParity.Keys.All(k => k is "0" or "1") && byParity.Values.Sum(g => g.As<DataList>().Count) == rows.Count;
        });

    [Property]
    public Property KeyBy_has_one_entry_per_distinct_key_and_the_last_row_wins() =>
        Prop.ForAll(Rows.ToArbitrary(), rows =>
        {
            var keyed = _.KeyBy(rows, "id");
            var expectedKeys = rows.Select(r => _.Get<string>(r, "id")).Distinct().ToList();

            return keyed.Keys.SequenceEqual(expectedKeys)
                   && keyed.All(kv =>
                   {
                       var last = rows.Last(r => _.Get<string>(r, "id") == kv.Key);
                       return kv.Value.Equals(last);
                   });
        });

    [Property]
    public Property Unwind_yields_one_map_per_element() =>
        Prop.ForAll((from m in Gens.Map from l in Gens.List select (m, l)).ToArbitrary(), t =>
        {
            var (map, list) = t;
            var withList = map.SetItem("items", list);
            var unwound = _.Unwind(withList, "items");

            return unwound.Count == list.Count
                   && unwound.Select((u, i) => u.As<DataMap>()["items"].Equals(list[i])).All(b => b)
                   && unwound.All(u => map.Keys.Where(k => k != "items").All(k => u.As<DataMap>()[k].Equals(map[k])));
        });

    [Fact]
    public void Unwind_of_an_empty_list_is_empty_and_of_a_non_list_throws()
    {
        _.Unwind(Map.Of(("a", 1), ("items", DataList.Empty)), "items").ShouldEqual(DataList.Empty);
        new Action(() => _.Unwind(Map.Of(("items", 5)), "items")).Should().Throw<InvalidOperationException>();
        new Action(() => _.Unwind(Map.Of(("a", 1)), "items")).Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void AggregateFields_collapses_rows_sharing_an_id()
    {
        var rows = List.Of(
            Map.Of(("isbn", "1"), ("title", "T"), ("author", "a")),
            Map.Of(("isbn", "1"), ("title", "T"), ("author", "b")),
            Map.Of(("isbn", "2"), ("title", "U"), ("author", "c")));

        _.AggregateFields(rows, "isbn", "author", "authors").ShouldEqual(List.Of(
            Map.Of(("isbn", "1"), ("title", "T"), ("authors", List.Of("a", "b"))),
            Map.Of(("isbn", "2"), ("title", "U"), ("authors", List.Of("c")))));
    }

    [Fact]
    public void KeyBy_and_GroupBy_of_nothing_are_empty()
    {
        _.KeyBy(DataList.Empty, "id").ShouldEqual(DataMap.Empty);
        _.GroupBy(DataList.Empty, "id").ShouldEqual(DataMap.Empty);
    }

    [Fact]
    public void KeyBy_needs_a_string_key_field()
    {
        new Action(() => _.KeyBy(List.Of(Map.Of(("id", 1))), "id")).Should().Throw<InvalidOperationException>();
        new Action(() => _.KeyBy(List.Of(Map.Of(("x", 1))), "id")).Should().Throw<KeyNotFoundException>();
    }
}

public sealed class LiteralTests
{
    [Fact]
    public void Map_Of_pairs_up_keys_and_values_in_order()
    {
        var map = Map.Of(("b", 1), ("a", 2));
        map.Keys.Should().Equal("b", "a");
        map["a"].Should().Be((DataValue)2L);
    }

    [Fact]
    public void Map_Of_with_a_repeated_key_keeps_the_last_value()
    {
        Map.Of(("a", 1), ("a", 2)).ShouldEqual(Map.Of(("a", 2)));
    }

    [Fact]
    public void Map_Of_nothing_is_the_empty_map()
    {
        Map.Of().ShouldEqual(DataMap.Empty);
        List.Of().ShouldEqual(DataList.Empty);
    }

    [Property]
    public bool List_Of_keeps_order(int a, int b, int c) =>
        List.Of(a, b, c).SequenceEqual([(DataValue)(long)a, (long)b, (long)c]);
}

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class DiffProperties
{
    private static readonly Gen<(DataMap Before, DataMap After)> ScalarEdit =
        from pair in Gens.MapWithExistingPath
        from value in Gens.Scalar
        select (pair.Map, _.Set(pair.Map, pair.Path, value));

    [Property]
    public bool A_value_does_not_differ_from_itself(DataValue value) =>
        _.Diff(value, value) is NoDiff;

    [Property]
    public bool Diffing_a_map_with_itself_is_empty(DataMap map) =>
        _.DiffObjects(map, map).IsEmpty;

    [Property]
    public bool Equal_but_separately_built_values_do_not_differ(DataMap map) =>
        _.DiffObjects(map, map.Aggregate(DataMap.Empty, (a, kv) => a.SetItem(kv.Key, kv.Value))).IsEmpty;

    [Property]
    public bool Differing_leaves_report_the_new_value(DataValue a, DataValue b)
    {
        if (a.IsComposite() && b.IsComposite()) return true;
        return a.Equals(b)
            ? _.Diff(a, b) is NoDiff
            : _.Diff(a, b) is Changed(var value) && value.Equals(b);
    }

    [Property]
    public Property Merging_a_diff_back_reproduces_the_edit() =>
        Prop.ForAll(ScalarEdit.ToArbitrary(), t =>
            _.Merge(t.Before, _.DiffObjects(t.Before, t.After)).Equals(t.After));

    [Property]
    public Property Merge_is_idempotent() =>
        Prop.ForAll(ScalarEdit.ToArbitrary(), t =>
        {
            var diff = _.DiffObjects(t.Before, t.After);
            var once = _.Merge(t.Before, diff);
            return _.Merge(once, diff).Equals(once);
        });

    [Property]
    public bool Merging_an_empty_diff_changes_nothing(DataMap map) =>
        _.Merge(map, DataMap.Empty).Equals(map);

    [Property]
    public Property An_edit_that_changes_nothing_has_an_empty_diff() =>
        Prop.ForAll(ScalarEdit.ToArbitrary(), t =>
            !t.Before.Equals(t.After) || _.DiffObjects(t.Before, t.After).IsEmpty);

    [Property]
    public Property Diff_paths_all_resolve_in_the_diff() =>
        Prop.ForAll(ScalarEdit.ToArbitrary(), t =>
        {
            var diff = _.DiffObjects(t.Before, t.After);
            return _.ChangedPaths(diff).All(p => _.ContainsKey(diff, p));
        });

    [Property]
    public bool Diffing_from_nothing_then_merging_creates_the_value(DataMap map)
    {
        var nonNull = map.Aggregate(DataMap.Empty, (a, kv) => kv.Value is DataNull ? a : a.SetItem(kv.Key, kv.Value));
        var diff = _.DiffObjects(DataNull.Instance, nonNull);
        var created = _.Merge((DataValue)DataNull.Instance, diff);
        return nonNull.IsEmpty ? created.Equals((DataValue)DataNull.Instance) : created.Equals((DataValue)nonNull);
    }

    [Fact]
    public void A_key_removed_diffs_to_null_and_an_added_key_to_its_value()
    {
        _.DiffObjects(Map.Of(("a", 1), ("b", 2)), Map.Of(("a", 1), ("c", 3)))
            .ShouldEqual(Map.Of(("b", DataNull.Instance), ("c", 3)));
    }

    [Fact]
    public void List_indices_become_string_keys_and_only_changed_slots_appear()
    {
        _.DiffObjects(List.Of("a", "b", "c"), List.Of("a", "X", "c"))
            .ShouldEqual(Map.Of(("1", "X")));
    }

    [Fact]
    public void A_list_that_grew_reports_only_the_new_slots()
    {
        _.DiffObjects(List.Of("a"), List.Of("a", "b", "c"))
            .ShouldEqual(Map.Of(("1", "b"), ("2", "c")));
    }

    [Fact]
    public void A_composite_replaced_by_a_leaf_is_a_change_to_that_leaf()
    {
        (_.Diff(Map.Of(("a", 1)), "now a string") is Changed(var toLeaf) && toLeaf.Equals((DataValue)"now a string"))
            .Should().BeTrue();
        (_.Diff("was a string", Map.Of(("a", 1))) is Changed(var toMap) && toMap.Equals((DataValue)Map.Of(("a", 1))))
            .Should().BeTrue();
    }

    [Fact]
    public void Merging_an_empty_diff_leaves_the_target_alone_rather_than_wiping_it()
    {
        // Regression: Merge walked InformationPaths, which reports the root of an empty
        // diff, and wrote the empty diff over the whole target.
        var target = Map.Of(("title", "Watchmen"), ("items", List.Of(1, 2)));
        _.Merge(target, DataMap.Empty).ShouldEqual(target);
        _.Merge((DataValue)target, DataMap.Empty).As<DataMap>().ShouldEqual(target);
        _.Merge((DataValue)DataNull.Instance, DataMap.Empty).Equals((DataValue)DataNull.Instance).Should().BeTrue();
    }

    [Fact]
    public void Merge_into_a_typed_map_returns_a_map()
    {
        _.Merge(Map.Of(("a", 1)), Map.Of(("a", 2))).ShouldEqual(Map.Of(("a", 2)));
    }
}

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class InformationPathProperties
{
    [Property]
    public bool Every_path_in_a_value_resolves_to_a_leaf_or_an_empty_composite(DataMap map) =>
        _.InformationPaths(map).All(p =>
        {
            var at = _.GetOrNull(map, p.ToList());
            return !at.IsComposite() || _.IsEmpty(at);
        });

    [Property]
    public bool A_leaf_has_just_the_root_path(DataValue value) =>
        value.IsComposite() && !_.IsEmpty(value)
        || _.InformationPaths(value).SequenceEqual([DataPath.Root]);

    [Property]
    public bool Paths_are_unique(DataMap map)
    {
        var paths = _.InformationPaths(map);
        return paths.Distinct().Count() == paths.Count;
    }

    [Property]
    public bool No_path_is_inside_another(DataMap map)
    {
        var paths = _.InformationPaths(map);
        return paths.All(p => paths.Count(q => q.Overlaps(p)) == 1) || map.IsEmpty;
    }

    [Property]
    public bool A_non_empty_map_has_a_path_per_leaf(DataMap map)
    {
        int Leaves(DataValue v) => v switch
        {
            DataMap m when !m.IsEmpty => m.Values.Sum(Leaves),
            DataList l when !l.IsEmpty => l.Sum(Leaves),
            _ => 1
        };
        return _.InformationPaths(map).Count == Leaves(map);
    }

    [Fact]
    public void An_empty_diff_touches_nothing_but_empty_data_still_has_a_root()
    {
        _.ChangedPaths(DataMap.Empty).Should().BeEmpty();
        _.InformationPaths(DataMap.Empty).Should().Equal(DataPath.Root);
    }

    [Fact]
    public void Paths_run_from_the_root_to_each_leaf_including_empty_composites()
    {
        var paths = _.InformationPaths(Map.Of(
            ("a", 1), ("b", Map.Of(("c", List.Of("x", DataMap.Empty)))), ("d", DataList.Empty)));

        paths.Should().Equal(
            DataPath.Of("a"),
            DataPath.Of("b", "c", 0),
            DataPath.Of("b", "c", 1),
            DataPath.Of("d"));
    }
}
