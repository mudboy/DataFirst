using DataFirst.Testing;
using System.Text.Json;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class DataListTests
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
