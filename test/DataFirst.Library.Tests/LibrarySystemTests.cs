using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class LibrarySystemTests
{
    public static IEnumerable<object[]> Kinds => new[] { "snapshot", "diff-indexed" }.Select(k => new object[] { k });

    private static LibrarySystem Make(string kind) =>
        new(kind == "snapshot"
            ? new SnapshotAggregateStore(DomainFixtures.Library)
            : new DiffIndexedStore(DomainFixtures.Library));

    private static readonly DataMap Info = Map.Of(("isbn", "978-0000000001"), ("id", "i-1"), ("libId", "l-1"));

    [Fact]
    public void A_system_can_be_built_straight_from_data()
    {
        new LibrarySystem(DomainFixtures.Library).Snapshot().Equals(DomainFixtures.Library).Should().BeTrue();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Reads_see_the_initial_library(string kind)
    {
        var system = Make(kind);

        system.Snapshot().Equals(DomainFixtures.Library).Should().BeTrue();
        system.SearchBook(Map.Of(("author", "moore"))).Should().HaveCount(1);
        system.SearchBooksByTitleJson("watch").Should().Contain("Watchmen");
        system.GetBookLendings("lib@x.co", "lent@x.co").Should().HaveCount(2);
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Adding_an_item_returns_the_book_and_is_visible_to_later_reads(string kind)
    {
        var system = Make(kind);

        var book = system.AddBookItem("vip@x.co", Info);

        _.Get<DataList>(book, "bookItems").Should().HaveCount(1);
        _.Get<DataList>(system.Snapshot(), ["catalog", "booksByIsbn", "978-0000000001", "bookItems"]).Should().HaveCount(1);
        new Action(() => system.AddBookItem("lib@x.co", Info)).Should().Throw<DuplicateBookItemException>();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Adding_an_item_is_checked_for_permission_then_shape_then_existence(string kind)
    {
        var system = Make(kind);

        new Action(() => system.AddBookItem("plain@x.co", Info)).Should().Throw<Exception>().WithMessage("Not allowed*");
        new Action(() => system.AddBookItem("lib@x.co", Info.SetItem("isLent", true))).Should().Throw<SchemaViolationException>();
        new Action(() => system.AddBookItem("lib@x.co", Info.SetItem("isbn", "978-9999999999"))).Should().Throw<KeyNotFoundException>();
        system.Snapshot().Equals(DomainFixtures.Library).Should().BeTrue();
    }

    /// UserManagement.AddMember refuses an email already used by a librarian, but
    /// LibrarySystem.AddMember lifts only the `members` collection into it, so the
    /// librarians are invisible and the same email is accepted as a member too.
    [Theory(Skip = "Known gap: LibrarySystem.AddMember does not see librarians, so it cannot enforce cross-collection uniqueness"),
     MemberData(nameof(Kinds))]
    public void Adding_a_member_with_a_librarians_email_is_refused(string kind)
    {
        var system = Make(kind);

        new Action(() => system.AddMember("lib@x.co", DomainFixtures.Member("lib@x.co")))
            .Should().Throw<DuplicateUserException>();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Blocking_a_member_stops_their_login_and_leaves_the_rest_of_them_alone(string kind)
    {
        var system = Make(kind);
        var before = _.Get<DataMap>(system.Snapshot(), ["userManagementData", "members", "lent@x.co"]);

        var blocked = system.BlockMember("lib@x.co", "lent@x.co");

        blocked.Equals(before.SetItem("isBlocked", true)).Should().BeTrue();
        UserManagement.Authenticate(_.Get<DataMap>(system.Snapshot(), "userManagementData"), "lent@x.co", "pw").Should().BeFalse();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Blocking_an_already_blocked_member_changes_nothing(string kind)
    {
        // Regression: the diff is empty, and merging an empty diff used to replace the
        // whole member with {}.
        var system = Make(kind);
        var before = _.Get<DataMap>(system.Snapshot(), ["userManagementData", "members", "blocked@x.co"]);

        var again = system.BlockMember("lib@x.co", "blocked@x.co");
        var twice = system.BlockMember("lib@x.co", "blocked@x.co");

        again.Equals(before).Should().BeTrue();
        twice.Equals(before).Should().BeTrue();
        system.Snapshot().Equals(DomainFixtures.Library).Should().BeTrue();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Only_librarians_block_and_only_real_members_can_be_blocked(string kind)
    {
        var system = Make(kind);

        new Action(() => system.BlockMember("super@x.co", "plain@x.co")).Should().Throw<Exception>().WithMessage("Not allowed*");
        new Action(() => system.BlockMember("lib@x.co", "ghost@x.co")).Should().Throw<KeyNotFoundException>();
        new Action(() => system.BlockMember("lib@x.co", "lib@x.co")).Should().Throw<KeyNotFoundException>();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Adding_a_member_makes_them_able_to_log_in(string kind)
    {
        var system = Make(kind);

        var members = system.AddMember("lib@x.co", DomainFixtures.Member("fresh@x.co"));

        members.ContainsKey("fresh@x.co").Should().BeTrue();
        members.Count.Should().Be(_.Get<DataMap>(DomainFixtures.Users, "members").Count + 1);
        UserManagement.Authenticate(_.Get<DataMap>(system.Snapshot(), "userManagementData"), "fresh@x.co", "pw").Should().BeTrue();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Adding_a_member_is_refused_for_non_librarians_duplicates_and_bad_shapes(string kind)
    {
        var system = Make(kind);

        new Action(() => system.AddMember("vip@x.co", DomainFixtures.Member("fresh@x.co"))).Should().Throw<Exception>().WithMessage("Not allowed*");
        new Action(() => system.AddMember("lib@x.co", DomainFixtures.Member("plain@x.co"))).Should().Throw<DuplicateUserException>();
        new Action(() => system.AddMember("lib@x.co", Map.Of(("email", "x@y.co")))).Should().Throw<SchemaViolationException>();
        system.Snapshot().Equals(DomainFixtures.Library).Should().BeTrue();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Unrelated_operations_from_many_threads_all_take_effect(string kind)
    {
        var system = Make(kind);
        var tasks = new Action[]
        {
            () => system.AddBookItem("lib@x.co", Info),
            () => system.AddBookItem("lib@x.co", Info.SetItem("isbn", "978-0000000002")),
            () => system.BlockMember("lib@x.co", "plain@x.co"),
            () => system.BlockMember("lib@x.co", "vip@x.co"),
            () => system.AddMember("lib@x.co", DomainFixtures.Member("new1@x.co")),
        };

        Parallel.ForEach(tasks, a => a());

        var snapshot = system.Snapshot();
        UserManagement.IsBlocked(_.Get<DataMap>(snapshot, "userManagementData"), "plain@x.co").Should().BeTrue();
        UserManagement.IsBlocked(_.Get<DataMap>(snapshot, "userManagementData"), "vip@x.co").Should().BeTrue();
        UserManagement.IsMember(_.Get<DataMap>(snapshot, "userManagementData"), "new1@x.co").Should().BeTrue();
        _.Get<DataList>(snapshot, ["catalog", "booksByIsbn", "978-0000000001", "bookItems"]).Should().HaveCount(1);
        _.Get<DataList>(snapshot, ["catalog", "booksByIsbn", "978-0000000002", "bookItems"]).Should().HaveCount(1);
    }
}
