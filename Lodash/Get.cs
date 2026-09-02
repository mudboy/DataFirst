namespace DataFirst.Lodash;

public static partial class _
{
    /// Reads the value at a single key or index.
    public static DataValue Get(DataValue obj, StringOrInt key) =>
        (obj, key) switch
        {
            (DataMap m, string k) => m[k],
            (DataList l, _) when key.TryAsIndex(out var i) => l[i],
            (DataMap, int i) => throw new InvalidOperationException(
                $"Cannot index a map with {i}; maps are keyed by string"),
            (DataList, string s) => throw new InvalidOperationException(
                $"Cannot index a list with key '{s}'; list indices must be numeric"),
            _ => throw new InvalidOperationException(
                $"Cannot read {key.Describe()} from a {obj.Describe()}")
        };

    /// Walks a path of keys and indices. An empty path returns the value unchanged.
    public static DataValue Get(DataValue obj, IReadOnlyList<StringOrInt> path)
    {
        var current = obj;
        foreach (var key in path) current = Get(current, key);
        return current;
    }

    /// Like Get, but yields null rather than throwing when the key is absent.
    public static DataValue GetOrNull(DataValue obj, StringOrInt key) =>
        ContainsKey(obj, key) ? Get(obj, key) : DataNull.Instance;

    /// Like Get for a path, yielding null rather than throwing when any step of it
    /// is absent. An empty path addresses the value itself.
    public static DataValue GetOrNull(DataValue obj, IReadOnlyList<StringOrInt> path)
    {
        if (path.Count == 0) return obj;
        return ContainsKey(obj, path) ? Get(obj, path) : DataNull.Instance;
    }

    public static T Get<T>(DataValue obj, StringOrInt key) => Get(obj, key).As<T>();

    public static T Get<T>(DataValue obj, IReadOnlyList<StringOrInt> path) => Get(obj, path).As<T>();
}
