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

    /// Commits a mutation calculated from `previous`.
    ///
    /// Retries when another thread commits in between: losing the swap means the
    /// reconciliation was against a stale version, so it is redone against the new
    /// one. A genuine conflict throws rather than looping.
    public DataMap Commit(DataMap previous, DataMap next)
    {
        while (true)
        {
            var current = Volatile.Read(ref systemData);
            var reconciled = SystemConsistency.Reconcile(current, previous, next);

            if (ReferenceEquals(Interlocked.CompareExchange(ref systemData, reconciled, current), current))
                return reconciled;
        }
    }

    /// Reads the current version, applies a mutation to it, and commits the result.
    public DataMap Update(Func<DataMap, DataMap> mutation)
    {
        var previous = Get();
        return Commit(previous, mutation(previous));
    }
}
