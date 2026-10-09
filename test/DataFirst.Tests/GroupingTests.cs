using DataFirst.Testing;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class GroupingTests
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
