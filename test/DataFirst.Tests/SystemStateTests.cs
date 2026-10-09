using DataFirst.Testing;
using System.Collections.Immutable;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

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
