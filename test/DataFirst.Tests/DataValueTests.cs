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
public sealed class DataValueTests
{
    [Fact]
    public void Unwrap_gives_the_underlying_value_for_every_case()
    {
        ((DataValue)DataNull.Instance).Unwrap().Should().BeNull();
        ((DataValue)"s").Unwrap().Should().Be("s");
        ((DataValue)5L).Unwrap().Should().Be(5L);
        ((DataValue)1.5).Unwrap().Should().Be(1.5);
        ((DataValue)true).Unwrap().Should().Be(true);
        var map = Map.Of(("a", 1));
        ReferenceEquals(((DataValue)map).Unwrap(), map).Should().BeTrue();
        var list = List.Of(1);
        ReferenceEquals(((DataValue)list).Unwrap(), list).Should().BeTrue();
    }

    [Fact]
    public void An_int_literal_becomes_a_long()
    {
        ((DataValue)1987).Unwrap().Should().BeOfType<long>();
    }

    [Fact]
    public void As_returns_the_value_at_the_right_type_and_names_the_actual_case_otherwise()
    {
        ((DataValue)"s").As<string>().Should().Be("s");
        ((DataValue)7L).As<long>().Should().Be(7L);

        var wrong = () => ((DataValue)7L).As<string>();
        wrong.Should().Throw<InvalidOperationException>().WithMessage("*String*number (long)*");

        var map = () => ((DataValue)Map.Of(("a", 1))).As<DataList>();
        map.Should().Throw<InvalidOperationException>().WithMessage("*map[1]*");
    }

    [Fact]
    public void Describe_names_each_case()
    {
        ((DataValue)DataNull.Instance).Describe().Should().Be("null");
        ((DataValue)"s").Describe().Should().Be("string");
        ((DataValue)1L).Describe().Should().Be("number (long)");
        ((DataValue)1.5).Describe().Should().Be("number (double)");
        ((DataValue)true).Describe().Should().Be("bool");
        ((DataValue)Map.Of(("a", 1), ("b", 2))).Describe().Should().Be("map[2]");
        ((DataValue)List.Of(1, 2, 3)).Describe().Should().Be("list[3]");
    }

    [Property]
    public bool Only_maps_and_lists_are_composite(DataValue value) =>
        value.IsComposite() == (value.Unwrap() is DataMap or DataList);

    [Property]
    public bool Equality_is_structural_across_a_rebuild(DataValue value) =>
        value.Equals(Rebuild(value));

    [Property]
    public bool A_long_never_equals_a_double_or_string_of_the_same_number(NonNegativeInt n)
    {
        DataValue integer = (long)n.Get;
        return !integer.Equals((DataValue)(double)n.Get) && !integer.Equals((DataValue)n.Get.ToString());
    }

    [Fact]
    public void Null_equals_only_null()
    {
        ((DataValue)DataNull.Instance).Equals((DataValue)DataNull.Instance).Should().BeTrue();
        ((DataValue)DataNull.Instance).Equals((DataValue)"null").Should().BeFalse();
        DataNull.Instance.ToString().Should().Be("null");
    }

    /// Rebuilds a value from scratch so equality cannot be satisfied by reference.
    private static DataValue Rebuild(DataValue value) =>
        value switch
        {
            DataMap m => m.Aggregate(DataMap.Empty, (acc, kv) => acc.SetItem(kv.Key, Rebuild(kv.Value))),
            DataList l => DataList.Create(l.Select(Rebuild).ToList()),
            var other => other
        };
}
