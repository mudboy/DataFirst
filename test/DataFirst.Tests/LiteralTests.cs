using DataFirst.Testing;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

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
