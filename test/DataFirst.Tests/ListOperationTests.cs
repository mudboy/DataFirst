using DataFirst.Testing;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class ListOperationTests
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
