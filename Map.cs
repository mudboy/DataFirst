using System.Collections;
using System.Collections.Immutable;

namespace DataFirst;

public static class Map
{
    public static StringMap Of(params object[] pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        
        if (pairs.Length % 2 != 0) throw new ArgumentException("must be even number of values", nameof(pairs));

        return pairs.Chunk(2).Aggregate(ImmutableDictionary.CreateBuilder<string, object>(), (builder, pair) =>
        {
            builder.Add(pair[0].ToString()!, pair[1]);
            return builder;
        }).ToImmutable();
    }
}

public static partial class _
{

    public static StringMap KeyBy(StringMap map, params string[] path)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, object>();
        map.Aggregate(builder, (b, kvp) =>
        {
            var key = Get<string>((StringMap)kvp.Value, path);
            b.Add(key, kvp.Value);
            return b;
        });
        return builder.ToImmutable();
    }
    
    public static bool ContainsKey(StringMap map, string key) => map.ContainsKey(key);

    public static bool ContainsKey(StringMap map, IEnumerable<string> pathKey)
    {
        object current = map;
        var result = false;
        foreach (var key in pathKey)
        {
            result = ContainsKey((StringMap)current, key);
            if (!result)
                return false;
            current = Get((StringMap)current, key);
        }

        return result;
    }

    public static T Get<T>(StringMap map, string key) =>
        (T)Get(map, key);

    public static T Get<T>(StringMap map, IEnumerable<string> keyPath) =>
        (T)Get(map, keyPath);

    public static T Get<T>(StringMap map, params string[] keyPath) =>
        (T)Get(map, keyPath);

    public static object Get(StringMap map, string key, object? fallbackValue = default) =>
        map.TryGetValue(key, out var value) ? value : fallbackValue ?? "undefined";

    public static object Get(StringMap map, IEnumerable<object> keyPath) =>
        keyPath.Aggregate<object, object>(map, (current, key) => current switch
        {
            StringMap m when key is string k => Get(m, k),
            ImmutableList<object> l when key is int i => Get(l, i),
            ImmutableList<string> ls when key is int y => Get(ls, y),
            _ => throw new Exception($"Unknown value type {current.GetType().Name}")
        });

    public static StringMap Set(StringMap map, string key, object value) => map.SetItem(key, value);

    public static ImmutableList<object> Map(StringMap map, Func<KeyValuePair<string, object>, object> f) =>
        map.Select(f).ToImmutableList();

    public static ImmutableList<object> Map(StringMap map, string key) =>
        map.Where(x => x.Key == key).Select(x => x.Value).ToImmutableList();


    public static ImmutableDictionary<string, ImmutableList<StringMap>> GroupBy(ImmutableList<StringMap> rows,
        Func<StringMap, string> f) =>
        rows.GroupBy(f)
            .Aggregate(ImmutableDictionary.CreateBuilder<string, ImmutableList<StringMap>>(), (builder, x2) =>
            {
                builder.Add(x2.Key, x2.ToImmutableList());
                return builder;
            }).ToImmutable();

    public static ImmutableDictionary<string, ImmutableList<StringMap>> GroupBy(ImmutableList<StringMap> rows,
        string idKey) =>
        _.GroupBy(rows, x => Get<string>(x, idKey));

    public static ImmutableList<V> Values<K,V>(ImmutableDictionary<K, V> map) where K : notnull => map.Values.ToImmutableList();

    public static ImmutableList<object> Values(
        StringMap map) => map.Values.ToImmutableList();
    
    public static ImmutableList<T> Values<T>(
        StringMap map) => map.Values.Cast<T>().ToImmutableList();

    public static StringMap Update(StringMap map, string path, Func<object, object> func)
    {
        var currentValue = Get(map, path);
        var nextValue = func(currentValue);
        return Set(map, path, nextValue);
    }

    public static ImmutableList<StringMap> Unwind(StringMap map, string key)
    {
        var arr = Get<ImmutableList<StringMap>>(map, key);
        return _.Map(arr, elem => _.Set(map, key, elem));
    }
}

public static class Getter
{
    public static Getter<T> Create<T>(string key) => new KeyGetter<T>(key);
    public static Getter<T> Create<T>(IEnumerable<string> keyPath) => new PathGetter<T>(keyPath);
}

public interface Getter<out T>
{
    public T Get(StringMap map);
}

internal sealed record KeyGetter<T>(string Key) : Getter<T>
{
    public T Get(StringMap map) => _.Get<T>(map, Key);
}

internal sealed record PathGetter<T>(IEnumerable<string> keyPath) : Getter<T>
{
    public T Get(StringMap map) => _.Get<T>(map, keyPath);
} 