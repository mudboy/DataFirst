using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class BookItemTests
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
    [Fact]
    public void Should_Add_A_Book_Item()
    {
        var info = Map.Of(("isbn", "978-1779501127"), ("id", "book-item-3"), ("libId", "brooklyn-lib"));

        var updated = Catalog.AddBookItem(CatalogFixture, info);

        (_.Get<DataList>(updated, ["booksByIsbn", "978-1779501127", "bookItems"])).ShouldEqual(List.Of(Map.Of(("id", "book-item-3"), ("libId", "brooklyn-lib"), ("isLent", false))));

        // A new item is not lent, and isLent cannot be supplied.
        var withIsLent = Map.Of(
            ("isbn", "978-1779501127"), ("id", "book-item-4"), ("libId", "brooklyn-lib"), ("isLent", true));

        var addWithIsLent = () => Catalog.AddBookItem(CatalogFixture, withIsLent);
        addWithIsLent.Should().Throw<SchemaViolationException>()
            .Which.Errors.Single().ToString().Should().Be("isLent: is not a permitted property");

        // The argument is untouched.
        _.ContainsKey(CatalogFixture, ["booksByIsbn", "978-1779501127", "bookItems"]).Should().BeFalse();
    }
    [Fact]
    public void Should_Reject_A_Duplicate_Book_Item()
    {
        var catalog = _.Get<DataMap>(LibraryOperations.LibraryData, "catalog");
        var taken = Map.Of(("isbn", "978-1779501127"), ("id", "book-item-1"), ("libId", "nyc-central-lib"));

        var add = () => Catalog.AddBookItem(catalog, taken);
        add.Should().Throw<DuplicateBookItemException>();
    }
    [Fact]
    public void Should_Reject_A_Book_Item_For_An_Unknown_Book()
    {
        var info = Map.Of(("isbn", "978-0000000000"), ("id", "book-item-3"), ("libId", "brooklyn-lib"));

        var add = () => Catalog.AddBookItem(CatalogFixture, info);
        add.Should().Throw<KeyNotFoundException>().WithMessage("*978-0000000000*");
    }
    [Fact]
    public void Should_Add_A_Book_Item_End_To_End()
    {
        var info = Map.Of(("isbn", "978-1779501127"), ("id", "book-item-3"), ("libId", "brooklyn-lib"));

        var updated = LibraryOperations.AddBookItem(LibraryOperations.LibraryData, "vip@gmail.com", info);

        var items = _.Get<DataList>(updated, ["catalog", "booksByIsbn", "978-1779501127", "bookItems"]);
        items.Select(i => _.Get<string>(i, "id"))
            .Should().Equal("book-item-1", "book-item-2", "book-item-3");

        // The whole library data comes back, so user management survives the change.
        UserManagement.IsLibrarian(_.Get<DataMap>(updated, "userManagementData"), "franck@gmail.com")
            .Should().BeTrue();

        // The only difference is the new item.
        (_.DiffObjects(LibraryOperations.LibraryData, updated)).ShouldEqual(Map.Of(("catalog", Map.Of(("booksByIsbn", Map.Of(("978-1779501127", Map.Of(
                ("bookItems", Map.Of(("2", Map.Of(
                    ("id", "book-item-3"), ("libId", "brooklyn-lib"), ("isLent", false)))))))))))));
    }
}
