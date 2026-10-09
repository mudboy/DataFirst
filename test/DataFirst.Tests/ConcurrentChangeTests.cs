using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Database;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Tests;

public sealed class ConcurrentChangeTests
{
    // ---- IAggregateStore: the two implementations must agree ----

    public static TheoryData<string> StoreKinds => new() { "snapshot", "diff-indexed" };
    private static IAggregateStore StoreOf(string kind, DataMap initial) =>
        kind switch
        {
            "snapshot" => new SnapshotAggregateStore(initial),
            "diff-indexed" => new DiffIndexedStore(initial),
            _ => throw new ArgumentException(kind)
        };
    [Fact]
    public void Should_Fast_Forward_When_Nothing_Was_Committed_In_Between()
    {
        var previous = Map.Of(("a", 1));
        var next = Map.Of(("a", 2));

        SystemConsistency.Reconcile(previous, previous, next).ShouldEqual(next);
    }
    [Fact]
    public void Should_Merge_Concurrent_Changes_To_Different_Places()
    {
        var state = new SystemState(Map.Of(("catalog", Map.Of(("a", 1), ("b", 2)))));
        var start = state.Get();

        // Two mutations calculated from the same version, touching different keys.
        var next1 = _.Set(start, ["catalog", "a"], 10);
        var next2 = _.Set(start, ["catalog", "b"], 20);

        state.Commit(start, next1);
        state.Commit(start, next2); // reconciled against the first

        state.Get().ShouldEqual(Map.Of(("catalog", Map.Of(("a", 10), ("b", 20)))));
    }
    [Fact]
    public void Should_Reject_Concurrent_Changes_To_The_Same_Place()
    {
        var state = new SystemState(Map.Of(("catalog", Map.Of(("a", 1)))));
        var start = state.Get();

        state.Commit(start, _.Set(start, ["catalog", "a"], 10));

        var secondCommit = () => state.Commit(start, _.Set(start, ["catalog", "a"], 20));

        secondCommit.Should().Throw<ConcurrentModificationException>()
            .Which.ConflictingPaths.Single().ToString().Should().Be("catalog.a");

        // The rejected commit left the state alone.
        state.Get().ShouldEqual(Map.Of(("catalog", Map.Of(("a", 10)))));
    }
    [Fact]
    public void Should_Reconcile_The_Chapter_Five_Scenario()
    {
        // The DiffyLoop scenario: next changed the publication year while current
        // changed the title and an author name. Different places, so both survive.
        var previous = Map.Of(
            ("booksByIsbn", Map.Of(("978-1779501127", Map.Of(("title", "Watchmen"), ("publicationYear", 1987))))),
            ("authorsById", Map.Of(("dave-gibbons", Map.Of(("name", "Dave Gibbons"))))));

        var next = _.Set(previous, ["booksByIsbn", "978-1779501127", "publicationYear"], 1986);

        var current = _.Set(
            _.Set(previous, ["booksByIsbn", "978-1779501127", "title"], "The Watchmen"),
            ["authorsById", "dave-gibbons", "name"], "David Chester Gibbons");

        SystemConsistency.Reconcile(current, previous, next).ShouldEqual(Map.Of(
            ("booksByIsbn", Map.Of(("978-1779501127", Map.Of(("title", "The Watchmen"), ("publicationYear", 1986))))),
            ("authorsById", Map.Of(("dave-gibbons", Map.Of(("name", "David Chester Gibbons")))))));
    }
    [Fact]
    public void Should_Not_Lose_Updates_Under_Parallel_Commits()
    {
        const int workers = 16;
        var state = new SystemState(Map.Of(("counters", Map.Of())));

        Parallel.For(0, workers, i =>
            state.Update(current => _.Set(current, ["counters", $"w{i}"], i)));

        var counters = _.Get<DataMap>(state.Get(), "counters");

        counters.Count.Should().Be(workers);
        for (var i = 0; i < workers; i++)
            _.Get<long>(counters, $"w{i}").Should().Be(i);
    }
    [Fact]
    public void Should_Treat_A_Path_And_Its_Ancestor_As_Overlapping()
    {
        DataPath.Of("items").Overlaps(DataPath.Of("items", 1)).Should().BeTrue();
        DataPath.Of("items", 1).Overlaps(DataPath.Of("items")).Should().BeTrue();
        DataPath.Of("items", 1).Overlaps(DataPath.Of("items", 1)).Should().BeTrue();

        // Siblings are independent, and so are unrelated branches.
        DataPath.Of("items", 0).Overlaps(DataPath.Of("items", 1)).Should().BeFalse();
        DataPath.Of("items").Overlaps(DataPath.Of("title")).Should().BeFalse();

        // The root contains everything.
        DataPath.Root.Overlaps(DataPath.Of("items", 1)).Should().BeTrue();
    }
    [Fact]
    public void Should_Conflict_When_One_Writer_Replaces_What_Another_Reaches_Into()
    {
        var start = Map.Of(("items", List.Of("a", "b")));

        // One writer drops the whole list, the other edits one element. The paths are
        // not equal, but they are not independent: exact intersection let both
        // through and the merge then produced a map where a list should be.
        var wholesale = _.DiffObjects(start, Map.Of(("items", DataNull.Instance)));
        var element = _.DiffObjects(start, Map.Of(("items", List.Of("a", "c"))));

        // Rendered "items.1", not "items.[1]": inside a diff an index is a string key.
        SystemConsistency.CommonPaths(wholesale, element)
            .Select(p => p.ToString()).Should().Equal("items.1");

        SystemConsistency.CommonPaths(element, wholesale)
            .Select(p => p.ToString()).Should().Equal("items");
    }
    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Reject_A_Write_Into_Something_Another_Writer_Replaced(string kind)
    {
        var store = StoreOf(kind, Map.Of(("book", Map.Of(("items", List.Of("a", "b"))))));
        var book = DataPath.Of("book");

        var (start, version) = store.Read(book);

        // Writer 1 drops the list entirely.
        store.Commit(book, version, _.DiffObjects(start, _.Set(start, "items", DataNull.Instance)));

        // Writer 2 still holds the old version and edits inside that list.
        var edit = () => store.Commit(book, version,
            _.DiffObjects(start, _.Set(start, ["items", 1], "c")));

        edit.Should().Throw<ConcurrentModificationException>();

        // The list stayed dropped rather than being resurrected as a map.
        (_.Get(store.Read(book).Value, "items") is DataNull).Should().BeTrue();
    }
    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Still_Merge_Writers_On_Sibling_Elements(string kind)
    {
        // The prefix rule must not make independent edits collide.
        var store = StoreOf(kind, Map.Of(("book", Map.Of(("items", List.Of("a", "b"))))));
        var book = DataPath.Of("book");

        var (start, version) = store.Read(book);

        store.Commit(book, version, _.DiffObjects(start, _.Set(start, ["items", 0], "A")));
        var second = store.Commit(book, version, _.DiffObjects(start, _.Set(start, ["items", 1], "B")));

        (_.Get(second.Value, "items").As<DataList>()).ShouldEqual(List.Of("A", "B"));
    }
}
