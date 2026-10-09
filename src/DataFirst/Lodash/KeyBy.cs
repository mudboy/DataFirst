namespace DataFirst.Lodash;

public static partial class _
{
    /// Indexes a list of maps by one of their fields. Last write wins on
    /// duplicate keys, as lodash does.
    public static DataMap KeyBy(DataList maps, string key)
    {
        var builder = DataMap.CreateBuilder();
        foreach (var map in maps) builder.Set(Get<string>(map, key), map);
        return builder.ToDataMap();
    }
}
