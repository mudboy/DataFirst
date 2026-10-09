using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Database;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Tests;

public sealed class ReadingDataTests
{
    private static readonly DataMap watchmenMap = Map.Of(
        ("isbn", "978-1779501127"),
        ("title", "Watchmen"),
        ("publicationYear", 1987)
    );
    private readonly DataMap searchResultsMap = Map.Of(
        ("978-1779501127", watchmenMap),
        ("978-1982137274", sevenHabitsMap)
    );
    private static readonly DataMap sevenHabitsMap = Map.Of(
        ("isbn", "978-1982137274"),
        ("title", "7 Habits of Highly Effective People"),
        ("publicationYear", 2020)
    );
    [Fact]
    public void Should_Get_Key()
    {
        _.Get<string>(watchmenMap, "title").ToUpper()
            .Should().Be("WATCHMEN");
    }
    [Fact]
    public void Should_Get_By_Path()
    {
        _.Get<string>(searchResultsMap, ["978-1779501127", "title"])
            .Should().Be("Watchmen");
    }
    [Fact]
    public void Should_Check_By_Path()
    {
        _.ContainsKey(searchResultsMap, ["978-1779501127", "title"])
            .Should().BeTrue();
    }
    [Fact]
    public void Should_Be_Able_To_Use_Key_Getters()
    {
        var TITLE = Getter.Create<string>("title");

        TITLE.Get(watchmenMap).ToUpper().Should().Be("WATCHMEN");
    }
    [Fact]
    public void Should_Be_Able_To_Use_Path_Getters()
    {
        var TITLE = Getter.Create<string>(["978-1779501127", "title"]);

        TITLE.Get(searchResultsMap).ToUpper().Should().Be("WATCHMEN");
    }
    [Fact]
    public void Should_Walk_A_Path_That_Mixes_Keys_And_Indexes()
    {
        var data = Map.Of(
            ("a", List.Of(
                Map.Of(("x", "wrong")),
                Map.Of(("b", List.Of("zero", "one", "two"))))));

        _.Get<string>(data, ["a", 1, "b", 2]).Should().Be("two");
    }
    [Fact]
    public void Should_Check_A_Path_Through_Lists()
    {
        var data = Map.Of(("a", List.Of(Map.Of(("b", "value")))));

        _.ContainsKey(data, ["a", 0, "b"]).Should().BeTrue();
        _.ContainsKey(data, ["a", 0, "missing"]).Should().BeFalse();
        _.ContainsKey(data, ["a", 9, "b"]).Should().BeFalse();

        // A leaf part-way along the path is absence, not an exception.
        _.ContainsKey(data, ["a", 0, "b", "deeper"]).Should().BeFalse();
    }
    [Fact]
    public void Should_Read_Several_Keys_At_Once()
    {
        var book = Map.Of(
            ("isbn", "978-1779501127"),
            ("title", "Watchmen"),
            ("publicationYear", 1987));

        _.At(book, "title", "isbn").ShouldEqual(List.Of("Watchmen", "978-1779501127"));

        // Order follows the key list, not the map.
        _.At(book, "isbn", "title").ShouldEqual(List.Of("978-1779501127", "Watchmen"));
    }
    [Fact]
    public void Should_Yield_Null_For_A_Key_That_Is_Not_There()
    {
        var book = Map.Of(("title", "Watchmen"));

        // The result is always as long as the key list, so it can be zipped back
        // against those keys without the positions drifting.
        (_.At(book, "title", "publisher", "title")).ShouldEqual(List.Of("Watchmen", DataNull.Instance, "Watchmen"));

        _.At(book).Should().BeEmpty();
    }
    [Fact]
    public void Should_Read_At_Indexes_Of_A_List()
    {
        var authors = List.Of("Alan Moore", "Dave Gibbons", "John Higgins");

        _.At(authors, 2, 0).ShouldEqual(List.Of("John Higgins", "Alan Moore"));

        // Out of range is absent, not an error.
        _.At(authors, 9).ShouldEqual(List.Of(DataNull.Instance));
    }
    [Fact]
    public void Should_Take_Keys_From_A_Collection()
    {
        var book = Map.Of(("isbn", "978-1779501127"), ("title", "Watchmen"));
        var wanted = new List<StringOrInt> { "title", "isbn" };

        _.At(book, wanted).ShouldEqual(List.Of("Watchmen", "978-1779501127"));
    }
}
