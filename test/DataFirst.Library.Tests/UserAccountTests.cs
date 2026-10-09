using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class UserAccountTests
{
    private static DataMap Users => _.Get<DataMap>(LibraryOperations.LibraryData, "userManagementData");
    [Fact]
    public void Should_Identify_Roles()
    {
        UserManagement.IsLibrarian(Users, "franck@gmail.com").Should().BeTrue();
        UserManagement.IsLibrarian(Users, "samantha@gmail.com").Should().BeFalse();
        UserManagement.IsLibrarian(Users, "nobody@gmail.com").Should().BeFalse();

        UserManagement.IsMember(Users, "samantha@gmail.com").Should().BeTrue();
        UserManagement.IsMember(Users, "franck@gmail.com").Should().BeFalse();

        UserManagement.IsSuperMember(Users, "samantha@gmail.com").Should().BeTrue();
        UserManagement.IsSuperMember(Users, "vip@gmail.com").Should().BeFalse();

        UserManagement.IsVipMember(Users, "vip@gmail.com").Should().BeTrue();
        UserManagement.IsVipMember(Users, "samantha@gmail.com").Should().BeFalse();
    }
    [Fact]
    public void Should_Treat_An_Absent_Flag_As_False()
    {
        // vip@gmail.com carries no isSuper or isBlocked at all.
        UserManagement.IsSuperMember(Users, "vip@gmail.com").Should().BeFalse();
        UserManagement.IsBlocked(Users, "vip@gmail.com").Should().BeFalse();

        // Neither does a user who does not exist.
        UserManagement.IsVipMember(Users, "nobody@gmail.com").Should().BeFalse();
        UserManagement.IsBlocked(Users, "nobody@gmail.com").Should().BeFalse();
    }
    [Fact]
    public void Should_Authenticate_Against_A_Hashed_Password()
    {
        UserManagement.Authenticate(Users, "samantha@gmail.com", "member-secret").Should().BeTrue();
        UserManagement.Authenticate(Users, "franck@gmail.com", "librarian-secret").Should().BeTrue();

        UserManagement.Authenticate(Users, "samantha@gmail.com", "wrong").Should().BeFalse();
        UserManagement.Authenticate(Users, "nobody@gmail.com", "member-secret").Should().BeFalse();
        UserManagement.Authenticate(Users, "samantha@gmail.com", "").Should().BeFalse();
    }
    [Fact]
    public void Should_Not_Store_The_Password_Itself()
    {
        var stored = _.Get<DataMap>(Users, ["members", "samantha@gmail.com", "password"]);

        DataJson.Serialize(stored).Should().NotContain("member-secret");
        stored.Keys.Should().BeEquivalentTo("salt", "hash", "iterations");

        // Two users with the same password get different hashes, because the salts differ.
        var one = Passwords.Hash("same-password", 1000);
        var two = Passwords.Hash("same-password", 1000);

        _.Get<string>(one, "hash").Should().NotBe(_.Get<string>(two, "hash"));
        Passwords.Verify(one, "same-password").Should().BeTrue();
        Passwords.Verify(two, "same-password").Should().BeTrue();
        Passwords.Verify(one, "different").Should().BeFalse();
    }
    [Fact]
    public void Should_Refuse_A_Blocked_Member()
    {
        var blocked = UserManagement.BlockMember(Users, "samantha@gmail.com");

        UserManagement.IsBlocked(blocked, "samantha@gmail.com").Should().BeTrue();
        UserManagement.Authenticate(blocked, "samantha@gmail.com", "member-secret").Should().BeFalse();

        var unblocked = UserManagement.UnblockMember(blocked, "samantha@gmail.com");
        UserManagement.Authenticate(unblocked, "samantha@gmail.com", "member-secret").Should().BeTrue();

        // The original data is untouched.
        UserManagement.IsBlocked(Users, "samantha@gmail.com").Should().BeFalse();
    }
    [Fact]
    public void Should_Add_A_Member()
    {
        var member = Map.Of(
            ("email", "new@gmail.com"),
            ("password", Passwords.Hash("new-secret", 1000)));

        var updated = UserManagement.AddMember(Users, member);

        UserManagement.IsMember(updated, "new@gmail.com").Should().BeTrue();
        UserManagement.Authenticate(updated, "new@gmail.com", "new-secret").Should().BeTrue();

        // The argument is untouched.
        UserManagement.IsMember(Users, "new@gmail.com").Should().BeFalse();
    }
    [Fact]
    public void Should_Reject_A_Member_That_Does_Not_Match_The_Schema()
    {
        var noPassword = Map.Of(("email", "new@gmail.com"));
        var badEmail = Map.Of(("email", "not-an-email"), ("password", Passwords.Hash("x", 1000)));

        var addNoPassword = () => UserManagement.AddMember(Users, noPassword);
        addNoPassword.Should().Throw<SchemaViolationException>()
            .Which.Errors.Single().ToString().Should().Be("password: is required but missing");

        var addBadEmail = () => UserManagement.AddMember(Users, badEmail);
        addBadEmail.Should().Throw<SchemaViolationException>()
            .Which.Errors.Single().Path.ToString().Should().Be("email");
    }
    [Fact]
    public void Should_Reject_A_Duplicate_User()
    {
        var existing = Map.Of(
            ("email", "samantha@gmail.com"),
            ("password", Passwords.Hash("another", 1000)));

        var addAgain = () => UserManagement.AddMember(Users, existing);
        addAgain.Should().Throw<DuplicateUserException>();

        // Across collections too: an id taken by a librarian is not free for a member.
        var asMember = Map.Of(
            ("email", "franck@gmail.com"),
            ("password", Passwords.Hash("another", 1000)));

        var addLibrarianAsMember = () => UserManagement.AddMember(Users, asMember);
        addLibrarianAsMember.Should().Throw<DuplicateUserException>();
    }
}
