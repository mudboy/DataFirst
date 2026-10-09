using DataFirst.Lodash;
using DataFirst.Testing;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class SortByTests
{
    /// Rows with a small sort key (so ties are common) and a sequence number that
    /// records the original position.
    private static readonly Gen<DataList> Rows =
        Gen.Choose(0, 12).SelectMany(n =>
            Gen.Choose(0, 4).ArrayOf(n).Select(keys =>
                DataList.Create(keys.Select((key, seq) => (DataValue)Map.Of(("n", key), ("seq", seq))).ToList())));

    private static readonly Gen<DataList> Scalars =
        Gen.Choose(0, 10).SelectMany(n => Gens.Scalar.ArrayOf(n).Select(DataList.Create));

    private static long N(DataValue row) => _.Get<long>(row, "n");
    private static long Seq(DataValue row) => _.Get<long>(row, "seq");

    private static string Fingerprint(IEnumerable<DataValue> values) =>
        string.Join("|", values.Select(v => v.ToString()).Order(StringComparer.Ordinal));

    [Property]
    public Property The_result_is_a_permutation_of_the_input() =>
        Prop.ForAll(Rows.ToArbitrary(), rows =>
            _.SortBy(rows, "n").Count == rows.Count && Fingerprint(_.SortBy(rows, "n")) == Fingerprint(rows));

    [Property]
    public Property Keys_come_out_in_ascending_order() =>
        Prop.ForAll(Rows.ToArbitrary(), rows =>
            _.SortBy(rows, "n").Select(N).SequenceEqual(rows.Select(N).Order()));

    [Property]
    public Property Elements_with_equal_keys_keep_their_original_order() =>
        Prop.ForAll(Rows.ToArbitrary(), rows =>
            _.SortBy(rows, "n").Zip(_.SortBy(rows, "n").Skip(1))
                .Where(pair => N(pair.First) == N(pair.Second))
                .All(pair => Seq(pair.First) < Seq(pair.Second)));

    [Property]
    public Property Sorting_twice_changes_nothing() =>
        Prop.ForAll(Rows.ToArbitrary(), rows =>
            _.SortBy(_.SortBy(rows, "n"), "n").Equals(_.SortBy(rows, "n")));

    [Property]
    public Property Sorting_by_the_original_position_restores_the_original_order() =>
        Prop.ForAll(Rows.ToArbitrary(), rows => _.SortBy(_.SortBy(rows, "n"), "seq").Equals(rows));

    [Property]
    public Property The_key_function_runs_once_per_element() =>
        Prop.ForAll(Rows.ToArbitrary(), rows =>
        {
            var calls = 0;
            _.SortBy(rows, row => { calls++; return _.Get(row, "n"); });
            return calls == rows.Count;
        });

    [Property]
    public Property Sorting_by_a_constant_key_keeps_the_order() =>
        Prop.ForAll(Rows.ToArbitrary(), rows => _.SortBy(rows, _ => 0L).Equals(rows));

    [Property]
    public Property A_map_sorts_its_values() =>
        Prop.ForAll(Rows.ToArbitrary(), rows =>
        {
            var keyed = _.KeyBy(rows, row => $"k{Seq(row)}");
            return _.SortBy(keyed, "n").Equals(_.SortBy(_.Values(keyed), "n"));
        });

    [Property]
    public Property Any_scalars_sort_by_kind_then_value_and_never_throw() =>
        Prop.ForAll(Scalars.ToArbitrary(), values =>
        {
            Func<DataValue, int> rank = v => v switch { DataNull => 0, bool => 1, long or double => 2, _ => 3 };
            Func<DataValue, double> number = v => v switch { long n => n, double d => d, _ => 0 };
            Func<DataValue, string> text = v => v switch { string s => s, _ => "" };
            Func<DataValue, bool> flag = v => v switch { bool b => b, _ => false };

            var expected = values.OrderBy(rank).ThenBy(flag).ThenBy(number).ThenBy(text, StringComparer.Ordinal);
            return _.SortBy(values, v => v).SequenceEqual(expected);
        });

    [Property]
    public Property Sorting_scalars_is_a_permutation_and_idempotent() =>
        Prop.ForAll(Scalars.ToArbitrary(), values =>
        {
            var sorted = _.SortBy(values, v => v);
            return Fingerprint(sorted) == Fingerprint(values) && _.SortBy(sorted, v => v).Equals(sorted);
        });

    [Fact]
    public void Kinds_order_as_null_then_bool_then_number_then_string()
    {
        _.SortBy(List.Of("b", 2, true, DataNull.Instance, "a", 1.5, false, 1), v => v)
            .ShouldEqual(List.Of(DataNull.Instance, false, true, 1, 1.5, 2, "a", "b"));
    }

    [Fact]
    public void A_long_and_a_double_compare_by_value()
    {
        _.SortBy(List.Of(2.5, 3L, 1L, 2L, 0.5), v => v).ShouldEqual(List.Of(0.5, 1, 2, 2.5, 3));
        _.SortBy(List.Of(1.0, 1L), v => v).ShouldEqual(List.Of(1.0, 1)); // equal keys: stable
        _.SortBy(List.Of(1L, 1.0), v => v).ShouldEqual(List.Of(1, 1.0));
    }

    [Fact]
    public void Strings_compare_ordinally_so_case_and_accents_do_not_get_clever()
    {
        _.SortBy(List.Of("a", "B", "b", "A", "é", "z"), v => v).ShouldEqual(List.Of("A", "B", "a", "b", "z", "é"));
    }

    [Fact]
    public void Large_longs_compare_exactly_not_through_a_lossy_double()
    {
        var big = long.MaxValue;
        _.SortBy(List.Of(big, big - 1), v => v).ShouldEqual(List.Of(big - 1, big));
    }

    [Fact]
    public void Sorting_by_a_field_reads_it_from_each_row()
    {
        var books = List.Of(
            Map.Of(("title", "Watchmen"), ("year", 1987)),
            Map.Of(("title", "Maus"), ("year", 1980)),
            Map.Of(("title", "Sandman"), ("year", 1989)));

        _.SortBy(books, "year").Select(b => _.Get<string>(b, "title")).Should().Equal("Maus", "Watchmen", "Sandman");
        _.SortBy(books, "title").Select(b => _.Get<string>(b, "title")).Should().Equal("Maus", "Sandman", "Watchmen");
    }

    [Fact]
    public void A_map_or_list_is_not_a_sortable_key_and_the_error_says_so()
    {
        new Action(() => _.SortBy(List.Of(1, 2), v => Map.Of(("k", v))))
            .Should().Throw<InvalidOperationException>().WithMessage("*Cannot sort by a map*");
        new Action(() => _.SortBy(List.Of(List.Of(1), List.Of(2)), v => v))
            .Should().Throw<InvalidOperationException>().WithMessage("*list*");
    }

    [Fact]
    public void A_single_element_with_an_unsortable_key_is_still_refused()
    {
        // Regression: a sort never compares a lone element, so its key was never checked.
        new Action(() => _.SortBy(List.Of(1), v => Map.Of(("k", v)))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Sorting_nothing_gives_nothing_and_a_single_element_gives_itself()
    {
        _.SortBy(DataList.Empty, v => v).ShouldEqual(DataList.Empty);
        _.SortBy(DataMap.Empty, v => v).ShouldEqual(DataList.Empty);
        _.SortBy(List.Of("only"), v => v).ShouldEqual(List.Of("only"));
    }
}
