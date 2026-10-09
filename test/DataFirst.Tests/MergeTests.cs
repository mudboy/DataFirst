using DataFirst.Lodash;
using DataFirst.Testing;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class MergeTests
{
    [Property]
    public bool Merging_nothing_in_either_direction_changes_nothing(DataMap map) =>
        _.Merge(map, DataMap.Empty).Equals(map) && _.Merge(DataMap.Empty, map).Equals(map);

    [Property]
    public bool Merging_a_value_with_itself_changes_nothing(DataValue value) =>
        _.Merge(value, value).Equals(value);

    [Property]
    public bool A_scalar_or_a_mismatched_kind_is_replaced_by_the_second(DataValue first, DataValue second)
    {
        var sameKind = (first is DataMap && second is DataMap) || (first is DataList && second is DataList);
        return sameKind || _.Merge(first, second).Equals(second);
    }

    [Property]
    public bool Every_non_composite_leaf_of_the_second_wins(DataMap first, DataMap second)
    {
        var merged = _.Merge(first, second);
        return _.InformationPaths(second)
            .Where(path => path.Count > 0)
            .Select(path => (path, leaf: _.Get(second, path)))
            .Where(t => !t.leaf.IsComposite())
            .All(t => _.Get(merged, t.path).Equals(t.leaf));
    }

    [Property]
    public bool Every_key_of_either_map_survives_and_new_keys_follow_the_first_maps(DataMap first, DataMap second)
    {
        var merged = _.Merge(first, second);
        return merged.Keys.SequenceEqual(first.Keys.Concat(second.Keys.Where(k => !first.ContainsKey(k))));
    }

    [Property]
    public bool A_key_in_only_one_map_is_carried_over_untouched(DataMap first, DataMap second)
    {
        var merged = _.Merge(first, second);
        return first.Where(kv => !second.ContainsKey(kv.Key)).All(kv => merged[kv.Key].Equals(kv.Value))
               && second.Where(kv => !first.ContainsKey(kv.Key)).All(kv => merged[kv.Key].Equals(kv.Value));
    }

    [Property]
    public bool Maps_with_no_keys_in_common_merge_the_same_either_way(DataMap first, DataMap second)
    {
        var disjoint = second.Aggregate(DataMap.Empty, (m, kv) => first.ContainsKey(kv.Key) ? m : m.SetItem(kv.Key, kv.Value));
        return _.Merge(first, disjoint).Equals(_.Merge(disjoint, first));
    }

    [Property]
    public bool Lists_merge_by_index_and_keep_the_longer_tail(DataList first, DataList second)
    {
        var merged = _.Merge(first, second).As<DataList>();
        return merged.Count == Math.Max(first.Count, second.Count)
               && Enumerable.Range(0, merged.Count).All(i =>
                   merged[i].Equals(
                       i < first.Count && i < second.Count ? _.Merge(first[i], second[i])
                       : i < first.Count ? first[i]
                       : second[i]));
    }

    [Property]
    public bool Merging_leaves_both_inputs_alone(DataMap first, DataMap second)
    {
        var (before1, before2) = (first.ToString(), second.ToString());
        _.Merge(first, second);
        return first.ToString() == before1 && second.ToString() == before2;
    }

    [Property]
    public bool The_typed_and_untyped_overloads_agree(DataMap first, DataMap second) =>
        _.Merge(first, second).Equals(_.Merge((DataValue)first, second).As<DataMap>());

    [Property]
    public bool Merging_the_second_in_twice_is_the_same_as_once(DataMap first, DataMap second)
    {
        var once = _.Merge(first, second);
        return _.Merge(once, second).Equals(once);
    }

    [Property]
    public bool A_second_that_is_a_scalar_overwrite_at_one_path_is_exactly_a_Set(DataValue leaf)
    {
        var first = Map.Of(("book", Map.Of(("title", "Watchmen"), ("year", 1987))), ("other", 1));
        var second = Map.Of(("book", Map.Of(("year", leaf))));

        return _.Merge(first, second).Equals(_.Set(first, ["book", "year"], leaf));
    }

    [Fact]
    public void The_lodash_example_merges_list_elements_by_position()
    {
        var first = Map.Of(("a", List.Of(Map.Of(("b", 2)), Map.Of(("d", 4)))));
        var second = Map.Of(("a", List.Of(Map.Of(("c", 3)), Map.Of(("e", 5)))));

        _.Merge(first, second).ShouldEqual(
            Map.Of(("a", List.Of(Map.Of(("b", 2), ("c", 3)), Map.Of(("d", 4), ("e", 5))))));
    }

    [Fact]
    public void Nested_maps_merge_at_every_depth_and_the_second_wins_a_clash()
    {
        var first = Map.Of(("book", Map.Of(("title", "Watchmen"), ("meta", Map.Of(("a", 1), ("b", 2))))));
        var second = Map.Of(("book", Map.Of(("title", "Maus"), ("meta", Map.Of(("b", 20), ("c", 30))))));

        _.Merge(first, second).ShouldEqual(
            Map.Of(("book", Map.Of(("title", "Maus"), ("meta", Map.Of(("a", 1), ("b", 20), ("c", 30)))))));
    }

    [Fact]
    public void Null_is_a_value_and_overwrites()
    {
        _.Merge(Map.Of(("a", 1), ("b", Map.Of(("c", 2)))), Map.Of(("a", DataNull.Instance), ("b", DataNull.Instance)))
            .ShouldEqual(Map.Of(("a", DataNull.Instance), ("b", DataNull.Instance)));
    }

    [Fact]
    public void A_map_and_a_list_never_merge_into_each_other()
    {
        _.Merge(Map.Of(("a", Map.Of(("x", 1)))), Map.Of(("a", List.Of(1)))).ShouldEqual(Map.Of(("a", List.Of(1))));
        _.Merge(Map.Of(("a", List.Of(1))), Map.Of(("a", Map.Of(("x", 1))))).ShouldEqual(Map.Of(("a", Map.Of(("x", 1)))));
    }

    [Fact]
    public void An_empty_second_value_does_not_wipe_the_first()
    {
        // Unlike a replace, merging {} or [] into something keeps what was there.
        _.Merge(Map.Of(("a", Map.Of(("x", 1)))), Map.Of(("a", DataMap.Empty))).ShouldEqual(Map.Of(("a", Map.Of(("x", 1)))));
        _.Merge(Map.Of(("a", List.Of(1, 2))), Map.Of(("a", DataList.Empty))).ShouldEqual(Map.Of(("a", List.Of(1, 2))));
    }

    [Fact]
    public void Merging_is_not_the_same_as_applying_a_diff()
    {
        // A diff's keys are paths into the target; Merge treats them as plain keys.
        var target = Map.Of(("items", List.Of("a", "b")));
        var diff = Map.Of(("items", Map.Of(("1", "X"))));

        _.ApplyDiff(target, diff).ShouldEqual(Map.Of(("items", List.Of("a", "X"))));
        _.Merge(target, diff).ShouldEqual(Map.Of(("items", Map.Of(("1", "X")))));
    }
}
