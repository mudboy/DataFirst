using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class LibrarySeedTests
{
    private static void ShouldNotEqual(object actual, object expected) =>
        actual.Should().NotBe(expected);
    private static DataMap Users => _.Get<DataMap>(LibraryOperations.LibraryData, "userManagementData");
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
