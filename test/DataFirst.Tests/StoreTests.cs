using System.Collections.Immutable;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

/// What one commit did, reduced to what both stores must agree on.
public abstract record Outcome
{
    public sealed record Committed(long Version, string Value) : Outcome;
    public sealed record Conflicted(string Paths) : Outcome;
    public sealed record Stale : Outcome;
}

/// One step of a random history: which earlier read to work from, and which key to set.
/// Every write stores a fresh number, so no key ever returns to an earlier value --
/// see the A-B-A test below for why that matters.
public sealed record Step(int Lag, string Key);

public static class StoreHarness
{
    public static readonly DataPath Aggregate = Aggregates.Book("b1");

    public static readonly DataMap Initial = Map.Of(
        ("catalog", Map.Of(("booksByIsbn", Map.Of(
            ("b1", Map.Of(("a", 0), ("b", 0), ("c", 0))),
            ("b2", Map.Of(("a", 0))))))));

    public static readonly IReadOnlyDictionary<string, Func<IAggregateStore>> Stores =
        new Dictionary<string, Func<IAggregateStore>>
        {
            ["snapshot"] = () => new SnapshotAggregateStore(Initial),
            ["diff-indexed"] = () => new DiffIndexedStore(Initial, 1000)
        };

    public static IAggregateStore Make(string kind) => Stores[kind]();

    public static DataMap EmptyDiff => DataMap.Empty;

    /// Runs a history against a store: each step works from an earlier read (the
    /// `Lag`th most recent one) and sets one key to a never-before-used number.
    public static ImmutableArray<Outcome> Run(IAggregateStore store, IEnumerable<Step> steps)
    {
        var reads = new List<Versioned> { store.Read(Aggregate) };
        var counter = 0L;
        var outcomes = ImmutableArray.CreateBuilder<Outcome>();

        foreach (var step in steps)
        {
            var from = reads[Math.Max(0, reads.Count - 1 - step.Lag)];
            var edited = _.Set(from.Value, step.Key, ++counter);
            var diff = _.DiffObjects(from.Value, edited);

            try
            {
                var committed = store.Commit(Aggregate, from.Version, diff);
                reads.Add(committed);
                outcomes.Add(new Outcome.Committed(committed.Version, committed.Value.ToString()!));
            }
            catch (ConcurrentModificationException e)
            {
                outcomes.Add(new Outcome.Conflicted(string.Join(",", e.ConflictingPaths.Select(p => p.ToString()).Order())));
            }
            catch (StaleVersionException)
            {
                outcomes.Add(new Outcome.Stale());
            }
        }

        return outcomes.ToImmutable();
    }

    public static Gen<Step[]> History =>
        Gen.Choose(0, 12).SelectMany(n =>
            (from lag in Gen.Choose(0, 4) from key in Gen.Elements("a", "b", "c") select new Step(lag, key)).ArrayOf(n));
}

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
        var absent = Aggregates.Book("nope");

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
        store.Read(Aggregates.Everything).Value.Equals((DataValue)StoreHarness.Initial).Should().BeTrue();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Writing_one_aggregate_leaves_its_neighbours_and_their_versions_alone(string kind)
    {
        var store = StoreHarness.Make(kind);
        var neighbour = Aggregates.Book("b2");
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
        var b2Start = store.Read(Aggregates.Book("b2"));

        var b1 = store.Read(StoreHarness.Aggregate);
        for (var i = 1; i <= 4; i++)
            b1 = store.Commit(StoreHarness.Aggregate, b1.Version, _.DiffObjects(b1.Value, _.Set(b1.Value, "a", i)));

        var act = () => store.Commit(Aggregates.Book("b2"), b2Start.Version,
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

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class SystemConsistencyProperties
{
    private static readonly DataMap Base = Map.Of(("a", 0), ("b", 0), ("c", 0), ("nested", Map.Of(("x", 0), ("y", 0))));

    private static readonly Gen<DataPath> BasePath =
        Gen.Elements(
            DataPath.Of("a"), DataPath.Of("b"), DataPath.Of("c"),
            DataPath.Of("nested", "x"), DataPath.Of("nested", "y"));

    [Property]
    public bool Nothing_in_between_means_the_mutation_stands(DataMap previous, DataMap next) =>
        SystemConsistency.Reconcile(previous, previous, next).Equals(next);

    [Property]
    public Property A_mutation_that_changed_nothing_returns_whatever_the_system_became() =>
        Prop.ForAll(BasePath.ToArbitrary(), path =>
        {
            var current = _.Set(Base, path, 7);
            return SystemConsistency.Reconcile(current, Base, Base).Equals(current);
        });

    [Property]
    public Property Changes_to_different_places_both_survive_in_either_order() =>
        Prop.ForAll((from p1 in BasePath from p2 in BasePath.Where(p => p != p1) select (p1, p2)).ToArbitrary(), t =>
        {
            var one = _.Set(Base, t.p1, 1);
            var two = _.Set(Base, t.p2, 2);
            var expected = _.Set(one, t.p2, 2);

            return SystemConsistency.Reconcile(one, Base, two).Equals(expected)
                   && SystemConsistency.Reconcile(two, Base, one).Equals(_.Set(two, t.p1, 1));
        });

    [Property]
    public Property Changes_to_the_same_place_conflict_and_name_it() =>
        Prop.ForAll(BasePath.ToArbitrary(), path =>
        {
            var current = _.Set(Base, path, 1);
            var next = _.Set(Base, path, 2);

            try
            {
                SystemConsistency.Reconcile(current, Base, next);
                return false;
            }
            catch (ConcurrentModificationException e)
            {
                return e.ConflictingPaths.Single().Equals(path);
            }
        });

    [Property]
    public Property Common_paths_emptiness_is_symmetric() =>
        Prop.ForAll(Gens.Map.Zip(Gens.Map, (a, b) => (a, b)).ToArbitrary(), t =>
            SystemConsistency.CommonPaths(t.a, t.b).Count == 0
            == (SystemConsistency.CommonPaths(t.b, t.a).Count == 0));

    [Property]
    public bool A_non_empty_diff_is_in_common_with_itself(DataMap diff) =>
        diff.IsEmpty
        || SystemConsistency.CommonPaths(diff, diff).Count == _.ChangedPaths(diff).Count;

    [Property]
    public bool An_empty_diff_has_nothing_in_common_with_anything(DataMap diff) =>
        SystemConsistency.CommonPaths(DataMap.Empty, diff).Count == 0
        && SystemConsistency.CommonPaths(diff, DataMap.Empty).Count == 0;

    [Fact]
    public void A_conflict_message_lists_the_paths()
    {
        var current = _.Set(Base, ["nested", "x"], 1);
        var next = _.Set(Base, ["nested", "x"], 2);

        var act = () => SystemConsistency.Reconcile(current, Base, next);

        act.Should().Throw<ConcurrentModificationException>().WithMessage("*nested.x*");
    }

    [Fact]
    public void Reconcile_of_values_that_are_not_maps_and_have_moved_fails_loudly()
    {
        var act = () => SystemConsistency.Reconcile((DataValue)"a", "b", "c");
        act.Should().Throw<InvalidOperationException>();
    }
}

public sealed class SystemStateTests
{
    private static readonly DataMap Start = Map.Of(
        ("counters", Map.Of(("a", 0), ("b", 0))),
        ("other", Map.Of(("x", 0))));

    [Fact]
    public void Get_returns_the_initial_data_and_Update_applies_a_mutation()
    {
        var state = new SystemState(Start);
        state.Get().ShouldEqual(Start);

        var after = state.Update(data => _.Set(data, ["counters", "a"], 1));

        after.ShouldEqual(_.Set(Start, ["counters", "a"], 1));
        state.Get().ShouldEqual(after);
    }

    [Fact]
    public void A_snapshot_held_before_a_commit_does_not_change()
    {
        var state = new SystemState(Start);
        var held = state.Get();

        state.Update(data => _.Set(data, ["counters", "a"], 1));

        held.ShouldEqual(Start);
    }

    [Fact]
    public void Read_returns_the_aggregate_or_null_when_it_is_not_there()
    {
        var state = new SystemState(Start);

        state.Read(DataPath.Of("counters")).Equals((DataValue)Map.Of(("a", 0), ("b", 0))).Should().BeTrue();
        state.Read(DataPath.Of("nope", "deeper")).Equals((DataValue)DataNull.Instance).Should().BeTrue();
        state.Read(DataPath.Root).Equals((DataValue)Start).Should().BeTrue();
    }

    [Fact]
    public void A_commit_from_a_stale_version_merges_when_the_changes_are_independent()
    {
        var state = new SystemState(Start);
        var read = state.Get();

        state.Update(data => _.Set(data, ["counters", "a"], 1));
        var merged = state.Commit(read, _.Set(read, ["counters", "b"], 2));

        _.Get(merged, ["counters", "a"]).Equals((DataValue)1L).Should().BeTrue();
        _.Get(merged, ["counters", "b"]).Equals((DataValue)2L).Should().BeTrue();
    }

    [Fact]
    public void A_commit_from_a_stale_version_that_collides_throws_and_leaves_the_state_alone()
    {
        var state = new SystemState(Start);
        var read = state.Get();
        var afterFirst = state.Update(data => _.Set(data, ["counters", "a"], 1));

        var act = () => state.Commit(read, _.Set(read, ["counters", "a"], 2));

        act.Should().Throw<ConcurrentModificationException>();
        state.Get().ShouldEqual(afterFirst);
    }

    [Fact]
    public void A_commit_that_changed_nothing_does_not_undo_what_another_writer_did()
    {
        var state = new SystemState(Start);
        var read = state.Get();
        var afterFirst = state.Update(data => _.Set(data, ["counters", "a"], 1));

        state.Commit(read, read).ShouldEqual(afterFirst);
    }

    [Fact]
    public void An_aggregate_commit_only_reconciles_inside_the_aggregate()
    {
        var state = new SystemState(Start);
        var counters = DataPath.Of("counters");
        var other = DataPath.Of("other");
        var startCounters = state.Read(counters);
        var startOther = state.Read(other);

        state.Commit(other, startOther, _.Set(startOther, "x", 9));
        var result = state.Commit(counters, startCounters, _.Set(startCounters, "a", 1));

        _.Get(result, ["other", "x"]).Equals((DataValue)9L).Should().BeTrue();
        _.Get(result, ["counters", "a"]).Equals((DataValue)1L).Should().BeTrue();
    }

    [Fact]
    public void An_aggregate_that_does_not_exist_yet_can_be_committed()
    {
        var state = new SystemState(Start);
        var path = DataPath.Of("fresh", "thing");

        var result = state.Commit(path, DataNull.Instance, Map.Of(("v", 1)));

        _.Get(result, ["fresh", "thing", "v"]).Equals((DataValue)1L).Should().BeTrue();
    }

    [Fact]
    public void Parallel_updates_to_different_counters_are_all_kept()
    {
        var keys = Enumerable.Range(0, 24).Select(i => $"k{i}").ToArray();
        var state = new SystemState(Map.Of(("counters", DataMap.Empty)));

        Parallel.ForEach(keys, new ParallelOptions { MaxDegreeOfParallelism = 8 }, key =>
            state.Update(data => _.Set(data, ["counters", key], 1)));

        var counters = _.Get<DataMap>(state.Get(), "counters");
        counters.Keys.Should().BeEquivalentTo(keys);
    }
}
