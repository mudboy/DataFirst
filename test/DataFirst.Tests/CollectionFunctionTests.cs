using DataFirst.Lodash;
using DataFirst.Testing;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

/// The appendix's "functions on collections": each takes a list or a map and works on
/// the list's elements or the map's values.
[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class CollectionFunctionTests
{
    private static bool IsString(DataValue value) => value is string;

    /// A list, or a map of the same elements, so every property runs over both.
    private static Gen<(DataValue Coll, List<DataValue> Elements)> AnyCollection =>
        Gen.OneOf(
            ListGens.Pool.Select(l => ((DataValue)l, l.ToList())),
            Gens.Map.Select(m => ((DataValue)m, m.Values.ToList())));

    private static Property OverCollections(Func<DataValue, List<DataValue>, bool> check) =>
        Prop.ForAll(AnyCollection.ToArbitrary(), t => check(t.Coll, t.Elements));

    // every

    [Property]
    public Property Every_is_All_over_the_elements() =>
        OverCollections((coll, elements) =>
            _.Every(coll, IsString) == elements.All(IsString) && _.Every(coll, _ => true));

    [Property]
    public Property Every_holds_vacuously_for_nothing() =>
        Prop.ForAll(Gen.Constant(0).ToArbitrary(), unit =>
            _.Every(DataList.Empty, v => false) && _.Every(DataMap.Empty, v => false));

    [Property]
    public Property Every_fails_exactly_when_filtering_loses_something() =>
        OverCollections((coll, items) => _.Every(coll, IsString) == (_.Filter(coll, IsString).Count == _.Size(coll)));

    [Property]
    public Property Every_over_a_concatenation_is_every_over_both() =>
        Prop.ForAll((from a in ListGens.Pool from b in ListGens.Pool select (a, b)).ToArbitrary(), t =>
            _.Every(_.Concat(t.a, t.b), IsString) == (_.Every(t.a, IsString) && _.Every(t.b, IsString)));

    // find

    [Property]
    public Property Find_returns_the_first_match() =>
        OverCollections((coll, elements) =>
            _.Find(coll, IsString).Equals(elements.FirstOrDefault(IsString, DataNull.Instance)));

    [Property]
    public Property Find_agrees_with_the_head_of_Filter_when_something_matches() =>
        OverCollections((coll, items) =>
        {
            var matches = _.Filter(coll, IsString);
            return matches.IsEmpty || _.Find(coll, IsString).Equals(matches[0]);
        });

    [Property]
    public Property Find_is_null_exactly_when_nothing_matches_for_a_predicate_no_null_can_satisfy() =>
        OverCollections((coll, elements) =>
            (_.Find(coll, IsString) is DataNull) == !elements.Any(IsString));

    [Property]
    public Property Find_stops_at_the_first_match() =>
        Prop.ForAll(Gen.Choose(0, 6).ToArbitrary(), index =>
        {
            var list = DataList.Create(Enumerable.Range(0, 8).Select(i => (DataValue)(long)i).ToList());
            var seen = new List<long>();

            var found = _.Find(list, v => { seen.Add(v.As<long>()); return v.As<long>() == index; });

            return found.Equals((DataValue)(long)index) && seen.SequenceEqual(Enumerable.Range(0, index + 1).Select(i => (long)i));
        });

    // forEach

    [Property]
    public Property ForEach_visits_every_element_once_in_order_and_returns_the_collection() =>
        OverCollections((coll, elements) =>
        {
            var visited = new List<DataValue>();
            var returned = _.ForEach(coll, visited.Add);
            return visited.SequenceEqual(elements) && returned.Equals(coll);
        });

    // size

    [Property]
    public Property Size_counts_elements() =>
        OverCollections((coll, elements) => _.Size(coll) == elements.Count);

    [Property]
    public Property Size_is_zero_exactly_when_the_collection_is_empty() =>
        OverCollections((coll, items) => (_.Size(coll) == 0) == _.IsEmpty(coll));

    [Property]
    public Property Size_agrees_with_the_number_of_keys() =>
        OverCollections((coll, items) => _.Size(coll) == _.Keys(coll).Count);

    [Property]
    public Property Mapping_preserves_size() =>
        OverCollections((coll, items) => _.Size(_.Map(coll, v => v)) == _.Size(coll));

    [Property]
    public Property Size_of_a_concatenation_is_the_sum() =>
        Prop.ForAll((from a in ListGens.Pool from b in ListGens.Pool select (a, b)).ToArbitrary(), t =>
            _.Size(_.Concat(t.a, t.b)) == _.Size(t.a) + _.Size(t.b));

    // filter, keyBy over maps

    [Property]
    public Property Filter_keeps_the_matching_elements_in_order() =>
        OverCollections((coll, elements) =>
            _.Filter(coll, IsString).SequenceEqual(elements.Where(IsString)));

    [Property]
    public Property Filter_splits_a_collection_into_two_parts_that_add_up() =>
        OverCollections((coll, items) =>
            _.Filter(coll, IsString).Count + _.Filter(coll, v => !IsString(v)).Count == _.Size(coll));

    [Property]
    public bool Filtering_a_map_gives_the_same_list_as_filtering_its_values(DataMap map) =>
        _.Filter(map, IsString).Equals(_.Filter(_.Values(map), IsString));

    [Property]
    public Property KeyBy_a_function_has_one_entry_per_distinct_key_and_the_last_element_wins() =>
        OverCollections((coll, elements) =>
        {
            Func<DataValue, string> kind = v => v.Describe();
            var keyed = _.KeyBy(coll, kind);

            return keyed.Keys.SequenceEqual(elements.Select(kind).Distinct())
                   && keyed.All(kv => kv.Value.Equals(elements.Last(e => kind(e) == kv.Key)));
        });

    [Property]
    public bool KeyBy_over_a_map_is_KeyBy_over_its_values(DataMap map) =>
        _.KeyBy(map, v => v.Describe()).Equals(_.KeyBy(_.Values(map), v => v.Describe()));

    // isArray, isObject, isEqual

    [Property]
    public bool IsArray_is_true_for_lists_only(DataValue value) =>
        _.IsArray(value) == (value is DataList);

    [Property]
    public bool A_collection_is_an_array_or_a_map_and_never_both(DataValue value) =>
        _.IsObject(value) == (_.IsArray(value) || value is DataMap)
        && !(_.IsArray(value) && value is DataMap);

    [Property]
    public bool IsEqual_is_the_deep_equality_of_the_data(DataValue first, DataValue second) =>
        _.IsEqual(first, second) == first.Equals(second);

    [Property]
    public bool IsEqual_is_reflexive_and_symmetric(DataValue first, DataValue second) =>
        _.IsEqual(first, first) && _.IsEqual(first, second) == _.IsEqual(second, first);

    [Property]
    public bool IsEqual_ignores_map_key_order(DataMap map) =>
        _.IsEqual(map, map.Reverse().Aggregate(DataMap.Empty, (m, kv) => m.SetItem(kv.Key, kv.Value)));

    [Property]
    public bool IsEqual_respects_list_order(DataValue first, DataValue second) =>
        first.Equals(second) || !_.IsEqual(List.Of(first, second), List.Of(second, first));

    [Property]
    public Property IsEqual_notices_a_change_anywhere_deep_inside() =>
        Prop.ForAll(
            (from pair in Gens.MapWithExistingPath from value in Gens.Value select (pair.Map, pair.Path, value)).ToArbitrary(),
            t => _.IsEqual(t.Map, _.Set(t.Map, t.Path, t.value)) == _.Get(t.Map, t.Path).Equals(t.value));

    [Fact]
    public void IsEqual_tells_a_long_from_a_double_as_Diff_does()
    {
        _.IsEqual(1L, 1.0).Should().BeFalse();
        _.IsEqual(Map.Of(("a", 1)), Map.Of(("a", 1))).Should().BeTrue();
    }

    // scalars are not collections

    [Fact]
    public void The_collection_functions_refuse_scalars_and_name_them()
    {
        new Action(() => _.Every("s", _ => true)).Should().Throw<InvalidOperationException>().WithMessage("*string*");
        new Action(() => _.Find(5, _ => true)).Should().Throw<InvalidOperationException>().WithMessage("*number*");
        new Action(() => _.ForEach(true, _ => { })).Should().Throw<InvalidOperationException>().WithMessage("*bool*");
        new Action(() => _.Size(DataNull.Instance)).Should().Throw<InvalidOperationException>().WithMessage("*null*");
        new Action(() => _.Filter(5, _ => true)).Should().Throw<InvalidOperationException>();
        new Action(() => _.KeyBy("s", _ => "k")).Should().Throw<InvalidOperationException>();
        new Action(() => _.SortBy(5, v => v)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Find_cannot_tell_a_matching_null_from_no_match()
    {
        var list = List.Of(1, DataNull.Instance);

        (_.Find(list, v => v is DataNull) is DataNull).Should().BeTrue();
        (_.Find(list, v => v is string) is DataNull).Should().BeTrue();
        _.Filter(list, v => v is DataNull).Count.Should().Be(1);
    }

    [Fact]
    public void The_book_style_examples_hold()
    {
        var users = List.Of(
            Map.Of(("user", "barney"), ("age", 36), ("active", true)),
            Map.Of(("user", "fred"), ("age", 40), ("active", false)),
            Map.Of(("user", "pebbles"), ("age", 1), ("active", true)));

        _.Every(users, u => u.As<DataMap>()["age"].As<long>() > 0).Should().BeTrue();
        _.Find(users, u => u.As<DataMap>()["age"].As<long>() < 40).As<DataMap>()["user"].Equals((DataValue)"barney").Should().BeTrue();
        _.Size(users).Should().Be(3);
        _.IsArray(users).Should().BeTrue();
        _.KeyBy(users, "user").Keys.Should().Equal("barney", "fred", "pebbles");
        _.Filter(Map.Of(("a", 1), ("b", "x"), ("c", 2)), v => v is long).ShouldEqual(List.Of(1, 2));
    }
}
