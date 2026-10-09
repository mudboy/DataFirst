using DataFirst.Lodash;
using DataFirst.Testing;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

public sealed class ArrayFunctionProperties
{
    private static Property Over(Gen<DataList> gen, Func<DataList, bool> check) =>
        Prop.ForAll(gen.ToArbitrary(), check);

    private static Property Over2(Gen<DataList> gen, Func<DataList, DataList, bool> check) =>
        Prop.ForAll((from a in gen from b in gen select (a, b)).ToArbitrary(), t => check(t.a, t.b));

    private static bool SameMembers(DataList a, DataList b) =>
        a.All(b.Contains) && b.All(a.Contains);

    // concat

    [Property]
    public Property Concat_is_the_two_lists_end_to_end() =>
        Over2(ListGens.Pool, (a, b) => _.Concat(a, b).SequenceEqual(a.Concat(b)) && _.Concat(a, b).Count == a.Count + b.Count);

    [Property]
    public Property Concat_has_the_empty_list_as_identity() =>
        Over(ListGens.Pool, a => _.Concat(a, DataList.Empty).Equals(a) && _.Concat(DataList.Empty, a).Equals(a));

    [Property]
    public Property Concat_is_associative() =>
        Prop.ForAll((from a in ListGens.Pool from b in ListGens.Pool from c in ListGens.Pool select (a, b, c)).ToArbitrary(), t =>
            _.Concat(_.Concat(t.a, t.b), t.c).Equals(_.Concat(t.a, _.Concat(t.b, t.c))));

    [Property]
    public Property Concat_leaves_its_inputs_alone() =>
        Over2(ListGens.Pool, (a, b) =>
        {
            var (before1, before2) = (a.ToString(), b.ToString());
            _.Concat(a, b);
            return a.ToString() == before1 && b.ToString() == before2;
        });

    // flatten

    [Property]
    public Property Flatten_splices_list_elements_and_keeps_the_rest_in_place() =>
        Over(ListGens.Nested, list =>
            _.Flatten(list).SequenceEqual(list.SelectMany(e => e is DataList inner ? inner.ToList() : [e])));

    [Property]
    public Property Flatten_of_a_list_of_lists_is_their_concatenation() =>
        Over2(ListGens.Pool, (a, b) =>
            _.Flatten(List.Of(a, b)).Equals(_.Concat(a, b)));

    [Property]
    public Property Flatten_goes_one_level_only() =>
        Over(ListGens.Pool, a => _.Flatten(List.Of(List.Of(a))).Equals(List.Of(a)));

    [Property]
    public Property Flatten_does_nothing_to_a_list_with_no_list_elements() =>
        Over(ListGens.Pool.Select(l => _.Filter(l, v => v is not DataList)), a => _.Flatten(a).Equals(a));

    [Property]
    public Property Flatten_never_opens_maps() =>
        Over(ListGens.Pool, a => _.Flatten(a.Add(Map.Of(("k", List.Of(1))))).Last().Equals((DataValue)Map.Of(("k", List.Of(1)))));

    // uniq

    [Property]
    public Property Uniq_has_no_duplicates() =>
        Over(ListGens.Pool, a => _.Uniq(a).Distinct().Count() == _.Uniq(a).Count);

    [Property]
    public Property Uniq_keeps_every_value_at_its_first_position() =>
        Over(ListGens.Pool, a =>
            _.Uniq(a).SequenceEqual(a.Where((v, i) => a.Take(i).All(earlier => !earlier.Equals(v)))));

    [Property]
    public Property Uniq_is_idempotent() =>
        Over(ListGens.Pool, a => _.Uniq(_.Uniq(a)).Equals(_.Uniq(a)));

    [Property]
    public Property Uniq_loses_no_value() =>
        Over(ListGens.Pool, a => SameMembers(_.Uniq(a), a));

    [Property]
    public Property Uniq_of_a_list_without_duplicates_is_that_list() =>
        Over(ListGens.Pool.Select(_.Uniq), a => _.Uniq(a).Equals(a));

    [Property]
    public Property Uniq_tells_a_long_from_a_double_and_a_string() =>
        Over(Gen.Constant(List.Of(1L, 1.0, "1", true)), a => _.Uniq(a).Count == 4);

    // union

    [Property]
    public Property Union_is_the_uniq_of_the_concatenation() =>
        Over2(ListGens.Pool, (a, b) => _.Union(a, b).Equals(_.Uniq(_.Concat(a, b))));

    [Property]
    public Property Union_holds_every_value_of_both_once() =>
        Over2(ListGens.Pool, (a, b) =>
        {
            var union = _.Union(a, b);
            return a.All(union.Contains) && b.All(union.Contains)
                   && union.Distinct().Count() == union.Count
                   && union.All(v => a.Contains(v) || b.Contains(v));
        });

    [Property]
    public Property Union_is_commutative_as_a_set() =>
        Over2(ListGens.Pool, (a, b) => SameMembers(_.Union(a, b), _.Union(b, a)));

    [Property]
    public Property Union_with_itself_or_with_nothing_is_uniq() =>
        Over(ListGens.Pool, a => _.Union(a, a).Equals(_.Uniq(a)) && _.Union(a, DataList.Empty).Equals(_.Uniq(a)));

    [Property]
    public Property Union_puts_the_first_lists_values_first() =>
        Over2(ListGens.Pool, (a, b) => _.Union(a, b).Take(_.Uniq(a).Count).SequenceEqual(_.Uniq(a)));

    [Fact]
    public void Union_of_key_lists_still_works_alongside_the_value_overload()
    {
        _.Union([(StringOrInt)"a", "b"], [(StringOrInt)"b", "c"]).Should().Equal("a", "b", "c");
    }

    // intersection

    [Property]
    public Property Intersection_holds_exactly_the_values_in_both() =>
        Over2(ListGens.Pool, (a, b) =>
        {
            var both = _.Intersection(a, b);
            return both.All(v => a.Contains(v) && b.Contains(v))
                   && a.Where(b.Contains).All(both.Contains)
                   && both.Distinct().Count() == both.Count;
        });

    [Property]
    public Property Intersection_follows_the_first_lists_order() =>
        Over2(ListGens.Pool, (a, b) =>
            _.Intersection(a, b).SequenceEqual(_.Uniq(a).Where(b.Contains)));

    [Property]
    public Property Intersection_is_commutative_as_a_set() =>
        Over2(ListGens.Pool, (a, b) => SameMembers(_.Intersection(a, b), _.Intersection(b, a)));

    [Property]
    public Property Intersection_with_itself_is_uniq_and_with_nothing_is_nothing() =>
        Over(ListGens.Pool, a =>
            _.Intersection(a, a).Equals(_.Uniq(a))
            && _.Intersection(a, DataList.Empty).IsEmpty
            && _.Intersection(DataList.Empty, a).IsEmpty);

    [Property]
    public Property Intersection_sits_inside_the_union_and_the_inclusion_exclusion_sizes_add_up() =>
        Over2(ListGens.Pool, (a, b) =>
            _.Intersection(a, b).All(_.Union(a, b).Contains)
            && _.Union(a, b).Count + _.Intersection(a, b).Count == _.Uniq(a).Count + _.Uniq(b).Count);

    // nth

    [Property]
    public Property Nth_inside_the_list_is_the_element() =>
        Over(ListGens.Pool.Where(l => !l.IsEmpty), a =>
            Enumerable.Range(0, a.Count).All(i => _.Nth(a, i).Equals(a[i])));

    [Property]
    public Property Nth_negative_counts_back_from_the_end() =>
        Over(ListGens.Pool.Where(l => !l.IsEmpty), a =>
            Enumerable.Range(1, a.Count).All(i => _.Nth(a, -i).Equals(a[a.Count - i])));

    [Property]
    public Property Nth_outside_the_list_is_null_never_an_exception() =>
        Over(ListGens.Pool, a =>
            _.Nth(a, a.Count).Equals((DataValue)DataNull.Instance)
            && _.Nth(a, -a.Count - 1).Equals((DataValue)DataNull.Instance)
            && _.Nth(a, int.MaxValue).Equals((DataValue)DataNull.Instance)
            && _.Nth(a, int.MinValue).Equals((DataValue)DataNull.Instance));

    [Property]
    public Property Nth_zero_and_minus_one_are_first_and_last() =>
        Over(ListGens.Pool.Where(l => !l.IsEmpty), a =>
            _.Nth(a, 0).Equals(a.First()) && _.Nth(a, -1).Equals(a.Last()));

    // sum

    [Property]
    public Property Sum_of_longs_is_a_long_equal_to_the_ordinary_total() =>
        Over(ListGens.Longs, a =>
            _.Sum(a).Unwrap() is long total && total == a.Sum(v => v.As<long>()));

    [Property]
    public Property Sum_of_doubles_is_a_double() =>
        Over(ListGens.Doubles.Where(l => !l.IsEmpty), a =>
            _.Sum(a).Unwrap() is double total && Math.Abs(total - a.Sum(v => v.As<double>())) < 1e-9);

    [Property]
    public Property Sum_of_mixed_numbers_is_a_double() =>
        Over(ListGens.Longs.Where(l => !l.IsEmpty), a =>
            _.Sum(a.Add(0.5)).Unwrap() is double total && Math.Abs(total - (a.Sum(v => v.As<long>()) + 0.5)) < 1e-9);

    [Property]
    public Property Sum_adds_across_a_concatenation() =>
        Over2(ListGens.Longs, (a, b) =>
            _.Sum(_.Concat(a, b)).Equals((DataValue)(_.Sum(a).As<long>() + _.Sum(b).As<long>())));

    [Property]
    public Property Sum_does_not_depend_on_order() =>
        Over(ListGens.Longs, a => _.Sum(DataList.Create(a.Reverse().ToList())).Equals(_.Sum(a)));

    [Property]
    public Property Sum_ignores_zeros_and_negates_with_negation() =>
        Over(ListGens.Longs, a =>
            _.Sum(a.Add(0L)).Equals(_.Sum(a))
            && _.Sum(_.Concat(a, _.Map(a, v => (DataValue)(-v.As<long>())))).Equals((DataValue)0L));

    [Fact]
    public void Sum_of_nothing_is_zero_a_long()
    {
        _.Sum(DataList.Empty).Unwrap().Should().Be(0L);
    }

    [Fact]
    public void Sum_refuses_anything_that_is_not_a_number_and_names_it()
    {
        new Action(() => _.Sum(List.Of(1, "2"))).Should().Throw<InvalidOperationException>().WithMessage("*string*");
        new Action(() => _.Sum(List.Of(1.5, true))).Should().Throw<InvalidOperationException>().WithMessage("*bool*");
        new Action(() => _.Sum(List.Of(1, DataNull.Instance))).Should().Throw<InvalidOperationException>().WithMessage("*null*");
    }

    [Fact]
    public void Sum_throws_on_long_overflow_rather_than_wrapping()
    {
        new Action(() => _.Sum(List.Of(long.MaxValue, 1L))).Should().Throw<OverflowException>();
    }

    [Fact]
    public void The_lodash_examples_hold()
    {
        _.Concat(List.Of(1), List.Of(2, 3)).ShouldEqual(List.Of(1, 2, 3));
        _.Flatten(List.Of(1, List.Of(2, List.Of(3)))).ShouldEqual(List.Of(1, 2, List.Of(3)));
        _.Intersection(List.Of(2, 1), List.Of(2, 3)).ShouldEqual(List.Of(2));
        _.Union(List.Of(2), List.Of(1, 2)).ShouldEqual(List.Of(2, 1));
        _.Uniq(List.Of(2, 1, 2)).ShouldEqual(List.Of(2, 1));
        _.Nth(List.Of("a", "b", "c", "d"), 1).Equals((DataValue)"b").Should().BeTrue();
        _.Nth(List.Of("a", "b", "c", "d"), -2).Equals((DataValue)"c").Should().BeTrue();
        _.Sum(List.Of(4, 2, 8, 6)).Equals((DataValue)20L).Should().BeTrue();
    }
}
