using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class LibrarySchemaExampleTests
{
    /// Asserts through DataMap/DataList's own structural equality. Both implement
    /// IEnumerable, so a plain Should().Be() would route to AwesomeAssertions'
    /// collection assertions and walk members instead. Failure messages print the
    /// values as JSON.
    private static void ShouldEqual(object actual, object expected) =>
        actual.Equals(expected).Should().BeTrue($"of\n  expected: {expected}\n  actual:   {actual}");

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
}
