using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class LibrarySeedValidationTests
{

    private static bool Valid(DataMap schema, DataValue data) => Validation.Validate(schema, data).IsValid();

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
}
