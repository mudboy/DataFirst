using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class StoreConflictTests
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
        Conflicts.CommonPaths(wholesale, element)
            .Select(p => p.ToString()).Should().Equal("items.1");

        Conflicts.CommonPaths(element, wholesale)
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

    [Fact]
    public void Should_Treat_An_Empty_Diff_As_Touching_Nothing()
    {
        // InformationPaths reports the root of an empty map, because setting a field
        // to {} really is a change. A diff is different: empty means nothing moved,
        // and the root would otherwise be a prefix of every concurrent write.
        _.InformationPaths(Map.Of()).Select(p => p.ToString()).Should().Equal("(root)");
        _.ChangedPaths(Map.Of()).Should().BeEmpty();

        var busy = _.DiffObjects(Map.Of(("a", 1)), Map.Of(("a", 2)));
        Conflicts.CommonPaths(Map.Of(), busy).Should().BeEmpty();
        Conflicts.CommonPaths(busy, Map.Of()).Should().BeEmpty();
    }
}
