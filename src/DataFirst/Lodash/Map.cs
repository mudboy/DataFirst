namespace DataFirst.Lodash;

/// Literal syntax for maps: Map.Of(("title", "Watchmen"), ("publicationYear", 1987)).
public static class Map
{
    public static DataMap Of(params (string Key, DataValue Value)[] entries) =>
        entries
            .Aggregate(DataMap.CreateBuilder(), (builder, entry) => builder.Set(entry.Key, entry.Value))
            .ToDataMap();
}
    