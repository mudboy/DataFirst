using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class AggregateScopeTests
{
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
        (_.DiffObjects(LibraryOperations.LibraryData, system.Snapshot())).ShouldEqual(Map.Of(("catalog", Map.Of(("booksByIsbn", Map.Of(("978-1779501127", Map.Of(
                ("bookItems", Map.Of(("2", Map.Of(
                    ("id", "book-item-3"), ("libId", "brooklyn-lib"), ("isLent", false)))))))))))));
    }
}
