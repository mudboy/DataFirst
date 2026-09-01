using System.Collections.Immutable;
using DataFirst.Lodash;

namespace DataFirst;

/// An aggregate store that retains only which paths changed, and only recently.
///
/// This is the shape a database would take. Current state is the truth, held as one
/// row per aggregate; alongside it sits a short tail of (version, paths) that exists
/// for exactly one question -- did anything I am about to write move since the client
/// read? The values are never needed, because conflict detection is a set
/// intersection.
///
/// That makes the history bounded, and bounded history has a consequence the snapshot
/// store does not: a client that has been away longer than the tail cannot be
/// answered at all, and is told to re-read rather than guessed at. The retention
/// window is therefore a real design parameter, set by how long a client may hold an
/// edit open.
public sealed class DiffIndexedStore : IAggregateStore
{
    /// One recorded write: the version it produced, and what it touched.
    private sealed record Change(long Version, ImmutableHashSet<DataPath> Paths);

    private sealed record Entry(long Version, ImmutableList<Change> Tail);

    private sealed record State(DataMap Data, ImmutableDictionary<DataPath, Entry> Index);

    private readonly int retained;
    private State state;

    public DiffIndexedStore(DataMap initial, int retainedChangesPerAggregate = 32)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(retainedChangesPerAggregate, 1);

        retained = retainedChangesPerAggregate;
        state = new State(initial, ImmutableDictionary<DataPath, Entry>.Empty);
    }

    public Versioned Read(DataPath aggregate)
    {
        var current = Volatile.Read(ref state);
        return new Versioned(ValueAt(current.Data, aggregate), EntryFor(current, aggregate).Version);
    }

    public Versioned Commit(DataPath aggregate, long expectedVersion, DataMap diff)
    {
        var touched = _.ChangedPaths(diff).ToImmutableHashSet();

        while (true)
        {
            var current = Volatile.Read(ref state);
            var entry = EntryFor(current, aggregate);

            if (expectedVersion != entry.Version)
            {
                var movedSince = PathsChangedSince(aggregate, entry, expectedVersion);

                var conflicts = touched.Where(path => movedSince.Any(path.Overlaps)).ToList();
                if (conflicts.Count > 0) throw new ConcurrentModificationException(conflicts);
            }

            var merged = _.Merge(ValueAt(current.Data, aggregate), diff);
            var nextVersion = entry.Version + 1;

            var next = new State(
                _.Set(current.Data, aggregate, merged),
                current.Index.SetItem(aggregate, new Entry(
                    nextVersion,
                    Prune(entry.Tail.Add(new Change(nextVersion, touched))))));

            if (ReferenceEquals(Interlocked.CompareExchange(ref state, next, current), current))
                return new Versioned(merged, nextVersion);
        }
    }

    /// The union of everything written after the client read.
    ///
    /// Answerable only while the tail still reaches back that far: to cover versions
    /// after `since`, the oldest retained change must be no later than `since + 1`.
    private static ImmutableHashSet<DataPath> PathsChangedSince(
        DataPath aggregate, Entry entry, long since)
    {
        if (entry.Tail.IsEmpty || entry.Tail[0].Version > since + 1)
            throw new StaleVersionException(
                aggregate, since,
                entry.Tail.IsEmpty ? entry.Version : entry.Tail[0].Version - 1);

        return entry.Tail
            .Where(change => change.Version > since)
            .Aggregate(ImmutableHashSet<DataPath>.Empty, (acc, change) => acc.Union(change.Paths));
    }

    private ImmutableList<Change> Prune(ImmutableList<Change> tail) =>
        tail.Count <= retained ? tail : tail.RemoveRange(0, tail.Count - retained);

    private static Entry EntryFor(State state, DataPath aggregate) =>
        state.Index.TryGetValue(aggregate, out var entry)
            ? entry
            : new Entry(0, ImmutableList<Change>.Empty);

    private static DataValue ValueAt(DataMap data, DataPath aggregate)
    {
        if (aggregate.Count == 0) return data;
        return _.ContainsKey(data, aggregate) ? _.Get(data, aggregate) : DataNull.Instance;
    }
}
