using DataFirst.Lodash;

namespace DataFirst;

/// Raised when two mutations, started from the same version, changed the same
/// location, so neither can be applied on top of the other without losing one.
public sealed class ConcurrentModificationException(IReadOnlyList<DataPath> conflictingPaths)
    : Exception($"Conflicting concurrent mutations at: {string.Join(", ", conflictingPaths)}")
{
    public IReadOnlyList<DataPath> ConflictingPaths { get; } = conflictingPaths;
}

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

        var conflicts = CommonPaths(previousToCurrent, previousToNext);
        if (conflicts.Count > 0) throw new ConcurrentModificationException(conflicts);

        return _.Merge(current, previousToNext);
    }

    /// The locations both diffs touch. Empty means the two changes are independent
    /// and can both be kept.
    ///
    /// Overlap, not equality: a change to `items` collides with a change to
    /// `items[1]`, because one replaces what the other reaches into.
    public static IReadOnlyList<DataPath> CommonPaths(DataMap diff1, DataMap diff2)
    {
        var first = _.ChangedPaths(diff1);

        return _.ChangedPaths(diff2)
            .Where(path => first.Any(path.Overlaps))
            .ToList();
    }

    private static DataMap DiffOf(DataMap from, DataMap to) => _.DiffObjects(from, to);
}
