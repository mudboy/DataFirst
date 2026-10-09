using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class UserManagementTests
{
    private static readonly DataMap Users = DomainFixtures.Users;

    [Fact]
    public void Roles_are_distinct_collections()
    {
        UserManagement.IsLibrarian(Users, "lib@x.co").Should().BeTrue();
        UserManagement.IsMember(Users, "lib@x.co").Should().BeFalse();
        UserManagement.IsMember(Users, "plain@x.co").Should().BeTrue();
        UserManagement.IsLibrarian(Users, "plain@x.co").Should().BeFalse();
        UserManagement.IsMember(Users, "nobody@x.co").Should().BeFalse();
        UserManagement.IsLibrarian(Users, "nobody@x.co").Should().BeFalse();
    }

    [Fact]
    public void Flags_default_to_false_and_never_apply_to_librarians_or_strangers()
    {
        UserManagement.IsVipMember(Users, "vip@x.co").Should().BeTrue();
        UserManagement.IsVipMember(Users, "plain@x.co").Should().BeFalse();
        UserManagement.IsSuperMember(Users, "super@x.co").Should().BeTrue();
        UserManagement.IsSuperMember(Users, "vip@x.co").Should().BeFalse();
        UserManagement.IsBlocked(Users, "blocked@x.co").Should().BeTrue();
        UserManagement.IsBlocked(Users, "plain@x.co").Should().BeFalse();
        UserManagement.IsVipMember(Users, "lib@x.co").Should().BeFalse();
        UserManagement.IsBlocked(Users, "nobody@x.co").Should().BeFalse();
    }

    [Fact]
    public void A_flag_set_to_something_other_than_true_is_not_set()
    {
        var odd = _.Set(Users, ["members", "plain@x.co", "isVip"], "yes");
        UserManagement.IsVipMember(odd, "plain@x.co").Should().BeFalse();
    }

    [Fact]
    public void Authentication_needs_a_known_unblocked_user_and_the_right_password()
    {
        UserManagement.Authenticate(Users, "plain@x.co", "pw").Should().BeTrue();
        UserManagement.Authenticate(Users, "lib@x.co", "pw").Should().BeTrue();
        UserManagement.Authenticate(Users, "plain@x.co", "wrong").Should().BeFalse();
        UserManagement.Authenticate(Users, "nobody@x.co", "pw").Should().BeFalse();
        UserManagement.Authenticate(Users, "blocked@x.co", "pw").Should().BeFalse();
        UserManagement.Authenticate(Users, "plain@x.co", "").Should().BeFalse();
    }

    [Fact]
    public void A_user_with_no_password_record_cannot_log_in()
    {
        var stripped = _.Set(Users, ["members", "plain@x.co"], Map.Of(("email", "plain@x.co")));
        UserManagement.Authenticate(stripped, "plain@x.co", "pw").Should().BeFalse();
    }

    [Fact]
    public void Lendings_are_listed_in_order_and_default_to_empty()
    {
        UserManagement.BookLendings(Users, "lent@x.co").Select(l => _.Get<string>(l, "bookItemId"))
            .Should().Equal("item-1", "item-2");
        UserManagement.BookLendings(Users, "plain@x.co").Should().BeEmpty();
    }

    [Fact]
    public void Lendings_of_a_stranger_or_a_librarian_are_an_error_not_an_empty_list()
    {
        new Action(() => UserManagement.BookLendings(Users, "nobody@x.co")).Should().Throw<KeyNotFoundException>();
        new Action(() => UserManagement.BookLendings(Users, "lib@x.co")).Should().Throw<KeyNotFoundException>();
    }

    [Property]
    public Property Blocking_then_unblocking_leaves_only_the_flag_changed() =>
        Prop.ForAll(Gen.Elements("plain@x.co", "vip@x.co", "super@x.co", "lent@x.co").ToArbitrary(), id =>
        {
            var blocked = UserManagement.BlockMember(Users, id);
            var unblocked = UserManagement.UnblockMember(blocked, id);

            return UserManagement.IsBlocked(blocked, id)
                   && !UserManagement.IsBlocked(unblocked, id)
                   && _.Set(unblocked, ["members", id], _.Get<DataMap>(unblocked, ["members", id]).Remove("isBlocked"))
                       .Equals(_.Set(Users, ["members", id], _.Get<DataMap>(Users, ["members", id]).Remove("isBlocked")))
                   && Users.Equals(DomainFixtures.Users);
        });

    [Property]
    public Property Blocking_is_idempotent() =>
        Prop.ForAll(Gen.Elements("plain@x.co", "blocked@x.co").ToArbitrary(), id =>
        {
            var once = UserManagement.BlockMember(Users, id);
            return UserManagement.BlockMember(once, id).Equals(once);
        });

    [Fact]
    public void Blocking_stops_a_member_logging_in_and_unblocking_restores_it()
    {
        var blocked = UserManagement.BlockMember(Users, "plain@x.co");
        UserManagement.Authenticate(blocked, "plain@x.co", "pw").Should().BeFalse();
        UserManagement.Authenticate(UserManagement.UnblockMember(blocked, "plain@x.co"), "plain@x.co", "pw").Should().BeTrue();
    }

    [Fact]
    public void Only_members_can_be_blocked()
    {
        new Action(() => UserManagement.BlockMember(Users, "lib@x.co")).Should().Throw<KeyNotFoundException>();
        new Action(() => UserManagement.UnblockMember(Users, "nobody@x.co")).Should().Throw<KeyNotFoundException>();
    }

    [Property]
    public Property An_added_member_exists_and_nothing_else_changes() =>
        Prop.ForAll(Gen.Elements("new@x.co", "other.person@example.org", "a@b.cd").ToArbitrary(), email =>
        {
            var added = UserManagement.AddMember(Users, DomainFixtures.Member(email));

            return UserManagement.IsMember(added, email)
                   && !UserManagement.IsMember(Users, email)
                   && _.Get<DataMap>(added, "members").Count == _.Get<DataMap>(Users, "members").Count + 1
                   && _.Get<DataMap>(added, "librarians").Equals(_.Get<DataMap>(Users, "librarians"))
                   && UserManagement.Authenticate(added, email, "pw");
        });

    [Fact]
    public void Adding_a_librarian_files_them_under_librarians()
    {
        var added = UserManagement.AddLibrarian(Users, DomainFixtures.Librarian("newlib@x.co"));

        UserManagement.IsLibrarian(added, "newlib@x.co").Should().BeTrue();
        UserManagement.IsMember(added, "newlib@x.co").Should().BeFalse();
        UserManagement.Authenticate(added, "newlib@x.co", "pw").Should().BeTrue();
    }

    [Theory]
    [InlineData("plain@x.co")]
    [InlineData("lib@x.co")]
    public void An_email_is_unique_across_members_and_librarians(string email)
    {
        new Action(() => UserManagement.AddMember(Users, DomainFixtures.Member(email)))
            .Should().Throw<DuplicateUserException>().WithMessage($"*{email}*");
        new Action(() => UserManagement.AddLibrarian(Users, DomainFixtures.Librarian(email)))
            .Should().Throw<DuplicateUserException>();
    }

    [Fact]
    public void A_user_that_breaks_the_schema_is_rejected_before_the_duplicate_check()
    {
        new Action(() => UserManagement.AddMember(Users, Map.Of(("email", "plain@x.co"))))
            .Should().Throw<SchemaViolationException>();
        new Action(() => UserManagement.AddLibrarian(Users, Map.Of(("email", "not-an-email"), ("password", Passwords.Hash("x", 1)))))
            .Should().Throw<SchemaViolationException>();
        new Action(() => UserManagement.AddMember(Users, DomainFixtures.Member("x@y.co").SetItem("isVip", "yes")))
            .Should().Throw<SchemaViolationException>();
    }

    [Fact]
    public void Adding_a_user_to_empty_user_data_creates_the_collection()
    {
        var added = UserManagement.AddMember(DataMap.Empty, DomainFixtures.Member("first@x.co"));
        UserManagement.IsMember(added, "first@x.co").Should().BeTrue();
    }
}
