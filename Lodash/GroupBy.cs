using System.Collections.Immutable;

namespace DataFirst.Lodash;

public static partial class _
{
    public static StringMap GroupBy<T>(object coll, Func<T, string> f)
    {
        return coll switch
        {
            StringMap m => m.GroupBy(x => f((T)x.Value))
                .Aggregate(MapBuilder(), (b, g) =>
                    Add(b, g.Key, g.ToImmutableDictionary())).ToImmutable(),
            IndexedList l => l.GroupBy(x => f((T)x))
                .Aggregate(MapBuilder(), (b, g) =>
                    Add(b, g.Key, g.ToImmutableList())).ToImmutable(),
            _ => throw new Exception($"Can't groupBy on type {coll.GetType()}")
        };
    }

    public static StringMap GroupBy(object rows, string idKey) =>
        _.GroupBy<object>(rows, x => Get<string>(x, idKey));

    internal static StringMap.Builder MapBuilder() => ImmutableDictionary.CreateBuilder<string, object>();
    
    internal static StringMap.Builder Add(StringMap.Builder b, string key, object value)
    {
        b.Add(key, value);
        return b;
    }
}