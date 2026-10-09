namespace DataFirst.Lodash;

/// <summary>
/// Literal syntax for maps.
/// </summary>
public static class Map
{
    /// <summary>
    /// Builds a map from its entries.
    /// </summary>
    /// <remarks>
    /// A repeated key keeps its original position and takes the last value.
    /// </remarks>
    /// <param name="entries">The key and value pairs, in order.</param>
    /// <returns>A new map.</returns>
    /// <example>
    /// <code>
    /// Map.Of(("title", "Watchmen"), ("publicationYear", 1987))
    /// </code>
    /// </example>
    public static DataMap Of(params (string Key, DataValue Value)[] entries) =>
        entries
            .Aggregate(DataMap.CreateBuilder(), (builder, entry) => builder.Set(entry.Key, entry.Value))
            .ToDataMap();
}
