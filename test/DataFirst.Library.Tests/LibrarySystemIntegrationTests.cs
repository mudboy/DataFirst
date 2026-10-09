using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class LibrarySystemIntegrationTests
{
    private static IEnumerable<string> TitlesOf(DataList results) =>
        results.Select(r => _.Get<string>(r, "title"));
    [Fact]
    public void Should_Keep_Reads_And_Writes_Consistent_Through_The_System_Layer()
    {
        var system = new LibrarySystem(LibraryOperations.LibraryData);

        TitlesOf(system.GetBookLendings("franck@gmail.com", "samantha@gmail.com"))
            .Should().Equal("Watchmen");

        system.BlockMember("franck@gmail.com", "samantha@gmail.com");

        var users = _.Get<DataMap>(system.Snapshot(), "userManagementData");
        UserManagement.IsBlocked(users, "samantha@gmail.com").Should().BeTrue();

        // Permission checks still apply at the system layer.
        var notALibrarian = () => system.BlockMember("vip@gmail.com", "samantha@gmail.com");
        notALibrarian.Should().Throw<Exception>().WithMessage("Not allowed to block members");
    }
    [Fact]
    public void Should_Add_A_Member_Through_The_System_Layer()
    {
        var system = new LibrarySystem(LibraryOperations.LibraryData);

        system.AddMember("franck@gmail.com", Map.Of(
            ("email", "new@gmail.com"),
            ("password", Passwords.Hash("new-secret", 1000))));

        var users = _.Get<DataMap>(system.Snapshot(), "userManagementData");
        UserManagement.IsMember(users, "new@gmail.com").Should().BeTrue();

        // The seed value is untouched: the store holds the new version, not the old one.
        UserManagement.IsMember(
            _.Get<DataMap>(LibraryOperations.LibraryData, "userManagementData"), "new@gmail.com")
            .Should().BeFalse();
    }
    [Fact]
    public void Should_Survive_Parallel_Writes_To_Different_Books()
    {
        const int books = 12;

        var seed = Enumerable.Range(0, books).Aggregate(
            LibraryOperations.LibraryData,
            (data, i) => _.Set(data, ["catalog", "booksByIsbn", $"978-000000000{i}"], Map.Of(
                ("isbn", $"978-000000000{i}"),
                ("title", $"Book {i}"),
                ("authorIds", List.Of("alan-moore")))));

        var system = new LibrarySystem(seed);

        Parallel.For(0, books, i =>
            system.AddBookItem("franck@gmail.com", Map.Of(
                ("isbn", $"978-000000000{i}"),
                ("id", $"item-{i}"),
                ("libId", "brooklyn-lib"))));

        var final = system.Snapshot();
        for (var i = 0; i < books; i++)
        {
            var items = _.Get<DataList>(final,
                ["catalog", "booksByIsbn", $"978-000000000{i}", "bookItems"]);
            items.Select(item => _.Get<string>(item, "id")).Should().Equal($"item-{i}");
        }
    }
}
