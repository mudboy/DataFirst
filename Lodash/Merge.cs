namespace DataFirst.Lodash;

public static partial class _
{
    /// Applies a diff produced by DiffObjects onto a value, returning a new value.
    ///
    /// Walks the diff's information paths rather than merging structurally, so every
    /// location the diff records is written exactly as recorded -- including one
    /// genuinely changed to null.
    public static DataValue Merge(DataValue target, DataMap diff)
    {
        var paths = InformationPaths(diff);
        return paths.Aggregate(Seed(target, paths), (acc, path) => Set(acc, path, Get(diff, path)));
    }

    /// Merging into nothing builds a map.
    ///
    /// It cannot build a list: a diff is always a map, with list indices rendered as
    /// string keys, so by the time a change reaches here the root container type has
    /// been erased. Creating a map-rooted aggregate from nothing works; creating a
    /// list-rooted one needs the value, not just the diff. Aggregates are maps in
    /// practice, so this has not bitten -- but it is a real edge of the encoding.
    private static DataValue Seed(DataValue target, IReadOnlyList<DataPath> paths) =>
        target is DataNull && paths.Count > 0 ? DataMap.Empty : target;

    public static DataMap Merge(DataMap target, DataMap diff) =>
        Merge((DataValue)target, diff).As<DataMap>();
}
