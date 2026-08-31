namespace DataFirst.Lodash;

public static partial class _
{
    public static StringMap Set(StringMap map, string key, object value) => map.SetItem(key, value);

    public static object Set(object obj, List<StringOrInt> path, object v) =>
        path switch
        {
            [] => obj,
            [.. var p] => DoSet(obj, p, v)
        };

    public static object Set(object obj, StringOrInt key, object v) => DoSet(obj, [key], v);

    private static object DoSet(object obj, List<StringOrInt> path, object v)
    {
        var (k, restOfPath) = path switch { [var h, .. var rest] => (h, rest) };
        var modifiedNode = v;
        if (restOfPath.Count > 0)
        {
            modifiedNode = Set(Get(obj, k), restOfPath, v);
        }

        return (obj, k) switch
        {
            (StringMap m, string key) => m.SetItem(key, modifiedNode),
            (IndexedList l, int idx) => SetAt(l, idx, modifiedNode),
            (IndexedList l, string si) when int.TryParse(si, out var ii) => SetAt(l, ii, modifiedNode),
            _ => throw new Exception($"Can't set value of type {obj.GetType()} with key of type {k.GetType()}")
        };
    }
}
