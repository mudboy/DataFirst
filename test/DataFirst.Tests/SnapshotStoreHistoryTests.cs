using DataFirst.Testing;
using System.Collections.Immutable;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

public sealed class SnapshotStoreHistoryTests
{
    [Fact]
    public void A_client_at_a_version_the_store_never_held_is_told_to_re_read()
    {
        var store = new SnapshotAggregateStore(StoreHarness.Initial);
        var start = store.Read(StoreHarness.Aggregate);
        store.Commit(StoreHarness.Aggregate, start.Version, _.DiffObjects(start.Value, _.Set(start.Value, "a", 1)));

        var act = () => store.Commit(StoreHarness.Aggregate, 41, DataMap.Empty);

        act.Should().Throw<StaleVersionException>().Which.ClientVersion.Should().Be(41);
    }

    [Fact]
    public void A_future_version_on_an_untouched_aggregate_is_rejected_too()
    {
        var store = new SnapshotAggregateStore(StoreHarness.Initial);
        var act = () => store.Commit(StoreHarness.Aggregate, 3, DataMap.Empty);
        act.Should().Throw<StaleVersionException>();
    }
}
