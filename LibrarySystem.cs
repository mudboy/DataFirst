using DataFirst.Lodash;

namespace DataFirst;

/// The system layer: the only place that talks to the store.
///
/// Every decision still happens in the pure functions of Library, Catalog and
/// UserManagement, which take data and return data and know nothing about a store.
/// This class reads the aggregates a decision needs, calls one of those functions,
/// and sends back a diff.
///
/// It codes against IAggregateStore rather than a concrete store, so the same logic
/// runs over one that keeps every version and one that keeps only recent paths. The
/// read-compute-diff-commit shape is the same either way, which is the point: it is
/// also the shape a network round trip takes.
public sealed class LibrarySystem(IAggregateStore store)
{
    public LibrarySystem(DataMap initial) : this(new SnapshotAggregateStore(initial)) { }

    /// The whole system, for reads that span aggregates. A snapshot is an immutable
    /// value, so reading needs no coordination at all.
    public DataMap Snapshot() => store.Read(Aggregates.Everything).Value.As<DataMap>();

    // Reads.

    public DataList SearchBook(DataMap searchQuery) => Library.SearchBook(Snapshot(), searchQuery);

    public string SearchBooksByTitleJson(string query) =>
        Library.SearchBooksByTitleJson(Snapshot(), query);

    public DataList GetBookLendings(string userId, string memberId) =>
        Library.GetBookLendings(Snapshot(), userId, memberId);

    // Writes: read one aggregate, compute over it, send back the difference.

    /// Adds a copy of a book, returning the new book. The caller only ever holds that
    /// one aggregate -- which is what makes this shape work over a network.
    public DataMap AddBookItem(string userId, DataMap bookItemInfo)
    {
        var users = ReadUsers();
        if (!UserManagement.IsLibrarian(users, userId) && !UserManagement.IsVipMember(users, userId))
            throw new Exception("Not allowed to add book items");

        Validation.ValidateOrThrow(Schemas.BookItemInfo, bookItemInfo);
        var isbn = _.Get<string>(bookItemInfo, "isbn");

        var (value, version) = store.Read(Aggregates.Book(isbn));
        if (value is DataNull) throw new KeyNotFoundException($"No book with isbn '{isbn}'");

        var book = value.As<DataMap>();
        var updated = Catalog.AddItemToBook(book, bookItemInfo);

        return Commit(Aggregates.Book(isbn), version, book, updated);
    }

    /// Blocks a member. Scoped to that member, so it can run alongside a book being
    /// changed without either one retrying.
    public DataMap BlockMember(string userId, string memberId)
    {
        var users = ReadUsers();
        if (!UserManagement.IsLibrarian(users, userId))
            throw new Exception("Not allowed to block members");

        var (value, version) = store.Read(Aggregates.Member(memberId));
        if (value is DataNull) throw new KeyNotFoundException($"No member with id '{memberId}'");

        var member = value.As<DataMap>();

        return Commit(Aggregates.Member(memberId), version, member, member.SetItem("isBlocked", true));
    }

    /// Adds a member. Scoped to the whole member collection rather than one member,
    /// because the duplicate check is only meaningful against the collection.
    public DataMap AddMember(string userId, DataMap member)
    {
        var users = ReadUsers();
        if (!UserManagement.IsLibrarian(users, userId))
            throw new Exception("Not allowed to add members");

        var (value, version) = store.Read(Aggregates.Members);
        var members = value.As<DataMap>();

        // UserManagement.AddMember validates and rejects duplicates; it wants the
        // user-management map, so the collection is lifted into that shape and the
        // result taken back out.
        var updated = _.Get<DataMap>(
            UserManagement.AddMember(Map.Of("members", members), member), "members");

        return Commit(Aggregates.Members, version, members, updated);
    }

    private DataMap ReadUsers() => store.Read(Aggregates.UserManagement).Value.As<DataMap>();

    /// Sends the difference rather than the value, so the store learns which paths
    /// were touched and can say precisely what collided.
    private DataMap Commit(DataPath aggregate, long version, DataValue before, DataValue after) =>
        store.Commit(aggregate, version, _.DiffObjects(before, after)).Value.As<DataMap>();
}
