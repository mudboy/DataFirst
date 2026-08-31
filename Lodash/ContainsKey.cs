namespace DataFirst.Lodash;

public static partial class _
{
    /// True when a single key or index is present.
    public static bool ContainsKey(DataValue obj, StringOrInt key) =>
        (obj, key) switch
        {
            (DataMap m, string k) => m.ContainsKey(k),
            (DataList l, _) when key.TryAsIndex(out var i) => i >= 0 && i < l.Count,
            _ => false
        };

    /// True when every step of the path is present. Unlike the map-only version
    /// this walks lists too, and returns false rather than throwing when an
    /// intermediate node is a leaf.
    public static bool ContainsKey(DataValue obj, IReadOnlyList<StringOrInt> path)
    {
        if (path.Count == 0) return false;

        var current = obj;
        for (var i = 0; i < path.Count; i++)
        {
            if (!ContainsKey(current, path[i])) return false;
            if (i < path.Count - 1) current = Get(current, path[i]);
        }

        return true;
    }
}
