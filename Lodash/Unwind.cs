namespace DataFirst.Lodash;

public static partial class _
{
    public static IndexedList Unwind(StringMap map, string key)
    {
        var arr = Get<IndexedList>(map, key);
        return _.Map<StringMap>(arr, elem => _.Set(map, key, elem));
    }
}