using DataFirst.Testing;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

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
            _.ApplyDiff(t.Before, _.DiffObjects(t.Before, t.After)).Equals(t.After));

    [Property]
    public Property Merge_is_idempotent() =>
        Prop.ForAll(ScalarEdit.ToArbitrary(), t =>
        {
            var diff = _.DiffObjects(t.Before, t.After);
            var once = _.ApplyDiff(t.Before, diff);
            return _.ApplyDiff(once, diff).Equals(once);
        });

    [Property]
    public bool Merging_an_empty_diff_changes_nothing(DataMap map) =>
        _.ApplyDiff(map, DataMap.Empty).Equals(map);

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
        var created = _.ApplyDiff((DataValue)DataNull.Instance, diff);
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
        // Regression: ApplyDiff walked InformationPaths, which reports the root of an empty
        // diff, and wrote the empty diff over the whole target.
        var target = Map.Of(("title", "Watchmen"), ("items", List.Of(1, 2)));
        _.ApplyDiff(target, DataMap.Empty).ShouldEqual(target);
        _.ApplyDiff((DataValue)target, DataMap.Empty).As<DataMap>().ShouldEqual(target);
        _.ApplyDiff((DataValue)DataNull.Instance, DataMap.Empty).Equals((DataValue)DataNull.Instance).Should().BeTrue();
    }

    [Fact]
    public void Merge_into_a_typed_map_returns_a_map()
    {
        _.ApplyDiff(Map.Of(("a", 1)), Map.Of(("a", 2))).ShouldEqual(Map.Of(("a", 2)));
    }
}
