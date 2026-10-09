using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class LibraryAuthorisationTests
{
    private static readonly DataMap Library = DomainFixtures.Library;
    private static readonly DataMap Info = Map.Of(("isbn", "978-0000000001"), ("id", "i-1"), ("libId", "l-1"));

    [Theory]
    [InlineData("lib@x.co", true)]
    [InlineData("super@x.co", true)]
    [InlineData("vip@x.co", false)]
    [InlineData("plain@x.co", false)]
    [InlineData("nobody@x.co", false)]
    public void Only_librarians_and_super_members_read_lendings(string user, bool allowed)
    {
        var read = () => LibraryOperations.GetBookLendings(Library, user, "lent@x.co");

        if (allowed) read().Should().HaveCount(2);
        else read.Should().Throw<Exception>().WithMessage("Not allowed*");
    }

    [Theory]
    [InlineData("lib@x.co", true)]
    [InlineData("vip@x.co", true)]
    [InlineData("super@x.co", false)]
    [InlineData("plain@x.co", false)]
    [InlineData("nobody@x.co", false)]
    public void Only_librarians_and_vip_members_add_items(string user, bool allowed)
    {
        var add = () => LibraryOperations.AddBookItem(Library, user, Info);

        if (allowed)
            _.Get<DataList>(add(), ["catalog", "booksByIsbn", "978-0000000001", "bookItems"]).Should().HaveCount(1);
        else
            add.Should().Throw<Exception>().WithMessage("Not allowed*");
    }

    [Fact]
    public void Adding_an_item_returns_the_whole_library_with_the_rest_untouched()
    {
        var updated = LibraryOperations.AddBookItem(Library, "lib@x.co", Info);

        _.Get<DataMap>(updated, "userManagementData").Equals(DomainFixtures.Users).Should().BeTrue();
        _.Get<DataMap>(updated, ["catalog", "authorsById"]).Equals(_.Get<DataMap>(DomainFixtures.Catalog, "authorsById")).Should().BeTrue();
    }

    [Fact]
    public void Searches_run_over_the_catalogue_and_serialise_to_json()
    {
        LibraryOperations.SearchBook(Library, Map.Of(("title", "watch"))).Should().HaveCount(1);
        LibraryOperations.SearchBooksByTitleJson(Library, "dark")
            .Should().Be("""[{"title":"The Dark Knight Returns","isbn":"978-0000000002","authorNames":["Frank Miller"]}]""");
        LibraryOperations.SearchBooksByTitleJson(Library, "zzz").Should().Be("[]");
    }

    [Fact]
    public void A_search_request_can_project_fields_in_the_order_asked()
    {
        LibraryOperations.SearchBooksJson(Library, Map.Of(("title", "watch"), ("fields", List.Of("isbn", "title"))))
            .Should().Be("""[{"isbn":"978-0000000001","title":"Watchmen"}]""");
        LibraryOperations.SearchBooksJson(Library, Map.Of(("title", "watch"), ("fields", List.Of("authorNames"))))
            .Should().Be("""[{"authorNames":["Alan Moore","Dave Gibbons"]}]""");
        LibraryOperations.SearchBooksJson(Library, Map.Of(("title", "watch")))
            .Should().Contain("\"title\":\"Watchmen\"").And.Contain("\"authorNames\"");
    }

    [Fact]
    public void A_malformed_search_request_never_reaches_the_catalogue()
    {
        new Action(() => LibraryOperations.SearchBooksJson(Library, Map.Of(("fields", List.Of("title")))))
            .Should().Throw<SchemaViolationException>();
        new Action(() => LibraryOperations.SearchBooksJson(Library, Map.Of(("title", "w"), ("fields", List.Of("password")))))
            .Should().Throw<SchemaViolationException>();
    }
}
