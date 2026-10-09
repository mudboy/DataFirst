using DataFirst.Lodash;

namespace DataFirst;

/// The single mutable reference in the system.
///
/// Everything else works on immutable values: a mutation reads the current version,
/// calculates a new one at its leisure, and commits. Commit is where the only
/// mutation happens, and it is one atomic reference swap.
public sealed class SystemState(DataMap initial)
{
    private DataMap systemData = initial;

    /// The current version. Safe to hold on to -- it will not change underneath you.
    public DataMap Get() => Volatile.Read(ref systemData);

    /// The current value of one aggregate, or null when it does not exist yet.
    /// A caller only needs the aggregate it intends to change, not the whole system.
    public DataValue Read(DataPath aggregate)
    {
        var data = Get();
        if (aggregate.Count == 0) return data;
        return _.ContainsKey(data, aggregate) ? _.Get(data, aggregate) : DataNull.Instance;
    }

    /// Commits a mutation calculated from `previous`.
    ///
    /// Retries when another thread commits in between: losing the swap means the
    /// reconciliation was against a stale version, so it is redone against the new
    /// one. A genuine conflict throws rather than looping.
    public DataMap Commit(DataMap previous, DataMap next) => Commit(DataPath.Root, previous, next);

    /// Commits a change to one aggregate.
    ///
    /// The swap is still on the whole system -- in one process that is a pointer
    /// write and costs nothing -- but reconciliation looks only inside the
    /// aggregate. Two commits to different aggregates therefore never conflict, and
    /// the diffing is bounded by the aggregate rather than the size of the system.
    ///
    /// This is the seam a central data store would replace: the swap becomes a
    /// conditional write on the aggregate row, and `previous` becomes a version
    /// token rather than a value.
    public DataMap Commit(DataPath aggregate, DataValue previous, DataValue next)
    {
        while (true)
        {
            var current = Volatile.Read(ref systemData);
            var currentAggregate = aggregate.Count == 0
                ? current
                : _.ContainsKey(current, aggregate) ? _.Get(current, aggregate) : DataNull.Instance;

            var reconciled = SystemConsistency.Reconcile(currentAggregate, previous, next);
            var nextSystem = _.Set(current, aggregate, reconciled);

            if (ReferenceEquals(Interlocked.CompareExchange(ref systemData, nextSystem, current), current))
                return nextSystem;
        }
    }

    /// Reads the current version, applies a mutation to it, and commits the result.
    public DataMap Update(Func<DataMap, DataMap> mutation)
    {
        var previous = Get();
        return Commit(previous, mutation(previous));
    }
}
