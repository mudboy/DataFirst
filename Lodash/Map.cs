namespace DataFirst.Lodash;

/// Literal syntax for maps: Map.Of("title", "Watchmen", "publicationYear", 1987).
public static class Map
{
    public static DataMap Of(params DataValue[] keysAndValues)
    {
        ArgumentNullException.ThrowIfNull(keysAndValues);

        if (keysAndValues.Length % 2 != 0)
            throw new ArgumentException(
                "must be an even number of values (alternating key and value)", nameof(keysAndValues));

        var builder = DataMap.CreateBuilder();
        for (var i = 0; i < keysAndValues.Length; i += 2)
        {
            if (keysAndValues[i] is not string key)
                throw new ArgumentException(
                    $"key at position {i} must be a string, but was {keysAndValues[i].Describe()}",
                    nameof(keysAndValues));

            builder.Set(key, keysAndValues[i + 1]);
        }

        return builder.ToDataMap();
    }
}
