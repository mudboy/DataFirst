using DataFirst.Lodash;

namespace DataFirst.Library;

/// Reconciles a mutation against whatever the system data became while that
/// mutation was being calculated.
public static class SystemConsistency
{
    /// Produces the version to commit.
    ///
    /// When nothing else has been committed since the mutation read `previous`, the
    /// mutation's own result stands. Otherwise the two sets of changes are merged,
    /// provided they touched different places.
    public static DataValue Reconcile(DataValue current, DataValue previous, DataValue next) =>
        current.Equals(previous)
            ? next // fast forward: nothing happened in between
            : ThreeWayMerge(current.As<DataMap>(), previous.As<DataMap>(), next.As<DataMap>());

    public static DataMap Reconcile(DataMap current, DataMap previous, DataMap next) =>
        Reconcile((DataValue)current, previous, next).As<DataMap>();

    private static DataMap ThreeWayMerge(DataMap current, DataMap previous, DataMap next)
    {
        var previousToCurrent = DiffOf(previous, current);
        var previousToNext = DiffOf(previous, next);

        var conflicts = Conflicts.CommonPaths(previousToCurrent, previousToNext);
        if (conflicts.Count > 0) throw new ConcurrentModificationException(conflicts);

        return _.ApplyDiff(current, previousToNext);
    }

    private static DataMap DiffOf(DataMap from, DataMap to) => _.DiffObjects(from, to);
}
