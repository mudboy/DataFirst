using DataFirst.Lodash;

namespace DataFirst;

/// The system layer: the only place that reads and writes the live state.
///
/// Every decision still happens in the pure functions of Library, Catalog and
/// UserManagement, which take data and return data and know nothing about a store.
/// This class does three things and nothing else: read a snapshot, call one of those
/// functions, and commit the aggregate that changed.
///
/// Reads may span aggregates freely -- a snapshot is an immutable value, so reading
/// the whole thing costs a pointer read and cannot tear. Writes are scoped to a
/// single aggregate, which is what keeps unrelated work from contending.
public sealed class LibrarySystem(SystemState state)
{
    public LibrarySystem(DataMap initial) : this(new SystemState(initial)) { }

    public DataMap Snapshot() => state.Get();

    // Reads: a snapshot is a value, so these need no coordination at all.

    public DataList SearchBook(DataMap searchQuery) => Library.SearchBook(state.Get(), searchQuery);

    public string SearchBooksByTitleJson(string query) =>
        Library.SearchBooksByTitleJson(state.Get(), query);

    public DataList GetBookLendings(string userId, string memberId) =>
        Library.GetBookLendings(state.Get(), userId, memberId);

    // Writes: compute over a snapshot, commit only the aggregate that changed.

    /// Adds a copy of a book. Contends only with other changes to that same book.
    public DataMap AddBookItem(string userId, DataMap bookItemInfo)
    {
        var snapshot = state.Get();
        var updated = Library.AddBookItem(snapshot, userId, bookItemInfo);

        return CommitChangeTo(Aggregates.Book(_.Get<string>(bookItemInfo, "isbn")), snapshot, updated);
    }

    /// Blocks a member. Contends only with other changes to that same member, so it
    /// can run alongside a book being added without either retrying on conflict.
    public DataMap BlockMember(string userId, string memberId)
    {
        var snapshot = state.Get();
        var users = _.Get<DataMap>(snapshot, "userManagementData");

        if (!UserManagement.IsLibrarian(users, userId))
            throw new Exception("Not allowed to block members");

        var updated = _.Set(snapshot, "userManagementData",
            UserManagement.BlockMember(users, memberId));

        return CommitChangeTo(Aggregates.Member(memberId), snapshot, updated);
    }

    /// Adds a member. Scoped to the whole member collection rather than one member,
    /// because the duplicate check is only meaningful against the collection.
    public DataMap AddMember(string userId, DataMap member)
    {
        var snapshot = state.Get();
        var users = _.Get<DataMap>(snapshot, "userManagementData");

        if (!UserManagement.IsLibrarian(users, userId))
            throw new Exception("Not allowed to add members");

        var updated = _.Set(snapshot, "userManagementData", UserManagement.AddMember(users, member));

        return CommitChangeTo(Aggregates.Members, snapshot, updated);
    }

    /// Extracts one aggregate from before and after, and commits just that.
    ///
    /// The pure function computed a whole new system value, but only one aggregate
    /// of it is offered to the store -- so a concurrent change anywhere else is not
    /// something this commit has an opinion about.
    private DataMap CommitChangeTo(DataPath aggregate, DataMap before, DataMap after) =>
        state.Commit(aggregate, _.Get(before, aggregate), _.Get(after, aggregate));
}
