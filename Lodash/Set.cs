namespace DataFirst.Lodash;

public static partial class _
{
    /// Writes a value at a single key or index, returning a new structure.
    public static DataValue Set(DataValue obj, StringOrInt key, DataValue value) =>
        (obj, key) switch
        {
            (DataMap m, string k) => m.SetItem(k, value),
            (DataList l, _) when key.TryAsIndex(out var i) => SetAt(l, i, value),
            (DataMap, int i) => throw new InvalidOperationException(
                $"Cannot index a map with {i}; maps are keyed by string"),
            (DataList, string s) => throw new InvalidOperationException(
                $"Cannot index a list with key '{s}'; list indices must be numeric"),
            _ => throw new InvalidOperationException(
                $"Cannot write {key.Describe()} into a {obj.Describe()}")
        };

    /// Writes a value at a path, rebuilding each node along the way.
    /// An empty path replaces the whole structure.
    public static DataValue Set(DataValue obj, IReadOnlyList<StringOrInt> path, DataValue value)
    {
        if (path.Count == 0) return value;

        var key = path[0];
        if (path.Count == 1) return Set(obj, key, value);

        var rest = path.Skip(1).ToList();
        return Set(obj, key, Set(Get(obj, key), rest, value));
    }

    public static DataMap Set(DataMap map, StringOrInt key, DataValue value) =>
        Set((DataValue)map, key, value).As<DataMap>();

    public static DataMap Set(DataMap map, IReadOnlyList<StringOrInt> path, DataValue value) =>
        Set((DataValue)map, path, value).As<DataMap>();

    /// Replaces the element at index, or extends the list (padding with nulls)
    /// when index is past the end. Unlike InsertAt this never grows a list whose
    /// index already exists.
    public static DataList SetAt(DataList list, int index, DataValue value) =>
        index < list.Count
            ? list.SetItem(index, value)
            : list.PadTo(index).Add(value);

    /// Inserts before the element at index, growing the list.
    public static DataList InsertAt(DataList list, int index, DataValue value) =>
        index <= list.Count
            ? list.Insert(index, value)
            : list.PadTo(index).Add(value);
}
