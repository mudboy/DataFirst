using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class CatalogSearchTests
{
    private static DataMap CatalogFixture => Map.Of(
        ("booksByIsbn", Map.Of(
            ("978-1779501127", Map.Of(
                ("isbn", "978-1779501127"),
                ("title", "Watchmen"),
                ("publicationYear", 1987),
                ("authorIds", List.Of("alan-moore")))),
            ("978-1982137274", Map.Of(
                ("isbn", "978-1982137274"),
                ("title", "7 Habits of Highly Effective People"),
                ("publicationYear", 2020),
                ("authorIds", List.Of("stephen-covey")))))),
        ("authorsById", Map.Of(
            ("alan-moore", Map.Of(("name", "Alan Moore"), ("bookIsbns", List.Of("978-1779501127")))),
            ("stephen-covey", Map.Of(("name", "Stephen Covey"), ("bookIsbns", List.Of("978-1982137274")))))));
    private static IEnumerable<string> TitlesOf(DataList results) =>
        results.Select(r => _.Get<string>(r, "title"));
    [Fact]
    public void Should_Get_AuthorNames()
    {
        var catalogData = _.Get<DataMap>(LibraryOperations.LibraryData, "catalog");
        var book = _.Get<DataMap>(LibraryOperations.LibraryData, ["catalog", "booksByIsbn", "978-1779501127"]);
        
        var names = Catalog.AuthorNames(catalogData, book);

        names.ShouldEqual(List.Of("Alan Moore", "Dave Gibbons"));
    }
    [Fact]
    public void Should_Search_Books_By_Title()
    {   
        var catalogData = _.Get<DataMap>(LibraryOperations.LibraryData, "catalog");

        var result = Catalog.SearchBooksByTitle(catalogData, "Wat");

        result.ShouldEqual(List.Of(
            Map.Of(("authorNames", List.Of("Alan Moore", "Dave Gibbons")),
                ("isbn", "978-1779501127"),
                ("title", "Watchmen"))));
    }
    [Fact]
    public void Should_Search_Library_Books_By_Title_Json()
    {
        var result = LibraryOperations.SearchBooksByTitleJson(LibraryOperations.LibraryData, "Watchmen");

        result.Should().Be(
            """[{"title":"Watchmen","isbn":"978-1779501127","authorNames":["Alan Moore","Dave Gibbons"]}]""");
    }
    [Fact]
    public void Should_Search_On_Combined_Criteria()
    {
        // An empty query matches everything.
        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of()))
            .Should().BeEquivalentTo("Watchmen", "7 Habits of Highly Effective People");

        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of(("author", "Moore"))))
            .Should().Equal("Watchmen");

        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of(("publishedAfter", 2000))))
            .Should().Equal("7 Habits of Highly Effective People");

        // Criteria combine with AND.
        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of(("title", "Habits"), ("publishedBefore", 2000))))
            .Should().BeEmpty();

        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of(("title", "Habits"), ("publishedAfter", 2000))))
            .Should().Equal("7 Habits of Highly Effective People");

        // Bounds are inclusive.
        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of(("publishedAfter", 1987), ("publishedBefore", 1987))))
            .Should().Equal("Watchmen");
    }
    [Fact]
    public void Should_Search_Case_Insensitively()
    {
        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of(("title", "watchMEN"))))
            .Should().Equal("Watchmen");

        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of(("author", "moore"))))
            .Should().Equal("Watchmen");

        TitlesOf(Catalog.SearchBooksByTitle(CatalogFixture, "WATCH"))
            .Should().Equal("Watchmen");
    }
    [Fact]
    public void Should_Reject_A_Search_Criterion_The_Schema_Does_Not_Name()
    {
        var search = () => Catalog.SearchBook(CatalogFixture, Map.Of(("publisher", "DC")));

        search.Should().Throw<SchemaViolationException>()
            .Which.Errors.Single().ToString().Should().Be("publisher: is not a permitted property");
    }
    [Fact]
    public void Should_Validate_A_Request_At_The_Boundary()
    {
        var request = Map.Of(("title", "Watchmen"), ("fields", List.Of("title", "isbn")));

        LibraryOperations.SearchBooksJson(LibraryOperations.LibraryData, request)
            .Should().Be("""[{"title":"Watchmen","isbn":"978-1779501127"}]""");
    }
    [Fact]
    public void Should_Reject_A_Malformed_Request_At_The_Boundary()
    {
        var badRequest = Map.Of(("title", ""), ("fields", List.Of("title", "publisher")));

        var search = () => LibraryOperations.SearchBooksJson(LibraryOperations.LibraryData, badRequest);

        search.Should().Throw<SchemaViolationException>()
            .Which.Errors.Select(e => e.ToString()).Should().BeEquivalentTo(
                "title: must be at least 1 characters, but was 0",
                "fields.[1]: must be one of [\"title\",\"isbn\",\"authorNames\"], but was \"publisher\"");
    }
}
