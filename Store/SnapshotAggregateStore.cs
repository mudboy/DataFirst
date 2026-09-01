using System.Collections.Immutable;
using DataFirst.Lodash;

namespace DataFirst;

/// An aggregate store that retains the value of every version.
///
/// This is the in-process shape: because versions share structure, keeping all of
/// them costs little, so the store can always reconstruct what a client started from
/// and answer a conflict question by diffing values. It is never stale.
///
/// The whole state -- data, versions and history -- is one immutable record swapped
/// with a compare-and-exchange, so the store is lock-free and a reader never sees a
/// half-applied commit.
public sealed class SnapshotAggregateStore : IAggregateStore
{
    private sealed record State(
        DataMap Data,
        ImmutableDictionary<DataPath, long> Versions,
        ImmutableDictionary<DataPath, ImmutableDictionary<long, DataValue>> History);

    private State state;

    public SnapshotAggregateStore(DataMap initial) =>
        state = new State(
            initial,
            ImmutableDictionary<DataPath, long>.Empty,
            ImmutableDictionary<DataPath, ImmutableDictionary<long, DataValue>>.Empty);

    public Versioned Read(DataPath aggregate)
    {
        var current = Volatile.Read(ref state);
        return new Versioned(ValueAt(current.Data, aggregate), VersionOf(current, aggregate));
    }

    public Versioned Commit(DataPath aggregate, long expectedVersion, DataMap diff)
    {
        while (true)
        {
            var current = Volatile.Read(ref state);
            var currentVersion = VersionOf(current, aggregate);
            var currentValue = ValueAt(current.Data, aggregate);

            if (expectedVersion != currentVersion)
            {
                var previous = HistoricalValue(current, aggregate, expectedVersion);
                var concurrent = _.DiffObjects(previous, currentValue);

                var conflicts = SystemConsistency.CommonPaths(concurrent, diff);
                if (conflicts.Count > 0) throw new ConcurrentModificationException(conflicts);
            }

            var merged = _.Merge(currentValue, diff);
            var nextVersion = currentVersion + 1;

            var next = new State(
                _.Set(current.Data, aggregate, merged),
                current.Versions.SetItem(aggregate, nextVersion),
                Record(current.History, aggregate, currentVersion, currentValue, nextVersion, merged));

            if (ReferenceEquals(Interlocked.CompareExchange(ref state, next, current), current))
                return new Versioned(merged, nextVersion);
        }
    }

    private static long VersionOf(State state, DataPath aggregate) =>
        state.Versions.TryGetValue(aggregate, out var version) ? version : 0;

    private static DataValue ValueAt(DataMap data, DataPath aggregate)
    {
        if (aggregate.Count == 0) return data;
        return _.ContainsKey(data, aggregate) ? _.Get(data, aggregate) : DataNull.Instance;
    }

    /// Version 0 is whatever the aggregate held before this store ever touched it,
    /// which is only recorded once something else has been written over it.
    private static DataValue HistoricalValue(State state, DataPath aggregate, long version)
    {
        if (state.History.TryGetValue(aggregate, out var versions)
            && versions.TryGetValue(version, out var value))
            return value;

        throw new StaleVersionException(aggregate, version, OldestKnown(state, aggregate));
    }

    private static long OldestKnown(State state, DataPath aggregate) =>
        state.History.TryGetValue(aggregate, out var versions) && versions.Count > 0
            ? versions.Keys.Min()
            : VersionOf(state, aggregate);

    /// Records both ends: the value being replaced (so a later commit from that
    /// version can still find it) and the value being written.
    private static ImmutableDictionary<DataPath, ImmutableDictionary<long, DataValue>> Record(
        ImmutableDictionary<DataPath, ImmutableDictionary<long, DataValue>> history,
        DataPath aggregate, long currentVersion, DataValue currentValue,
        long nextVersion, DataValue nextValue)
    {
        var versions = history.TryGetValue(aggregate, out var existing)
            ? existing
            : ImmutableDictionary<long, DataValue>.Empty;

        return history.SetItem(aggregate, versions
            .SetItem(currentVersion, currentValue)
            .SetItem(nextVersion, nextValue));
    }
}
