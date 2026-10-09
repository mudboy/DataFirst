using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Database;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Tests;

public sealed class DiffAndMergeTests
{
    [Fact]
    public void Diffing()
    {
        var data1 = Map.Of(
            ("a", Map.Of(
                ("x", 1),
                ("y", List.Of(2, 3)),
                ("z", 4)
            )));
        
        var data2 = Map.Of(
            ("a", Map.Of(
                ("x", 2),
                ("y", List.Of(2, 4)),
                ("z", 4)
            )));

        // A list diff is keyed by index: only slot 1 changed, and there is no
        // padding to confuse with a real null.
        var expected = Map.Of(
            ("a", Map.Of(
                ("x", 2),
                ("y", Map.Of(("1", 4)))
            )));

        var diff = _.DiffObjects(data1, data2);
        diff.ShouldEqual(expected);

        // var empty = List.Of(1);
        // var ins = _.InsertAt(empty, 1, 4);
        // ins.Should().BeEquivalentTo(List.Of(1, 4));

        var d1 = List.Of(1, 2);
        var d2 = List.Of(1, 4);
        
        var no = _.Diff(_.Get(d1, "0"), _.Get(d2, "0"));
        
        (no is NoDiff).Should().BeTrue();
        
        var res = _.DiffObjects(d1, d2);
        
        res.ShouldEqual(Map.Of(("1", 4)));

    }
    [Fact]
    public void Should_Not_Confuse_A_Literal_No_Diff_Value_With_An_Unchanged_Node()
    {
        // "no-diff" used to be the sentinel, so a real value of "no-diff" was
        // silently dropped from the diff as though nothing had changed.
        var before = Map.Of(("status", "pending"));
        var after = Map.Of(("status", "no-diff"));

        (_.DiffObjects(before, after).As<DataMap>()).ShouldEqual(Map.Of(("status", "no-diff")));
    }
    [Fact]
    public void Should_Report_Changed_Leaves_And_Unchanged_Nodes()
    {
        var unchanged = _.Diff("Watchmen", "Watchmen");
        (unchanged is NoDiff).Should().BeTrue();

        // A union value's runtime type is the union itself, so pattern matching
        // rather than BeOfType is how you get at the case.
        var changed = _.Diff("Watchmen", "The Watchmen") switch
        {
            Changed(var value) => value.As<string>(),
            NoDiff => null
        };
        changed.Should().Be("The Watchmen");

        var equivalentMaps = _.Diff(
            Map.Of(("title", "Watchmen")),
            Map.Of(("title", "Watchmen")));
        (equivalentMaps is NoDiff).Should().BeTrue();
    }
    [Fact]
    public void Should_List_Information_Paths()
    {
        var data = Map.Of(
            ("a", Map.Of(("x", 1), ("y", List.Of("p", "q")))),
            ("b", true));

        _.InformationPaths(data).Select(p => p.ToString())
            .Should().BeEquivalentTo("a.x", "a.y.[0]", "a.y.[1]", "b");
    }
    [Fact]
    public void Should_Merge_A_Value_Genuinely_Changed_To_Null()
    {
        // This is why a list diff is keyed by index. With positional padding the
        // null at slot 0 (meaning "unchanged") would be indistinguishable from
        // slot 1's real change to null, and merge would have to guess.
        var previous = Map.Of(("xs", List.Of(1, 2)));
        var next = Map.Of(("xs", List.Of(1, DataNull.Instance)));

        var diff = _.DiffObjects(previous, next);
        diff.ShouldEqual(Map.Of(("xs", Map.Of(("1", DataNull.Instance)))));

        _.ApplyDiff(previous, diff).ShouldEqual(next);
    }
    [Fact]
    public void Should_Merge_A_Diff_Back_Onto_Its_Source()
    {
        var previous = Map.Of(("a", Map.Of(("x", 1), ("y", List.Of(2, 3)))));
        var next = Map.Of(("a", Map.Of(("x", 9), ("y", List.Of(2, 30)))));

        _.ApplyDiff(previous, _.DiffObjects(previous, next)).ShouldEqual(next);
    }
    [Fact]
    public void Should_Merge_A_Diff_That_Introduces_A_Path()
    {
        // Reachable whenever two people edit one book and only one of them adds the
        // first copy: the paths are disjoint, the merge is valid, and the descent
        // walks through a key the target does not have.
        var previous = Map.Of(("book", Map.Of(("title", "Watchmen"))));
        var next = Map.Of(("book", Map.Of(("title", "Watchmen"), ("items", List.Of(Map.Of(("id", "a")))))));

        _.ApplyDiff(previous, _.DiffObjects(previous, next)).ShouldEqual(next);
    }
    [Fact]
    public void Should_Diff_From_Nothing_And_Merge_Into_Nothing()
    {
        // Creating a value is the same operation as changing one, so a store needs no
        // separate path for an aggregate that does not exist yet.
        var book = Map.Of(("isbn", "978-1779501127"), ("title", "Watchmen"));

        var born = _.DiffObjects(DataNull.Instance, book);
        born.ShouldEqual(Map.Of(("isbn", "978-1779501127"), ("title", "Watchmen")));
        (_.ApplyDiff(DataNull.Instance, born).As<DataMap>()).ShouldEqual(book);

        // And the reverse reads as every key removed.
        _.DiffObjects(book, DataNull.Instance).ShouldEqual(Map.Of(("isbn", DataNull.Instance), ("title", DataNull.Instance)));

        // A list cannot be rebuilt from its diff alone. Diffs are always maps, with
        // indices as string keys, so the root container type is gone by then --
        // creating a list-rooted aggregate needs the value, not the difference.
        var list = List.Of("a", "b");
        (_.ApplyDiff(DataNull.Instance, _.DiffObjects(DataNull.Instance, list)).As<DataMap>()).ShouldEqual(Map.Of(("0", "a"), ("1", "b")));
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
        SystemConsistency.CommonPaths(Map.Of(), busy).Should().BeEmpty();
        SystemConsistency.CommonPaths(busy, Map.Of()).Should().BeEmpty();
    }
    [Fact]
    public void Probe_MergeIntoMissingPath()
    {
        var previous = Map.Of(("book", Map.Of(("title", "Watchmen"))));
        var next = Map.Of(("book", Map.Of(("title", "Watchmen"), ("items", List.Of(Map.Of(("id", "a")))))));
        var diff = _.DiffObjects(previous, next);
        var merged = _.ApplyDiff(previous, diff);
        merged.ShouldEqual(next);
    }
    [Fact]
    public void DiffyLoop()
    {
        var watchmen = Map.Of(
            ("isbn", "978-1779501127"),
            ("title", "Watchmen"),
            ("publicationYear", 1987),
            ("authorIds", List.Of("alan-moore", "dave-gibbons")));
        var alan = Map.Of(
            ("name", "Alan Moore"),
            ("bookIsbns", List.Of("978-1779501127")));
        var dave = Map.Of(
            ("name", "Dave Gibbons"),
            ("bookIsbns", List.Of("978-1779501127")));
        
        var library = Map.Of(
            ("catalog", Map.Of(
                ("booksByIsbn", Map.Of(("978-1779501127", watchmen))),
                ("authorsById", Map.Of(("alan-moore", alan), ("dave-gibbons", dave))))));

        var previous = library;
        var next = _.Set(library, 
            ["catalog", "booksByIsbn", "978-1779501127", "publicationYear"], 1986);
        var libraryWithUpdatedTitle = _.Set(library,
            ["catalog", "booksByIsbn", "978-1779501127", "title"], "The Watchmen");
        var current = _.Set(libraryWithUpdatedTitle, 
            ["catalog", "authorsById", "dave-gibbons", "name"], "David Chester Gibbons");

        // next changes only the publication year...
        var diff1 = _.DiffObjects(previous, next);

        (diff1.As<DataMap>()).ShouldEqual(Map.Of(("catalog", Map.Of(
                ("booksByIsbn", Map.Of(
                    ("978-1779501127", Map.Of(
                        ("publicationYear", 1986)))))))));

        // ...while current changes the title and one author's name.
        var diff2 = _.DiffObjects(previous, current);

        (diff2.As<DataMap>()).ShouldEqual(Map.Of(("catalog", Map.Of(
                ("booksByIsbn", Map.Of(
                    ("978-1779501127", Map.Of(
                        ("title", "The Watchmen"))))),
                ("authorsById", Map.Of(
                    ("dave-gibbons", Map.Of(
                        ("name", "David Chester Gibbons")))))))));
    }
}
