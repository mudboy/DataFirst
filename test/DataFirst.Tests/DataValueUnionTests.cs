using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Database;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Tests;

public sealed class DataValueUnionTests
{
    [Fact]
    public void Should_Switch_Exhaustively_Over_Every_Case()
    {
        // No default arm: the compiler checks this covers the union.
        static string Name(DataValue v) => v switch
        {
            DataNull => "null",
            string => "string",
            long => "long",
            double => "double",
            bool => "bool",
            DataMap => "map",
            DataList => "list"
        };

        Name(DataNull.Instance).Should().Be("null");
        Name("x").Should().Be("string");
        Name(1987).Should().Be("long");
        Name(1.5).Should().Be("double");
        Name(true).Should().Be("bool");
        Name(Map.Of()).Should().Be("map");
        Name(List.Of()).Should().Be("list");
    }
}
