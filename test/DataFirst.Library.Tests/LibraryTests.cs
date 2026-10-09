using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class LibraryTests
{
    /// Asserts through DataMap/DataList's own structural equality. Both implement
    /// IEnumerable, so a plain Should().Be() would route to AwesomeAssertions'
    /// collection assertions and walk members instead. Failure messages print the
    /// values as JSON.
    private static void ShouldEqual(object actual, object expected) =>
        actual.Equals(expected).Should().BeTrue($"of\n  expected: {expected}\n  actual:   {actual}");

    private static void ShouldNotEqual(object actual, object expected) =>
        actual.Should().NotBe(expected);

    [Fact]
    public void Should_Get_AuthorNames()
    {
        var catalogData = _.Get<DataMap>(LibraryOperations.LibraryData, "catalog");
        var book = _.Get<DataMap>(LibraryOperations.LibraryData, ["catalog", "booksByIsbn", "978-1779501127"]);
        
        var names = Catalog.AuthorNames(catalogData, book);

        ShouldEqual(names, List.Of("Alan Moore", "Dave Gibbons"));
    }

    [Fact]
    public void Should_Search_Books_By_Title()
    {   
        var catalogData = _.Get<DataMap>(LibraryOperations.LibraryData, "catalog");

        var result = Catalog.SearchBooksByTitle(catalogData, "Wat");

        ShouldEqual(result, List.Of(
            Map.Of(("authorNames", List.Of("Alan Moore", "Dave Gibbons")),
                ("isbn", "978-1779501127"),
                ("title", "Watchmen"))));
    }

    [Fact]
    public void Should_Search_Library_Books_By_Title_Json()
    {
        var result = LibraryOperations.SearchBooksByTitleJson(LibraryOperations.LibraryData, "Watchmen");

        result.Should().Be(
            """[{"title":"Watchmen","isbn":"978-1779501127","authorNames":["Alan Moore","Dave Gibbons"]}]""");
    }

    [Fact]
    public void Should_Not_Modify_Original_With_Set()
    {
        var oldData = LibraryOperations.LibraryData;
        var newData = _.Set(oldData, 
            ["catalog", "booksByIsbn", "978-1779501127", "publicationYear"], 1986);

        ShouldNotEqual(newData, oldData);
    }

    [Fact]
    public void Should_Accept_The_Real_Library_Data()
    {
        Validation.Validate(Schemas.LibraryData, LibraryOperations.LibraryData).Errors()
            .Should().BeEmpty();

        Schemas.ValidateCatalog(_.Get<DataMap>(LibraryOperations.LibraryData, "catalog")).Errors()
            .Should().BeEmpty();
    }

    [Fact]
    public void Should_Validate_A_Request_At_The_Boundary()
    {
        var request = Map.Of(("title", "Watchmen"), ("fields", List.Of("title", "isbn")));

        LibraryOperations.SearchBooksJson(LibraryOperations.LibraryData, request)
            .Should().Be("""[{"title":"Watchmen","isbn":"978-1779501127"}]""");
    }

    [Fact]
    public void Should_Reject_A_Malformed_Request_At_The_Boundary()
    {
        var badRequest = Map.Of(("title", ""), ("fields", List.Of("title", "publisher")));

        var search = () => LibraryOperations.SearchBooksJson(LibraryOperations.LibraryData, badRequest);

        search.Should().Throw<SchemaViolationException>()
            .Which.Errors.Select(e => e.ToString()).Should().BeEquivalentTo(
                "title: must be at least 1 characters, but was 0",
                "fields.[1]: must be one of [\"title\",\"isbn\",\"authorNames\"], but was \"publisher\"");
    }

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

    [Fact]
    public void Should_Read_Book_Lendings()
    {
        ShouldEqual(
            UserManagement.BookLendings(Users, "samantha@gmail.com"),
            List.Of(Map.Of(
                ("bookItemId", "book-item-1"),
                ("bookIsbn", "978-1779501127"),
                ("lendingDate", "2020-04-23"))));

        // A member with no lendings recorded gets an empty list, not an error.
        UserManagement.BookLendings(Users, "vip@gmail.com").Should().BeEmpty();

        var unknown = () => UserManagement.BookLendings(Users, "nobody@gmail.com");
        unknown.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Should_Accept_The_Seeded_User_Management_Data()
    {
        Schemas.ValidateUserManagement(Users).Errors().Should().BeEmpty();
    }

    [Fact]
    public void Should_Refuse_Library_Operations_To_Unauthorised_Users()
    {
        // vip@gmail.com is neither a librarian nor a super member.
        var getLendings = () => LibraryOperations.GetBookLendings(LibraryOperations.LibraryData, "vip@gmail.com", "samantha@gmail.com");
        getLendings.Should().Throw<Exception>().WithMessage("Not allowed to get book lendings");

        // samantha is a super member, but not a VIP.
        var addItem = () => LibraryOperations.AddBookItem(LibraryOperations.LibraryData, "samantha@gmail.com", Map.Of());
        addItem.Should().Throw<Exception>().WithMessage("Not allowed to add book items");

        var unknownUser = () => LibraryOperations.AddBookItem(LibraryOperations.LibraryData, "nobody@gmail.com", Map.Of());
        unknownUser.Should().Throw<Exception>().WithMessage("Not allowed to add book items");
    }

    private static DataMap CatalogFixture => Map.Of(
        ("booksByIsbn", Map.Of(
            ("978-1779501127", Map.Of(
                ("isbn", "978-1779501127"),
                ("title", "Watchmen"),
                ("publicationYear", 1987),
                ("authorIds", List.Of("alan-moore")))),
            ("978-1982137274", Map.Of(
                ("isbn", "978-1982137274"),
                ("title", "7 Habits of Highly Effective People"),
                ("publicationYear", 2020),
                ("authorIds", List.Of("stephen-covey")))))),
        ("authorsById", Map.Of(
            ("alan-moore", Map.Of(("name", "Alan Moore"), ("bookIsbns", List.Of("978-1779501127")))),
            ("stephen-covey", Map.Of(("name", "Stephen Covey"), ("bookIsbns", List.Of("978-1982137274")))))));

    private static IEnumerable<string> TitlesOf(DataList results) =>
        results.Select(r => _.Get<string>(r, "title"));

    [Fact]
    public void Should_Search_On_Combined_Criteria()
    {
        // An empty query matches everything.
        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of()))
            .Should().BeEquivalentTo("Watchmen", "7 Habits of Highly Effective People");

        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of(("author", "Moore"))))
            .Should().Equal("Watchmen");

        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of(("publishedAfter", 2000))))
            .Should().Equal("7 Habits of Highly Effective People");

        // Criteria combine with AND.
        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of(("title", "Habits"), ("publishedBefore", 2000))))
            .Should().BeEmpty();

        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of(("title", "Habits"), ("publishedAfter", 2000))))
            .Should().Equal("7 Habits of Highly Effective People");

        // Bounds are inclusive.
        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of(("publishedAfter", 1987), ("publishedBefore", 1987))))
            .Should().Equal("Watchmen");
    }

    [Fact]
    public void Should_Search_Case_Insensitively()
    {
        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of(("title", "watchMEN"))))
            .Should().Equal("Watchmen");

        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of(("author", "moore"))))
            .Should().Equal("Watchmen");

        TitlesOf(Catalog.SearchBooksByTitle(CatalogFixture, "WATCH"))
            .Should().Equal("Watchmen");
    }

    [Fact]
    public void Should_Reject_A_Search_Criterion_The_Schema_Does_Not_Name()
    {
        var search = () => Catalog.SearchBook(CatalogFixture, Map.Of(("publisher", "DC")));

        search.Should().Throw<SchemaViolationException>()
            .Which.Errors.Single().ToString().Should().Be("publisher: is not a permitted property");
    }

    [Fact]
    public void Should_Describe_A_Members_Lendings()
    {
        var lendings = UserManagement.BookLendings(Users, "samantha@gmail.com");
        var catalog = _.Get<DataMap>(LibraryOperations.LibraryData, "catalog");

        ShouldEqual(
            Catalog.GetBookLendings(catalog, lendings),
            List.Of(Map.Of(
                ("bookItemId", "book-item-1"),
                ("lendingDate", "2020-04-23"),
                ("title", "Watchmen"),
                ("isbn", "978-1779501127"),
                ("authorNames", List.Of("Alan Moore", "Dave Gibbons")))));
    }

    [Fact]
    public void Should_Reject_A_Lending_For_A_Book_Not_In_The_Catalogue()
    {
        var orphan = List.Of(Map.Of(
            ("bookItemId", "book-item-9"),
            ("bookIsbn", "978-0000000000"),
            ("lendingDate", "2020-04-23")));

        var describe = () => Catalog.GetBookLendings(CatalogFixture, orphan);

        describe.Should().Throw<KeyNotFoundException>()
            .WithMessage("*978-0000000000*");
    }

    [Fact]
    public void Should_Add_A_Book_Item()
    {
        var info = Map.Of(("isbn", "978-1779501127"), ("id", "book-item-3"), ("libId", "brooklyn-lib"));

        var updated = Catalog.AddBookItem(CatalogFixture, info);

        ShouldEqual(
            _.Get<DataList>(updated, ["booksByIsbn", "978-1779501127", "bookItems"]),
            List.Of(Map.Of(("id", "book-item-3"), ("libId", "brooklyn-lib"), ("isLent", false))));

        // A new item is not lent, and isLent cannot be supplied.
        var withIsLent = Map.Of(
            ("isbn", "978-1779501127"), ("id", "book-item-4"), ("libId", "brooklyn-lib"), ("isLent", true));

        var addWithIsLent = () => Catalog.AddBookItem(CatalogFixture, withIsLent);
        addWithIsLent.Should().Throw<SchemaViolationException>()
            .Which.Errors.Single().ToString().Should().Be("isLent: is not a permitted property");

        // The argument is untouched.
        _.ContainsKey(CatalogFixture, ["booksByIsbn", "978-1779501127", "bookItems"]).Should().BeFalse();
    }

    [Fact]
    public void Should_Reject_A_Duplicate_Book_Item()
    {
        var catalog = _.Get<DataMap>(LibraryOperations.LibraryData, "catalog");
        var taken = Map.Of(("isbn", "978-1779501127"), ("id", "book-item-1"), ("libId", "nyc-central-lib"));

        var add = () => Catalog.AddBookItem(catalog, taken);
        add.Should().Throw<DuplicateBookItemException>();
    }

    [Fact]
    public void Should_Reject_A_Book_Item_For_An_Unknown_Book()
    {
        var info = Map.Of(("isbn", "978-0000000000"), ("id", "book-item-3"), ("libId", "brooklyn-lib"));

        var add = () => Catalog.AddBookItem(CatalogFixture, info);
        add.Should().Throw<KeyNotFoundException>().WithMessage("*978-0000000000*");
    }

    [Fact]
    public void Should_Get_Lendings_End_To_End()
    {
        // A librarian may read another member's lendings.
        TitlesOf(LibraryOperations.GetBookLendings(LibraryOperations.LibraryData, "franck@gmail.com", "samantha@gmail.com"))
            .Should().Equal("Watchmen");

        // So may a super member.
        TitlesOf(LibraryOperations.GetBookLendings(LibraryOperations.LibraryData, "samantha@gmail.com", "samantha@gmail.com"))
            .Should().Equal("Watchmen");

        // A member with no lendings gets an empty list.
        LibraryOperations.GetBookLendings(LibraryOperations.LibraryData, "franck@gmail.com", "vip@gmail.com")
            .Should().BeEmpty();
    }

    [Fact]
    public void Should_Add_A_Book_Item_End_To_End()
    {
        var info = Map.Of(("isbn", "978-1779501127"), ("id", "book-item-3"), ("libId", "brooklyn-lib"));

        var updated = LibraryOperations.AddBookItem(LibraryOperations.LibraryData, "vip@gmail.com", info);

        var items = _.Get<DataList>(updated, ["catalog", "booksByIsbn", "978-1779501127", "bookItems"]);
        items.Select(i => _.Get<string>(i, "id"))
            .Should().Equal("book-item-1", "book-item-2", "book-item-3");

        // The whole library data comes back, so user management survives the change.
        UserManagement.IsLibrarian(_.Get<DataMap>(updated, "userManagementData"), "franck@gmail.com")
            .Should().BeTrue();

        // The only difference is the new item.
        ShouldEqual(
            _.DiffObjects(LibraryOperations.LibraryData, updated),
            Map.Of(("catalog", Map.Of(("booksByIsbn", Map.Of(("978-1779501127", Map.Of(
                ("bookItems", Map.Of(("2", Map.Of(
                    ("id", "book-item-3"), ("libId", "brooklyn-lib"), ("isLent", false)))))))))))));
    }

    private static DataMap TwoBookLibrary => _.Set(
        LibraryOperations.LibraryData,
        ["catalog", "booksByIsbn", "978-1982137274"],
        Map.Of(
            ("isbn", "978-1982137274"),
            ("title", "7 Habits of Highly Effective People"),
            ("publicationYear", 2020),
            ("authorIds", List.Of("alan-moore"))));

    [Fact]
    public void Should_Not_Contend_Across_Aggregates()
    {
        var state = new SystemState(TwoBookLibrary);
        var start = state.Get();

        var watchmen = Aggregates.Book("978-1779501127");
        var habits = Aggregates.Book("978-1982137274");

        // Two changes calculated from the same version, in different aggregates.
        var newWatchmen = _.Set(_.Get(start, watchmen), "publicationYear", 1986);
        var newHabits = _.Set(_.Get(start, habits), "publicationYear", 2021);

        state.Commit(watchmen, _.Get(start, watchmen), newWatchmen);
        state.Commit(habits, _.Get(start, habits), newHabits);

        var after = state.Get();
        _.Get<long>(after, [.. watchmen, "publicationYear"]).Should().Be(1986);
        _.Get<long>(after, [.. habits, "publicationYear"]).Should().Be(2021);
    }

    [Fact]
    public void Should_Merge_Disjoint_Changes_Within_One_Aggregate()
    {
        var state = new SystemState(LibraryOperations.LibraryData);
        var book = Aggregates.Book("978-1779501127");
        var start = _.Get(state.Get(), book);

        state.Commit(book, start, _.Set(start, "publicationYear", 1986));
        state.Commit(book, start, _.Set(start, "title", "The Watchmen"));

        var after = _.Get(state.Get(), book);
        _.Get<long>(after, "publicationYear").Should().Be(1986);
        _.Get<string>(after, "title").Should().Be("The Watchmen");
    }

    [Fact]
    public void Should_Still_Conflict_Within_One_Aggregate()
    {
        var state = new SystemState(LibraryOperations.LibraryData);
        var book = Aggregates.Book("978-1779501127");
        var start = _.Get(state.Get(), book);

        state.Commit(book, start, _.Set(start, "title", "The Watchmen"));

        var second = () => state.Commit(book, start, _.Set(start, "title", "Watchmen!"));

        second.Should().Throw<ConcurrentModificationException>()
            .Which.ConflictingPaths.Single().ToString().Should().Be("title");
    }

    [Fact]
    public void Should_Commit_An_Aggregate_That_Does_Not_Exist_Yet()
    {
        var state = new SystemState(LibraryOperations.LibraryData);
        var newBook = Aggregates.Book("978-0000000001");

        // Nothing there yet, so the previous value is null rather than an error.
        (state.Read(newBook) is DataNull).Should().BeTrue();

        state.Commit(newBook, DataNull.Instance, Map.Of(
            ("isbn", "978-0000000001"), ("title", "New"), ("authorIds", List.Of("alan-moore"))));

        _.Get<string>(state.Get(), [.. newBook, "title"]).Should().Be("New");
    }

    [Fact]
    public void Should_Read_Only_The_Aggregate_A_Caller_Needs()
    {
        var state = new SystemState(LibraryOperations.LibraryData);

        // A caller changing a book never has to hold the whole system -- which is
        // the property that makes this work over a network.
        var book = state.Read(Aggregates.Book("978-1779501127")).As<DataMap>();

        book.Keys.Should().Contain("title");
        book.ContainsKey("userManagementData").Should().BeFalse();

        state.Commit(Aggregates.Book("978-1779501127"), book, _.Set(book, "publicationYear", 1986));

        _.Get<long>(state.Get(), ["catalog", "booksByIsbn", "978-1779501127", "publicationYear"])
            .Should().Be(1986);
    }

    [Fact]
    public void Should_Scope_Library_Writes_To_One_Aggregate()
    {
        var system = new LibrarySystem(LibraryOperations.LibraryData);

        // The caller gets back the aggregate it changed, not the system.
        var book = system.AddBookItem("franck@gmail.com", Map.Of(
            ("isbn", "978-1779501127"), ("id", "book-item-3"), ("libId", "brooklyn-lib")));

        _.Get<DataList>(book, "bookItems").Select(i => _.Get<string>(i, "id"))
            .Should().Equal("book-item-1", "book-item-2", "book-item-3");

        // And nothing else in the system moved.
        ShouldEqual(
            _.DiffObjects(LibraryOperations.LibraryData, system.Snapshot()),
            Map.Of(("catalog", Map.Of(("booksByIsbn", Map.Of(("978-1779501127", Map.Of(
                ("bookItems", Map.Of(("2", Map.Of(
                    ("id", "book-item-3"), ("libId", "brooklyn-lib"), ("isLent", false)))))))))))));
    }

    public static TheoryData<string> StoreKinds => new() { "snapshot", "diff-indexed" };

    private static IAggregateStore StoreOf(string kind, DataMap initial) =>
        kind switch
        {
            "snapshot" => new SnapshotAggregateStore(initial),
            "diff-indexed" => new DiffIndexedStore(initial),
            _ => throw new ArgumentException(kind)
        };

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Read_An_Aggregate_With_Its_Version(string kind)
    {
        var store = StoreOf(kind, LibraryOperations.LibraryData);
        var book = Aggregates.Book("978-1779501127");

        var (value, version) = store.Read(book);

        version.Should().Be(0);
        _.Get<string>(value, "title").Should().Be("Watchmen");

        // An aggregate that is not there reads as null at version 0, so creating one
        // needs no separate code path.
        var (missing, missingVersion) = store.Read(Aggregates.Book("978-0000000000"));
        (missing is DataNull).Should().BeTrue();
        missingVersion.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Apply_A_Diff_And_Advance_The_Version(string kind)
    {
        var store = StoreOf(kind, LibraryOperations.LibraryData);
        var book = Aggregates.Book("978-1779501127");

        var (before, version) = store.Read(book);
        var after = _.Set(before, "publicationYear", 1986);

        var committed = store.Commit(book, version, _.DiffObjects(before, after));

        committed.Version.Should().Be(1);
        _.Get<long>(committed.Value, "publicationYear").Should().Be(1986);
        store.Read(book).Version.Should().Be(1);

        // The title was never in the diff, so it survived.
        _.Get<string>(store.Read(book).Value, "title").Should().Be("Watchmen");
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Merge_Two_Writers_On_Different_Paths(string kind)
    {
        var store = StoreOf(kind, LibraryOperations.LibraryData);
        var book = Aggregates.Book("978-1779501127");

        // Both read the same version, then write.
        var (start, version) = store.Read(book);

        store.Commit(book, version, _.DiffObjects(start, _.Set(start, "publicationYear", 1986)));
        var second = store.Commit(book, version, _.DiffObjects(start, _.Set(start, "title", "The Watchmen")));

        second.Version.Should().Be(2);
        _.Get<long>(second.Value, "publicationYear").Should().Be(1986);
        _.Get<string>(second.Value, "title").Should().Be("The Watchmen");
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Reject_Two_Writers_On_The_Same_Path(string kind)
    {
        var store = StoreOf(kind, LibraryOperations.LibraryData);
        var book = Aggregates.Book("978-1779501127");
        var (start, version) = store.Read(book);

        store.Commit(book, version, _.DiffObjects(start, _.Set(start, "title", "The Watchmen")));

        var second = () => store.Commit(book, version, _.DiffObjects(start, _.Set(start, "title", "Watchmen!")));

        second.Should().Throw<ConcurrentModificationException>()
            .Which.ConflictingPaths.Single().ToString().Should().Be("title");

        // A rejected commit changes nothing.
        store.Read(book).Version.Should().Be(1);
        _.Get<string>(store.Read(book).Value, "title").Should().Be("The Watchmen");
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Keep_Aggregates_Independent(string kind)
    {
        var store = StoreOf(kind, LibraryOperations.LibraryData);
        var book = Aggregates.Book("978-1779501127");
        var member = Aggregates.Member("samantha@gmail.com");

        var (bookValue, bookVersion) = store.Read(book);
        var (memberValue, memberVersion) = store.Read(member);

        store.Commit(book, bookVersion, _.DiffObjects(bookValue, _.Set(bookValue, "publicationYear", 1986)));

        // The member is untouched, so its version has not moved and a write against
        // the version read earlier still applies.
        store.Read(member).Version.Should().Be(memberVersion);
        store.Commit(member, memberVersion, _.DiffObjects(memberValue, _.Set(memberValue, "isBlocked", true)));

        _.Get<bool>(store.Read(member).Value, "isBlocked").Should().BeTrue();
        _.Get<long>(store.Read(book).Value, "publicationYear").Should().Be(1986);
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Create_An_Aggregate_That_Is_Not_There_Yet(string kind)
    {
        var store = StoreOf(kind, LibraryOperations.LibraryData);
        var fresh = Aggregates.Book("978-0000000001");

        var (missing, version) = store.Read(fresh);
        var book = Map.Of(("isbn", "978-0000000001"), ("title", "New"), ("authorIds", List.Of("alan-moore")));

        var created = store.Commit(fresh, version, _.DiffObjects(missing, book));

        created.Version.Should().Be(1);
        _.Get<string>(created.Value, "title").Should().Be("New");
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Run_LibrarySystem_Unchanged(string kind)
    {
        var system = new LibrarySystem(StoreOf(kind, LibraryOperations.LibraryData));

        TitlesOf(system.GetBookLendings("franck@gmail.com", "samantha@gmail.com"))
            .Should().Equal("Watchmen");

        var book = system.AddBookItem("vip@gmail.com", Map.Of(
            ("isbn", "978-1779501127"), ("id", "book-item-3"), ("libId", "brooklyn-lib")));
        _.Get<DataList>(book, "bookItems").Count.Should().Be(3);

        system.BlockMember("franck@gmail.com", "samantha@gmail.com");
        UserManagement.IsBlocked(
            _.Get<DataMap>(system.Snapshot(), "userManagementData"), "samantha@gmail.com")
            .Should().BeTrue();

        system.AddMember("franck@gmail.com", Map.Of(
            ("email", "new@gmail.com"), ("password", Passwords.Hash("new-secret", 1000))));
        UserManagement.IsMember(
            _.Get<DataMap>(system.Snapshot(), "userManagementData"), "new@gmail.com")
            .Should().BeTrue();

        var notALibrarian = () => system.BlockMember("vip@gmail.com", "samantha@gmail.com");
        notALibrarian.Should().Throw<Exception>().WithMessage("Not allowed to block members");
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Survive_Parallel_Writes_To_Different_Aggregates(string kind)
    {
        const int books = 12;
        var seed = Enumerable.Range(0, books).Aggregate(LibraryOperations.LibraryData,
            (data, i) => _.Set(data, ["catalog", "booksByIsbn", $"978-000000000{i}"], Map.Of(
                ("isbn", $"978-000000000{i}"), ("title", $"Book {i}"), ("authorIds", List.Of("alan-moore")))));

        var system = new LibrarySystem(StoreOf(kind, seed));

        Parallel.For(0, books, i =>
            system.AddBookItem("franck@gmail.com", Map.Of(
                ("isbn", $"978-000000000{i}"), ("id", $"item-{i}"), ("libId", "brooklyn-lib"))));

        var final = system.Snapshot();
        for (var i = 0; i < books; i++)
            _.Get<DataList>(final, ["catalog", "booksByIsbn", $"978-000000000{i}", "bookItems"])
                .Select(item => _.Get<string>(item, "id")).Should().Equal($"item-{i}");
    }

    // ---- where the two implementations legitimately differ ----

    [Fact]
    public void Should_Reject_A_Client_Older_Than_The_Retained_Tail()
    {
        // Room for two changes only.
        var store = new DiffIndexedStore(LibraryOperations.LibraryData, retainedChangesPerAggregate: 2);
        var book = Aggregates.Book("978-1779501127");

        var (start, ancientVersion) = store.Read(book);

        // Three unrelated writes push the client version off the end of the tail.
        var running = start;
        for (var i = 0; i < 3; i++)
        {
            var (value, version) = store.Read(book);
            running = _.Set(value, "publicationYear", 1900 + i);
            store.Commit(book, version, _.DiffObjects(value, running));
        }

        // The paths do not collide, but the store can no longer tell that.
        var late = () => store.Commit(book, ancientVersion, _.DiffObjects(start, _.Set(start, "title", "Late")));

        late.Should().Throw<StaleVersionException>()
            .Which.ClientVersion.Should().Be(ancientVersion);
    }

    [Fact]
    public void Should_Answer_An_Old_Client_When_Every_Version_Is_Retained()
    {
        // The same sequence against the store that keeps values: it can still work out
        // what moved, so a non-colliding late write is accepted rather than refused.
        var store = new SnapshotAggregateStore(LibraryOperations.LibraryData);
        var book = Aggregates.Book("978-1779501127");

        var (start, ancientVersion) = store.Read(book);

        for (var i = 0; i < 3; i++)
        {
            var (value, version) = store.Read(book);
            store.Commit(book, version, _.DiffObjects(value, _.Set(value, "publicationYear", 1900 + i)));
        }

        var late = store.Commit(book, ancientVersion, _.DiffObjects(start, _.Set(start, "title", "Late")));

        _.Get<string>(late.Value, "title").Should().Be("Late");
        _.Get<long>(late.Value, "publicationYear").Should().Be(1902);
    }

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

    [Fact]
    public void Should_Read_At_Paths()
    {
        var library = LibraryOperations.LibraryData;

        var picked = _.At(library, [
            DataPath.Of("catalog", "booksByIsbn", "978-1779501127", "title"),
            DataPath.Of("userManagementData", "members", "samantha@gmail.com", "email"),
            DataPath.Of("catalog", "nothing", "here")]);

        ShouldEqual(picked, List.Of("Watchmen", "samantha@gmail.com", DataNull.Instance));
    }
}
