using DataFirst.Testing;
using System.Data;
using DataFirst.Database;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DataFirst.Tests;

public sealed class DebugDumpTests : IDisposable
{
    private readonly List<string> written = [];

    public void Dispose()
    {
        foreach (var path in written) File.Delete(path);
    }

    [Fact]
    public void Dump_writes_the_json_and_returns_where()
    {
        var path = Debug.Dump("spec-dump", Map.Of(("a", List.Of(1, 2))));
        written.Add(path);

        File.ReadAllText(path).Should().Be("""{"a":[1,2]}""");
        Path.GetFileName(path).Should().Be("spec-dump.json");
    }

    [Fact]
    public void Dump_overwrites_an_earlier_dump()
    {
        written.Add(Debug.Dump("spec-overwrite", 1L));
        var path = Debug.Dump("spec-overwrite", 2L);
        written.Add(path);

        File.ReadAllText(path).Should().Be("2");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Dump_needs_a_name(string context)
    {
        new Action(() => Debug.Dump(context, 1L)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Dump_writes_the_data_not_a_wrapper()
    {
        // Previously this threw: the directory was never created, the method name was
        // a typo, and JsonSerializer wrote the wrapper rather than the data.
        var path = Debug.Dump($"dump-{Guid.NewGuid():N}", Map.Of(
            ("title", "Watchmen"),
            ("authorIds", List.Of("alan-moore"))));

        try
        {
            File.ReadAllText(path).Should()
                .Be("""{"title":"Watchmen","authorIds":["alan-moore"]}""");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
