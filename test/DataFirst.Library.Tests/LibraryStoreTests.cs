using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class LibraryStoreTests
{
    private static IEnumerable<string> TitlesOf(DataList results) =>
        results.Select(r => _.Get<string>(r, "title"));
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
}
