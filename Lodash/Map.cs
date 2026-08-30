namespace DataFirst.Lodash;

public static class Map
{
    public static StringMap Of(params object[] pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        
        if (pairs.Length % 2 != 0) throw new ArgumentException("must be even number of values", nameof(pairs));

        return pairs.Chunk(2).Aggregate(_.MapBuilder(), 
            (b, p) => _.Add(b, p[0].ToString()!, p[1])).ToImmutable();
    }
}