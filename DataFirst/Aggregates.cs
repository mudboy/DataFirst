namespace DataFirst;

/// The units of atomic change.
///
/// An aggregate is a path into the system data, and it is the boundary a commit is
/// scoped to. Two changes to different aggregates never conflict; two changes to the
/// same one are reconciled against each other.
///
/// Choosing these paths is a design decision, not a mechanical one. Too coarse and
/// unrelated work contends -- the whole system as one aggregate means every write
/// fights every other. Too fine and a change that must be atomic gets split across
/// two commits, which nothing will put back together.
public static class Aggregates
{
    /// One book, with its items. Adding a copy contends only with other changes to
    /// the same book.
    public static DataPath Book(string isbn) => DataPath.Of("catalog", "booksByIsbn", isbn);

    public static DataPath Author(string authorId) => DataPath.Of("catalog", "authorsById", authorId);

    /// One member, with their lendings.
    public static DataPath Member(string email) => DataPath.Of("userManagementData", "members", email);

    /// The member collection, for adding and removing members.
    ///
    /// Deliberately coarser than Member: adding a member has to be atomic against
    /// another add of the same id, and that check cannot live inside an aggregate
    /// that does not exist yet.
    public static DataPath Members => DataPath.Of("userManagementData", "members");

    /// All of user management, for reads that decide permission.
    public static DataPath UserManagement => DataPath.Of("userManagementData");

    /// The whole system. What the book's SystemState uses for every commit.
    public static DataPath Everything => DataPath.Root;
}
