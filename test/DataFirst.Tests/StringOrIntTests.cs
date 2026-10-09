using DataFirst.Testing;
using System.Text.Json;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

public sealed class StringOrIntTests
{
    [Property]
    public bool An_int_step_is_its_own_index(int i)
    {
        StringOrInt step = i;
        return step.TryAsIndex(out var index) && index == i;
    }

    [Property]
    public bool A_numeric_string_step_parses_as_an_index(int i)
    {
        StringOrInt step = i.ToString();
        return step.TryAsIndex(out var index) && index == i;
    }

    [Theory]
    [InlineData("")]
    [InlineData("title")]
    [InlineData("1.5")]
    [InlineData("1e3")]
    [InlineData(" ")]
    [InlineData("99999999999")]
    public void A_non_numeric_string_is_not_an_index(string text)
    {
        StringOrInt step = text;
        step.TryAsIndex(out var index).Should().BeFalse();
        index.Should().Be(0);
    }

    [Fact]
    public void Describes_keys_and_indices()
    {
        ((StringOrInt)"title").Describe().Should().Be("key 'title'");
        ((StringOrInt)3).Describe().Should().Be("index 3");
    }
}
