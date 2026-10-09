using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Database;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Tests;

public sealed class SchemaKeywordTests
{
    [Fact]
    public void Should_Ignore_Keywords_That_Do_Not_Apply_To_The_Value()
    {
        // minimum says nothing about a string, as in JSON Schema.
        var schema = Map.Of(("minimum", 10), ("minLength", 2));

        Validation.Validate(schema, "ab").IsValid().Should().BeTrue();
        Validation.Validate(schema, 20).IsValid().Should().BeTrue();
        Validation.Validate(schema, 5).IsValid().Should().BeFalse();
        Validation.Validate(schema, "a").IsValid().Should().BeFalse();
    }
}
