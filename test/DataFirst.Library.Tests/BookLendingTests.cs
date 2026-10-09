using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class BookLendingTests
{
    private static DataMap Users => _.Get<DataMap>(LibraryOperations.LibraryData, "userManagementData");
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
    public void Should_Read_Book_Lendings()
    {
        (UserManagement.BookLendings(Users, "samantha@gmail.com")).ShouldEqual(List.Of(Map.Of(
                ("bookItemId", "book-item-1"),
                ("bookIsbn", "978-1779501127"),
                ("lendingDate", "2020-04-23"))));

        // A member with no lendings recorded gets an empty list, not an error.
        UserManagement.BookLendings(Users, "vip@gmail.com").Should().BeEmpty();

        var unknown = () => UserManagement.BookLendings(Users, "nobody@gmail.com");
        unknown.Should().Throw<KeyNotFoundException>();
    }
    [Fact]
    public void Should_Refuse_Library_Operations_To_Unauthorised_Users()
    {
        // vip@gmail.com is neither a librarian nor a super member.
        var getLendings = () => LibraryOperations.GetBookLendings(LibraryOperations.LibraryData, "vip@gmail.com", "samantha@gmail.com");
        getLendings.Should().Throw<Exception>().WithMessage("Not allowed to get book lendings");

        // samantha is a super member, but not a VIP.
        var addItem = () => LibraryOperations.AddBookItem(LibraryOperations.LibraryData, "samantha@gmail.com", Map.Of());
        addItem.Should().Throw<Exception>().WithMessage("Not allowed to add book items");

        var unknownUser = () => LibraryOperations.AddBookItem(LibraryOperations.LibraryData, "nobody@gmail.com", Map.Of());
        unknownUser.Should().Throw<Exception>().WithMessage("Not allowed to add book items");
    }
    [Fact]
    public void Should_Describe_A_Members_Lendings()
    {
        var lendings = UserManagement.BookLendings(Users, "samantha@gmail.com");
        var catalog = _.Get<DataMap>(LibraryOperations.LibraryData, "catalog");

        (Catalog.GetBookLendings(catalog, lendings)).ShouldEqual(List.Of(Map.Of(
                ("bookItemId", "book-item-1"),
                ("lendingDate", "2020-04-23"),
                ("title", "Watchmen"),
                ("isbn", "978-1779501127"),
                ("authorNames", List.Of("Alan Moore", "Dave Gibbons")))));
    }
    [Fact]
    public void Should_Reject_A_Lending_For_A_Book_Not_In_The_Catalogue()
    {
        var orphan = List.Of(Map.Of(
            ("bookItemId", "book-item-9"),
            ("bookIsbn", "978-0000000000"),
            ("lendingDate", "2020-04-23")));

        var describe = () => Catalog.GetBookLendings(CatalogFixture, orphan);

        describe.Should().Throw<KeyNotFoundException>()
            .WithMessage("*978-0000000000*");
    }
    [Fact]
    public void Should_Get_Lendings_End_To_End()
    {
        // A librarian may read another member's lendings.
        TitlesOf(LibraryOperations.GetBookLendings(LibraryOperations.LibraryData, "franck@gmail.com", "samantha@gmail.com"))
            .Should().Equal("Watchmen");

        // So may a super member.
        TitlesOf(LibraryOperations.GetBookLendings(LibraryOperations.LibraryData, "samantha@gmail.com", "samantha@gmail.com"))
            .Should().Equal("Watchmen");

        // A member with no lendings gets an empty list.
        LibraryOperations.GetBookLendings(LibraryOperations.LibraryData, "franck@gmail.com", "vip@gmail.com")
            .Should().BeEmpty();
    }
}
