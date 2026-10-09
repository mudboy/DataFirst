using DataFirst.Testing;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class LibrarySchemaTests
{
    private static readonly DataMap GoodBook = Map.Of(
        ("isbn", "978-1779501127"), ("title", "Watchmen"), ("publicationYear", 1987),
        ("authorIds", List.Of("alan-moore")));

    private static bool Valid(DataMap schema, DataValue data) => Validation.Validate(schema, data).IsValid();

    [Property]
    public bool No_data_can_make_validation_throw(DataValue data) =>
        new[] { Schemas.Book, Schemas.Author, Schemas.Member, Schemas.Librarian, Schemas.BookItem,
                Schemas.BookItemInfo, Schemas.BookLending, Schemas.SearchQuery, Schemas.SearchRequest,
                Schemas.CatalogData, Schemas.LibraryData, Schemas.UserManagementData }
            .All(schema => { Validation.Validate(schema, data); return true; });

    [Property]
    public bool A_year_is_valid_exactly_between_1400_and_2100(int year) =>
        Valid(Schemas.Book, GoodBook.SetItem("publicationYear", (long)year)) == (year >= 1400 && year <= 2100);

    [Property]
    public bool A_book_needs_at_least_one_author_and_no_duplicates(NonNegativeInt count, bool duplicate)
    {
        var ids = Enumerable.Range(0, count.Get % 4).Select(i => (DataValue)$"author-{i}").ToList();
        if (duplicate && ids.Count > 0) ids.Add(ids[0]);

        return Valid(Schemas.Book, GoodBook.SetItem("authorIds", DataList.Create(ids)))
               == (ids.Count > 0 && !duplicate);
    }

    [Theory]
    [InlineData("978-1779501127", true)]
    [InlineData("1779501127", true)]
    [InlineData("978-0-306-40615-7", true)]
    [InlineData("12345", false)]
    [InlineData("978 1779501127", false)]
    [InlineData("978-1779501127-9999", false)]
    [InlineData("97817795011279999", true)]
    [InlineData("abcdefghij", false)]
    [InlineData("", false)]
    public void An_isbn_is_ten_to_seventeen_digits_and_hyphens(string isbn, bool expected)
    {
        Valid(Schemas.Book, GoodBook.SetItem("isbn", isbn)).Should().Be(expected);
    }

    [Theory]
    [InlineData("2020-04-23", true)]
    [InlineData("2020-4-23", false)]
    [InlineData("23/04/2020", false)]
    [InlineData("2020-04-23T10:00", false)]
    public void A_lending_date_is_iso_formatted(string date, bool expected)
    {
        Valid(Schemas.BookLending, Map.Of(("bookItemId", "i"), ("bookIsbn", "978-1779501127"), ("lendingDate", date)))
            .Should().Be(expected);
    }

    [Fact]
    public void A_new_item_may_not_say_whether_it_is_lent()
    {
        var info = Map.Of(("isbn", "978-1779501127"), ("id", "i"), ("libId", "l"));
        Valid(Schemas.BookItemInfo, info).Should().BeTrue();
        Valid(Schemas.BookItemInfo, info.SetItem("isLent", true)).Should().BeFalse();
        Valid(Schemas.BookItemInfo, Map.Of(("isbn", "978-1779501127"), ("id", "i"))).Should().BeFalse();
        Valid(Schemas.BookItemInfo, info.SetItem("id", "")).Should().BeFalse();
    }

    [Fact]
    public void A_stored_book_item_requires_all_three_fields()
    {
        Valid(Schemas.BookItem, Map.Of(("id", "i"), ("libId", "l"), ("isLent", false))).Should().BeTrue();
        Valid(Schemas.BookItem, Map.Of(("id", "i"), ("libId", "l"))).Should().BeFalse();
        Valid(Schemas.BookItem, Map.Of(("id", "i"), ("libId", "l"), ("isLent", "no"))).Should().BeFalse();
    }

    [Fact]
    public void Search_bounds_are_limited_to_plausible_years_and_every_criterion_is_optional()
    {
        Valid(Schemas.SearchQuery, DataMap.Empty).Should().BeTrue();
        Valid(Schemas.SearchQuery, Map.Of(("publishedAfter", 1399))).Should().BeFalse();
        Valid(Schemas.SearchQuery, Map.Of(("publishedBefore", 2101))).Should().BeFalse();
        Valid(Schemas.SearchQuery, Map.Of(("title", ""))).Should().BeFalse();
        Valid(Schemas.SearchQuery, Map.Of(("title", new string('x', 201)))).Should().BeFalse();
        Valid(Schemas.SearchQuery, Map.Of(("title", new string('x', 200)))).Should().BeTrue();
        Valid(Schemas.SearchQuery, Map.Of(("colour", "red"))).Should().BeFalse();
    }

    [Fact]
    public void A_search_request_limits_its_fields_to_the_known_projections()
    {
        Valid(Schemas.SearchRequest, Map.Of(("title", "w"), ("fields", List.Of("title", "isbn")))).Should().BeTrue();
        Valid(Schemas.SearchRequest, Map.Of(("title", "w"), ("fields", List.Of("secret")))).Should().BeFalse();
        Valid(Schemas.SearchRequest, Map.Of(("title", "w"), ("fields", DataList.Empty))).Should().BeFalse();
        Valid(Schemas.SearchRequest, Map.Of(("title", "w"), ("fields", List.Of("title", "title")))).Should().BeFalse();
        Valid(Schemas.SearchRequest, Map.Of(("fields", List.Of("title")))).Should().BeFalse();
    }

    [Fact]
    public void Catalog_validation_walks_id_keyed_collections_and_reports_where_each_error_is()
    {
        var catalog = Map.Of(
            ("booksByIsbn", Map.Of(("978-1779501127", GoodBook.SetItem("title", "")))),
            ("authorsById", Map.Of(("alan-moore", Map.Of(("name", "Alan Moore"))))));

        var errors = Schemas.ValidateCatalog(catalog).Errors().Select(e => e.Path.ToString()).ToList();

        errors.Should().BeEquivalentTo(
            "booksByIsbn.978-1779501127.title",
            "authorsById.alan-moore.bookIsbns");
    }

    [Fact]
    public void Catalog_validation_stops_at_a_malformed_envelope_without_walking_entries()
    {
        var errors = Schemas.ValidateCatalog(Map.Of(("booksByIsbn", Map.Of()))).Errors();
        errors.Should().ContainSingle().Which.Path.ToString().Should().Be("authorsById");
    }

    [Fact]
    public void User_validation_walks_both_collections_and_tolerates_a_missing_one()
    {
        Schemas.ValidateUserManagement(DataMap.Empty).IsValid().Should().BeTrue();
        Schemas.ValidateUserManagement(Map.Of(("members", Map.Of(("x@y.co", Map.Of(("email", "x@y.co")))))))
            .Errors().Select(e => e.Path.ToString()).Should().Equal("members.x@y.co.password");
        Schemas.ValidateUserManagement(Map.Of(("librarians", Map.Of(("bad", Map.Of(("email", "nope")))))))
            .Errors().Select(e => e.Path.ToString()).Should().Contain("librarians.bad.email");
    }
}
