using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class ConcurrentChangeTests
{
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
}
