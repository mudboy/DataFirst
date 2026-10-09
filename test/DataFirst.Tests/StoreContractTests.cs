using DataFirst.Testing;
using System.Collections.Immutable;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

public sealed class StoreContractTests
{
    public static IEnumerable<object[]> Kinds => StoreHarness.Stores.Keys.Select(k => new object[] { k });

    [Property]
    public Property Both_stores_give_the_same_answer_to_any_history() =>
        Prop.ForAll(StoreHarness.History.ToArbitrary(), steps =>
        {
            var snapshot = StoreHarness.Run(StoreHarness.Make("snapshot"), steps);
            var indexed = StoreHarness.Run(StoreHarness.Make("diff-indexed"), steps);
            return snapshot.SequenceEqual(indexed);
        });

    [Property]
    public Property A_successful_commit_advances_the_version_by_one_and_is_readable() =>
        Prop.ForAll(StoreHarness.History.ToArbitrary(), steps =>
            StoreHarness.Stores.Keys.All(kind =>
            {
                var store = StoreHarness.Make(kind);
                var outcomes = StoreHarness.Run(store, steps);
                var committed = outcomes.OfType<Outcome.Committed>().ToList();

                var final = store.Read(StoreHarness.Aggregate);
                return committed.Select(c => c.Version).SequenceEqual(Enumerable.Range(1, committed.Count).Select(i => (long)i))
                       && final.Version == committed.Count
                       && (committed.Count == 0 || final.Value.ToString() == committed[^1].Value);
            }));

    [Property]
    public Property A_rejected_commit_changes_nothing() =>
        Prop.ForAll(StoreHarness.History.ToArbitrary(), steps =>
            StoreHarness.Stores.Keys.All(kind =>
            {
                var store = StoreHarness.Make(kind);
                var reads = new List<Versioned> { store.Read(StoreHarness.Aggregate) };
                var counter = 0L;

                foreach (var step in steps)
                {
                    var from = reads[Math.Max(0, reads.Count - 1 - step.Lag)];
                    var diff = _.DiffObjects(from.Value, _.Set(from.Value, step.Key, ++counter));
                    var before = store.Read(StoreHarness.Aggregate);

                    try { reads.Add(store.Commit(StoreHarness.Aggregate, from.Version, diff)); }
                    catch (ConcurrentModificationException)
                    {
                        var after = store.Read(StoreHarness.Aggregate);
                        if (after.Version != before.Version || !after.Value.Equals(before.Value)) return false;
                    }
                }

                return true;
            }));

    [Property]
    public Property Disjoint_writers_from_the_same_version_both_land_in_either_order(bool aFirst) =>
        Prop.ForAll(Gen.Constant(0).ToArbitrary(), _ =>
            StoreHarness.Stores.Keys.All(kind =>
            {
                var store = StoreHarness.Make(kind);
                var start = store.Read(StoreHarness.Aggregate);
                var writeA = DiffFor(start.Value, "a", 1);
                var writeB = DiffFor(start.Value, "b", 2);

                var (first, second) = aFirst ? (writeA, writeB) : (writeB, writeA);
                store.Commit(StoreHarness.Aggregate, start.Version, first);
                var final = store.Commit(StoreHarness.Aggregate, start.Version, second);

                return final.Version == 2
                       && final.Value.Equals((DataValue)Map.Of(("a", 1), ("b", 2), ("c", 0)));
            }));

    [Theory, MemberData(nameof(Kinds))]
    public void Writers_to_the_same_key_from_the_same_version_conflict_and_name_the_path(string kind)
    {
        var store = StoreHarness.Make(kind);
        var start = store.Read(StoreHarness.Aggregate);

        store.Commit(StoreHarness.Aggregate, start.Version, DiffFor(start.Value, "a", 1));

        var act = () => store.Commit(StoreHarness.Aggregate, start.Version, DiffFor(start.Value, "a", 2));
        act.Should().Throw<ConcurrentModificationException>()
            .Which.ConflictingPaths.Select(p => p.ToString()).Should().Equal("a");
    }

    [Theory, MemberData(nameof(Kinds))]
    public void An_empty_diff_advances_the_version_and_leaves_the_value_alone(string kind)
    {
        // Regression: Merge used to write the empty diff over the whole aggregate.
        var store = StoreHarness.Make(kind);
        var before = store.Read(StoreHarness.Aggregate);

        var after = store.Commit(StoreHarness.Aggregate, before.Version, DataMap.Empty);

        after.Version.Should().Be(before.Version + 1);
        after.Value.Equals(before.Value).Should().BeTrue();
        store.Read(StoreHarness.Aggregate).Value.Equals(before.Value).Should().BeTrue();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void An_empty_diff_never_conflicts_with_a_stale_client(string kind)
    {
        var store = StoreHarness.Make(kind);
        var start = store.Read(StoreHarness.Aggregate);
        store.Commit(StoreHarness.Aggregate, start.Version, DiffFor(start.Value, "a", 1));

        var act = () => store.Commit(StoreHarness.Aggregate, start.Version, DataMap.Empty);
        act.Should().NotThrow();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void A_missing_aggregate_reads_as_null_at_version_zero_and_can_be_created(string kind)
    {
        var store = StoreHarness.Make(kind);
        var absent = StoreHarness.Book("nope");

        var read = store.Read(absent);
        read.Version.Should().Be(0);
        read.Value.Equals((DataValue)DataNull.Instance).Should().BeTrue();

        var created = store.Commit(absent, 0, _.DiffObjects(DataNull.Instance, Map.Of(("title", "New"))));
        created.Version.Should().Be(1);
        created.Value.Equals((DataValue)Map.Of(("title", "New"))).Should().BeTrue();
        store.Read(absent).Value.Equals((DataValue)Map.Of(("title", "New"))).Should().BeTrue();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void The_whole_system_can_be_read_as_the_root_aggregate(string kind)
    {
        var store = StoreHarness.Make(kind);
        store.Read(DataPath.Root).Value.Equals((DataValue)StoreHarness.Initial).Should().BeTrue();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Writing_one_aggregate_leaves_its_neighbours_and_their_versions_alone(string kind)
    {
        var store = StoreHarness.Make(kind);
        var neighbour = StoreHarness.Book("b2");
        var neighbourBefore = store.Read(neighbour);
        var start = store.Read(StoreHarness.Aggregate);

        store.Commit(StoreHarness.Aggregate, start.Version, DiffFor(start.Value, "a", 9));

        var neighbourAfter = store.Read(neighbour);
        neighbourAfter.Version.Should().Be(neighbourBefore.Version);
        neighbourAfter.Value.Equals(neighbourBefore.Value).Should().BeTrue();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Concurrent_writers_to_different_keys_all_land(string kind)
    {
        var store = StoreHarness.Make(kind);
        var keys = Enumerable.Range(0, 16).Select(i => $"k{i}").ToArray();
        var start = store.Read(StoreHarness.Aggregate);

        Parallel.ForEach(keys, new ParallelOptions { MaxDegreeOfParallelism = 8 }, key =>
            store.Commit(StoreHarness.Aggregate, start.Version, DiffFor(start.Value, key, 1)));

        var final = store.Read(StoreHarness.Aggregate);
        final.Version.Should().Be(keys.Length);
        keys.All(k => _.ContainsKey(final.Value, k)).Should().BeTrue();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Concurrent_writers_to_one_key_let_exactly_one_win(string kind)
    {
        var store = StoreHarness.Make(kind);
        var start = store.Read(StoreHarness.Aggregate);
        var wins = 0;
        var conflicts = 0;

        Parallel.For(0, 12, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            try
            {
                store.Commit(StoreHarness.Aggregate, start.Version, DiffFor(start.Value, "a", i + 1));
                Interlocked.Increment(ref wins);
            }
            catch (ConcurrentModificationException) { Interlocked.Increment(ref conflicts); }
        });

        wins.Should().Be(1);
        conflicts.Should().Be(11);
        store.Read(StoreHarness.Aggregate).Version.Should().Be(1);
    }

    private static DataMap DiffFor(DataValue from, string key, long value) =>
        _.DiffObjects(from, _.Set(from, key, value));
}
