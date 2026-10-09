using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class AggregatePathTests
{
    [Fact]
    public void Aggregates_name_their_place_in_the_system_data()
    {
        Aggregates.Book("b").ToString().Should().Be("catalog.booksByIsbn.b");
        Aggregates.Author("a").ToString().Should().Be("catalog.authorsById.a");
        Aggregates.Member("m@x.co").ToString().Should().Be("userManagementData.members.m@x.co");
        Aggregates.Members.ToString().Should().Be("userManagementData.members");
        Aggregates.UserManagement.ToString().Should().Be("userManagementData");
        Aggregates.Everything.ToString().Should().Be("(root)");
    }

    [Fact]
    public void Containment_is_visible_through_overlap_which_is_what_makes_the_granularity_matter()
    {
        Aggregates.Member("m@x.co").Overlaps(Aggregates.Members).Should().BeTrue();
        Aggregates.Members.Overlaps(Aggregates.UserManagement).Should().BeTrue();
        Aggregates.Everything.Overlaps(Aggregates.Book("b")).Should().BeTrue();

        Aggregates.Book("b1").Overlaps(Aggregates.Book("b2")).Should().BeFalse();
        Aggregates.Book("b").Overlaps(Aggregates.Author("b")).Should().BeFalse();
        Aggregates.Member("m@x.co").Overlaps(Aggregates.Book("b")).Should().BeFalse();
    }

    [Fact]
    public void Every_aggregate_path_resolves_in_the_seeded_library()
    {
        foreach (var path in new[]
                 {
                     Aggregates.Book("978-1779501127"), Aggregates.Author("alan-moore"),
                     Aggregates.Member("samantha@gmail.com"), Aggregates.Members, Aggregates.UserManagement
                 })
            _.ContainsKey(LibraryOperations.LibraryData, path).Should().BeTrue(path.ToString());
    }
}
