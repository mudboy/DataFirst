using DataFirst.Testing;
using System.Collections.Immutable;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class DiffIndexedStoreRetentionTests
{
    [Property]
    public Property A_client_within_the_retained_tail_is_answered_and_one_beyond_it_is_told_to_re_read(
        PositiveInt retainedSeed, NonNegativeInt lagSeed)
    {
        var retained = retainedSeed.Get % 6 + 1;
        var lag = lagSeed.Get % (retained + 4);

        // retained + 4 commits, so a lag of up to retained + 3 is always reachable.
        var store = new DiffIndexedStore(StoreHarness.Initial, retained);
        var reads = new List<Versioned> { store.Read(StoreHarness.Aggregate) };
        for (var i = 0; i < retained + 4; i++)
            reads.Add(store.Commit(StoreHarness.Aggregate, reads[^1].Version,
                _.DiffObjects(reads[^1].Value, _.Set(reads[^1].Value, "a", i + 1))));

        var current = reads[^1];
        var old = reads[^(lag + 1)];
        var disjoint = _.DiffObjects(old.Value, _.Set(old.Value, "b", 99));

        return (lag <= retained
                ? Try(() => store.Commit(StoreHarness.Aggregate, old.Version, disjoint)) == "ok"
                : Try(() => store.Commit(StoreHarness.Aggregate, old.Version, disjoint)) == "stale")
            .Label($"retained={retained} lag={lag} current={current.Version}");
    }

    [Fact]
    public void A_stale_commit_reports_what_the_client_was_at_and_how_far_back_the_store_can_see()
    {
        var store = new DiffIndexedStore(StoreHarness.Initial, retainedChangesPerAggregate: 2);
        var v0 = store.Read(StoreHarness.Aggregate);
        var latest = v0;
        for (var i = 1; i <= 5; i++)
            latest = store.Commit(StoreHarness.Aggregate, latest.Version,
                _.DiffObjects(latest.Value, _.Set(latest.Value, "a", i)));

        var act = () => store.Commit(StoreHarness.Aggregate, v0.Version, DataMap.Empty);

        var stale = act.Should().Throw<StaleVersionException>().Which;
        stale.ClientVersion.Should().Be(0);
        stale.OldestKnown.Should().Be(3);
        stale.Aggregate.ShouldEqual(StoreHarness.Aggregate);
        stale.Message.Should().Contain("re-read");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Retaining_nothing_is_rejected(int retained)
    {
        var act = () => new DiffIndexedStore(StoreHarness.Initial, retained);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Retention_is_per_aggregate()
    {
        var store = new DiffIndexedStore(StoreHarness.Initial, 1);
        var b2Start = store.Read(StoreHarness.Book("b2"));

        var b1 = store.Read(StoreHarness.Aggregate);
        for (var i = 1; i <= 4; i++)
            b1 = store.Commit(StoreHarness.Aggregate, b1.Version, _.DiffObjects(b1.Value, _.Set(b1.Value, "a", i)));

        var act = () => store.Commit(StoreHarness.Book("b2"), b2Start.Version,
            _.DiffObjects(b2Start.Value, _.Set(b2Start.Value, "a", 7)));
        act.Should().NotThrow();
    }

    /// The two stores answer "did anything move?" differently when a key is changed and
    /// then changed back. The snapshot store compares values, so A-B-A looks like
    /// nothing happened; the index store only knows the path was touched, so it reports
    /// a conflict. Pinned here so a change to either store's answer is a deliberate one.
    [Fact]
    public void A_key_changed_and_changed_back_conflicts_in_the_index_store_but_not_the_snapshot_store()
    {
        string Outcome(IAggregateStore store)
        {
            var start = store.Read(StoreHarness.Aggregate);
            var changed = store.Commit(StoreHarness.Aggregate, start.Version,
                _.DiffObjects(start.Value, _.Set(start.Value, "a", 1)));
            store.Commit(StoreHarness.Aggregate, changed.Version,
                _.DiffObjects(changed.Value, _.Set(changed.Value, "a", 0)));

            return Try(() => store.Commit(StoreHarness.Aggregate, start.Version,
                _.DiffObjects(start.Value, _.Set(start.Value, "a", 5))));
        }

        Outcome(new SnapshotAggregateStore(StoreHarness.Initial)).Should().Be("ok");
        Outcome(new DiffIndexedStore(StoreHarness.Initial)).Should().Be("conflict");
    }

    private static string Try(Func<Versioned> commit)
    {
        try { commit(); return "ok"; }
        catch (ConcurrentModificationException) { return "conflict"; }
        catch (StaleVersionException) { return "stale"; }
    }
}
