using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class StoreRetentionTests
{
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
}
