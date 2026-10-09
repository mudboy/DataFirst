using DataFirst.Lodash;

namespace DataFirst;

/// Raised when a user cannot be added because the id is taken.
public sealed class DuplicateUserException(string userId)
    : Exception($"A user already exists with id '{userId}'");

/// Who a user is and what they may do.
///
/// Every function is a plain transformation of generic data: nothing is stored here,
/// the data arrives as an argument, and the mutating operations return a new value
/// rather than changing the one they were given.
///
/// The shape it reads:
///
///   { librarians: { "<id>": {...} },
///     members:    { "<id>": { isVip, isSuper, isBlocked, bookLendings: [...] } } }
public static class UserManagement
{
    private const string Librarians = "librarians";
    private const string Members = "members";

    public static bool IsLibrarian(DataMap userManagementData, string userId) =>
        _.ContainsKey(userManagementData, [Librarians, userId]);

    public static bool IsMember(DataMap userManagementData, string userId) =>
        _.ContainsKey(userManagementData, [Members, userId]);

    /// A member with borrowing privileges beyond the ordinary.
    public static bool IsVipMember(DataMap userManagementData, string userId) =>
        HasFlag(userManagementData, userId, "isVip");

    /// A member trusted to see other members' lending records.
    public static bool IsSuperMember(DataMap userManagementData, string userId) =>
        HasFlag(userManagementData, userId, "isSuper");

    public static bool IsBlocked(DataMap userManagementData, string userId) =>
        HasFlag(userManagementData, userId, "isBlocked");

    /// True only when the user exists, is not blocked, and the password matches.
    /// Does not distinguish an unknown user from a wrong password.
    public static bool Authenticate(DataMap userManagementData, string userId, string password)
    {
        var record = FindUser(userManagementData, userId);
        if (record is null || IsBlocked(userManagementData, userId)) return false;

        return record.ContainsKey("password") && Passwords.Verify(record["password"], password);
    }

    /// The lendings recorded against a member, or an empty list when they have none.
    public static DataList BookLendings(DataMap userManagementData, string memberId)
    {
        if (!IsMember(userManagementData, memberId))
            throw new KeyNotFoundException($"No member with id '{memberId}'");

        var path = new StringOrInt[] { Members, memberId, "bookLendings" };
        return _.ContainsKey(userManagementData, path)
            ? _.Get<DataList>(userManagementData, path)
            : DataList.Empty;
    }

    /// Adds a member, rejecting one that does not match the schema or whose id is
    /// already taken. Returns the new data; the argument is untouched.
    public static DataMap AddMember(DataMap userManagementData, DataMap member)
    {
        Validation.ValidateOrThrow(Schemas.Member, member);

        var email = _.Get<string>(member, "email");
        if (IsMember(userManagementData, email) || IsLibrarian(userManagementData, email))
            throw new DuplicateUserException(email);

        return _.Set(userManagementData, [Members, email], member);
    }

    public static DataMap AddLibrarian(DataMap userManagementData, DataMap librarian)
    {
        Validation.ValidateOrThrow(Schemas.Librarian, librarian);

        var email = _.Get<string>(librarian, "email");
        if (IsMember(userManagementData, email) || IsLibrarian(userManagementData, email))
            throw new DuplicateUserException(email);

        return _.Set(userManagementData, [Librarians, email], librarian);
    }

    public static DataMap BlockMember(DataMap userManagementData, string memberId) =>
        SetBlocked(userManagementData, memberId, true);

    public static DataMap UnblockMember(DataMap userManagementData, string memberId) =>
        SetBlocked(userManagementData, memberId, false);

    private static DataMap SetBlocked(DataMap userManagementData, string memberId, bool blocked)
    {
        if (!IsMember(userManagementData, memberId))
            throw new KeyNotFoundException($"No member with id '{memberId}'");

        return _.Set(userManagementData, [Members, memberId, "isBlocked"], blocked);
    }

    /// A flag is absent-means-false, so members need only carry the ones they have.
    private static bool HasFlag(DataMap userManagementData, string userId, string flag)
    {
        var path = new StringOrInt[] { Members, userId, flag };
        return _.ContainsKey(userManagementData, path) && _.Get(userManagementData, path) is true;
    }

    /// Looks in both collections, since librarians authenticate the same way members do.
    private static DataMap? FindUser(DataMap userManagementData, string userId)
    {
        foreach (var collection in new[] { Members, Librarians })
        {
            var path = new StringOrInt[] { collection, userId };
            if (_.ContainsKey(userManagementData, path))
                return _.Get<DataMap>(userManagementData, path);
        }

        return null;
    }
}
