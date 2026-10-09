using DataFirst.Testing;
using System.Collections.Immutable;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

public static class StoreHarness
{
    /// A path into the harness data; stores treat it as an opaque aggregate boundary.
    public static DataPath Book(string isbn) => DataPath.Of("catalog", "booksByIsbn", isbn);

    public static readonly DataPath Aggregate = Book("b1");

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
