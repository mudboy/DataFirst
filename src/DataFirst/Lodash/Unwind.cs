namespace DataFirst.Lodash;

public static partial class _
{
    /// Expands a map holding a list at key into one map per element.
    public static DataList Unwind(DataMap map, string key)
    {
        var elements = Get<DataList>(map, key);
        return DataList.Create(elements.Select(element => (DataValue)map.SetItem(key, element)));
    }
}
