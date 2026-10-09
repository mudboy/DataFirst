using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;
using DataFirst.Library;

namespace DataFirst.Library.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class LibrarySchemaTests
{
    private static DataMap Users => _.Get<DataMap>(LibraryOperations.LibraryData, "userManagementData");
    private static void ShouldNotEqual(object actual, object expected) =>
        actual.Should().NotBe(expected);
    /// Asserts through DataMap/DataList's own structural equality. Both implement
    /// IEnumerable, so a plain Should().Be() would route to AwesomeAssertions'
    /// collection assertions and walk members instead. Failure messages print the
    /// values as JSON.
    private static void ShouldEqual(object actual, object expected) =>
        actual.Equals(expected).Should().BeTrue($"of\n  expected: {expected}\n  actual:   {actual}");

    private static bool Valid(DataMap schema, DataValue data) => Validation.Validate(schema, data).IsValid();
    private static readonly DataMap GoodBook = Map.Of(
        ("isbn", "978-1779501127"), ("title", "Watchmen"), ("publicationYear", 1987),
        ("authorIds", List.Of("alan-moore")));

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

    [Fact]
    public void Should_Report_Every_Error_With_Its_Path()
    {
        var book = Map.Of(
            ("isbn", "nope"),
            ("title", ""),
            ("publicationYear", 3000),
            ("authorIds", List.Of()));

        var errors = Validation.Validate(Schemas.Book, book).Errors()
            .Select(e => e.ToString()).ToList();

        // Every problem, not just the first.
        errors.Should().HaveCount(4);
        errors.Should().Contain(e => e.StartsWith("isbn:") && e.Contains("must match"));
        errors.Should().Contain(e => e.StartsWith("title:") && e.Contains("at least 1 characters"));
        errors.Should().Contain(e => e.StartsWith("publicationYear:") && e.Contains("at most 2100"));
        errors.Should().Contain(e => e.StartsWith("authorIds:") && e.Contains("at least 1 items"));
    }

    [Fact]
    public void Should_Report_A_Missing_Required_Field()
    {
        var errors = Validation.Validate(Schemas.Book, Map.Of(("title", "Watchmen"))).Errors();

        errors.Select(e => e.ToString()).Should().BeEquivalentTo(
            "isbn: is required but missing",
            "authorIds: is required but missing");
    }

    [Fact]
    public void Should_Path_Errors_Through_Nested_Structures()
    {
        var book = Map.Of(
            ("isbn", "978-1779501127"),
            ("title", "Watchmen"),
            ("authorIds", List.Of("alan-moore")),
            ("bookItems", List.Of(
                Map.Of(("id", "book-item-1"), ("libId", "nyc"), ("isLent", false)),
                Map.Of(("id", "book-item-2"), ("libId", "nyc"), ("isLent", "no")))));

        Validation.Validate(Schemas.Book, book).Errors().Single().ToString()
            .Should().Be("bookItems.[1].isLent: expected boolean, but found string");
    }

    [Fact]
    public void Should_Path_Errors_Through_Id_Keyed_Collections()
    {
        var catalog = Map.Of(
            ("booksByIsbn", Map.Of(
                ("978-1779501127", Map.Of(
                    ("isbn", "978-1779501127"),
                    ("title", "Watchmen"),
                    ("authorIds", List.Of("alan-moore", "alan-moore")))))),
            ("authorsById", Map.Of()));

        Schemas.ValidateCatalog(catalog).Errors().Single().ToString()
            .Should().Be("booksByIsbn.978-1779501127.authorIds: must not contain duplicates");
    }

    [Fact]
    public void Should_Reject_Properties_The_Schema_Does_Not_Name()
    {
        var author = Map.Of(
            ("name", "Alan Moore"),
            ("bookIsbns", List.Of("978-1779501127")),
            ("favouriteColour", "black"));

        Schemas.ValidateCatalog(Map.Of(
                ("booksByIsbn", Map.Of()),
                ("authorsById", Map.Of(("alan-moore", author)))))
            .Errors().Single().ToString()
            .Should().Be("authorsById.alan-moore.favouriteColour: is not a permitted property");
    }

    [Fact]
    public void Should_Support_Unions_Of_Types_And_Schemas()
    {
        var nullableIsbn = Map.Of(("type", List.Of("string", "null")));

        Validation.Validate(nullableIsbn, "978-1779501127").IsValid().Should().BeTrue();
        Validation.Validate(nullableIsbn, DataNull.Instance).IsValid().Should().BeTrue();
        Validation.Validate(nullableIsbn, 1987).IsValid().Should().BeFalse();

        var stringOrCount = Map.Of(("anyOf", List.Of(
            Map.Of(("type", "string")),
            Map.Of(("type", "integer"), ("minimum", 0)))));

        Validation.Validate(stringOrCount, "x").IsValid().Should().BeTrue();
        Validation.Validate(stringOrCount, 3).IsValid().Should().BeTrue();
        Validation.Validate(stringOrCount, -1).IsValid().Should().BeFalse();
    }

    [Fact]
    public void Should_Treat_A_Schema_As_Data()
    {
        // The point of principle 4: a schema is a value. It can be built from parts,
        // diffed, and changed without touching a type.
        var strict = _.Set(Schemas.Book, ["properties", "title", "minLength"], 5).As<DataMap>();

        Validation.Validate(Schemas.Book, Map.Of(
            ("isbn", "978-1779501127"), ("title", "Wat"), ("authorIds", List.Of("a")))).IsValid()
            .Should().BeTrue();

        Validation.Validate(strict, Map.Of(
            ("isbn", "978-1779501127"), ("title", "Wat"), ("authorIds", List.Of("a")))).IsValid()
            .Should().BeFalse();

        ShouldEqual(
            _.DiffObjects(Schemas.Book, strict),
            Map.Of(("properties", Map.Of(("title", Map.Of(("minLength", 5)))))));
    }

    [Theory]
    [InlineData("a@b.co", true)]
    [InlineData("first.last@example.org", true)]
    [InlineData("no-at-sign.com", false)]
    [InlineData("two@@example.com", false)]
    [InlineData("spaces in@example.com", false)]
    [InlineData("missing@tld", false)]
    [InlineData("", false)]
    public void An_email_needs_a_local_part_a_domain_and_a_dot(string email, bool expected)
    {
        var member = Map.Of(("email", email), ("password", Passwords.Hash("pw", 1)));
        Valid(Schemas.Member, member).Should().Be(expected);
        Valid(Schemas.Librarian, member).Should().Be(expected);
    }

    [Fact]
    public void The_seeded_library_satisfies_every_schema_that_describes_it()
    {
        Valid(Schemas.LibraryData, LibraryOperations.LibraryData).Should().BeTrue();
        Schemas.ValidateCatalog(_.Get<DataMap>(LibraryOperations.LibraryData, "catalog")).IsValid().Should().BeTrue();
        Schemas.ValidateUserManagement(_.Get<DataMap>(LibraryOperations.LibraryData, "userManagementData")).IsValid().Should().BeTrue();
    }
    [Fact]
    public void Should_Not_Modify_Original_With_Set()
    {
        var oldData = LibraryOperations.LibraryData;
        var newData = _.Set(oldData, 
            ["catalog", "booksByIsbn", "978-1779501127", "publicationYear"], 1986);

        ShouldNotEqual(newData, oldData);
    }
    [Fact]
    public void Should_Accept_The_Real_Library_Data()
    {
        Validation.Validate(Schemas.LibraryData, LibraryOperations.LibraryData).Errors()
            .Should().BeEmpty();

        Schemas.ValidateCatalog(_.Get<DataMap>(LibraryOperations.LibraryData, "catalog")).Errors()
            .Should().BeEmpty();
    }
    [Fact]
    public void Should_Accept_The_Seeded_User_Management_Data()
    {
        Schemas.ValidateUserManagement(Users).Errors().Should().BeEmpty();
    }
    [Fact]
    public void Should_Read_At_Paths()
    {
        var library = LibraryOperations.LibraryData;

        var picked = _.At(library, [
            DataPath.Of("catalog", "booksByIsbn", "978-1779501127", "title"),
            DataPath.Of("userManagementData", "members", "samantha@gmail.com", "email"),
            DataPath.Of("catalog", "nothing", "here")]);

        picked.ShouldEqual(List.Of("Watchmen", "samantha@gmail.com", DataNull.Instance));
    }
}
