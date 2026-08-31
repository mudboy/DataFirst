namespace DataFirst.Lodash;

public static partial class _
{
    /// Applies a diff produced by DiffObjects onto a value, returning a new value.
    ///
    /// Walks the diff's information paths rather than merging structurally, so every
    /// location the diff records is written exactly as recorded -- including one
    /// genuinely changed to null.
    public static DataValue Merge(DataValue target, DataMap diff) =>
        InformationPaths(diff).Aggregate(target, (acc, path) => Set(acc, path, Get(diff, path)));

    public static DataMap Merge(DataMap target, DataMap diff) =>
        Merge((DataValue)target, diff).As<DataMap>();
}
