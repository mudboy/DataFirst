namespace DataFirst.Lodash;

/// Literal syntax for maps: Map.Of(("title", "Watchmen"), ("publicationYear", 1987)).
public static class Map
{
    public static DataMap Of(params (string Key, DataValue Value)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var builder = DataMap.CreateBuilder();
        foreach (var (key, value) in entries)
            builder.Set(key, value);

        return builder.ToDataMap();
    }
}
