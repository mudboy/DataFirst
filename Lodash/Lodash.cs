using System.Collections.Immutable;
using System.Diagnostics;

namespace DataFirst.Lodash;

public static partial class _
{
    public static IndexedList Map<T>(object obj, Func<T, object> f) =>
        obj switch
        {
            StringMap m => m.Select(pair => f((T)pair.Value)).ToImmutableList(),
            IndexedList l => l.Select(x => f((T)x)).ToImmutableList(),
            _ => throw new Exception($"Can't Map type {obj.GetType()}")
        };

    public static ImmutableList<T> Filter<T>(ImmutableList<T> list, Func<T, bool> predicate) 
        => list.Where(predicate).ToImmutableList();
    
    public static IndexedList AggregateFields(IndexedList rows, string idFieldName, string fieldName,
        string aggregateFieldName)
    {
        var rowsByIdField = _.GroupBy(rows, idFieldName);
        var groupedRows = _.Values(rowsByIdField);
        return _.Map<IndexedList>(groupedRows, x => AggregateField(x, fieldName, aggregateFieldName));
    }
    
    public static StringMap AggregateField(IndexedList rows, string fieldName, string newName)
    {
        var aggregatedValues = _.Map<object>(rows, x => _.Get(x, fieldName));
        var firstRow = rows[0];
        var firstRowWithAggregatedValues = (StringMap)_.Set(firstRow, newName, aggregatedValues);
        return firstRowWithAggregatedValues.Remove(fieldName);
    }

    public static IndexedList Keys(object obj)
    {
        return obj switch
        { 
            StringMap m => m.Keys.ToImmutableList<object>(),
            IndexedList l => Enumerable.Range(0, l.Count)
                .Select(x => x.ToString()).ToImmutableList<object>(),
            _ => throw new Exception("Unknown type")
        };
    }

    public static bool IsObject(object obj) =>
        obj switch
        {
            StringMap => true,
            IndexedList => true,
            _ => false
        };

    public static bool IsEmpty(object obj) => 
        obj switch
        {
            StringMap m => m.IsEmpty,
            IndexedList l=> l.IsEmpty,
            _ => true
        };

    public static IndexedList Union(IndexedList l1, IndexedList l2) => l1.Union(l2).ToImmutableList();

    public static object DiffObjects(object data1, object data2)
    {
        object emptyObject = data1 is IndexedList ? 
            IndexedList.Empty : StringMap.Empty;

        if (data1 == data2) return emptyObject;

        var keys = _.Union(_.Keys(data1), _.Keys(data2));

        return keys.Aggregate(emptyObject, (acc, kObj) =>
        {
            var k = (string)kObj;
            var res = _.Diff(Get(data1, k), Get(data2, k));
            if ((IsObject(res) && IsEmpty(res)) || res.Equals("no-diff"))
            {
                return acc;
            }

            return Set(acc, k, res);
        });

    }

    public static object Diff(object data1, object data2)
    {
        if (IsObject(data1) && IsObject(data2))
            return DiffObjects(data1, data2);
        // leafs
        if (!Equals(data1, data2))
            return data2;
        return "no-diff";
    }

    public static object Reduce(object obj, Func<object, object, object, object> f, object initial)
    {
        return obj switch
        {
            StringMap m => m.Aggregate(initial, (acc, pair) => f(acc, pair.Value, pair.Key)),
            IndexedList l => l.Aggregate((initial, 0), (acc, v) => (f(acc.initial, v, acc.Item2), acc.Item2++)).initial
        };
    }
}

public static class Getter
{
    public static Getter<T> Create<T>(string key) => new KeyGetter<T>(key);
    public static Getter<T> Create<T>(List<StringOrInt> keyPath) => new PathGetter<T>(keyPath);
}

public interface Getter<out T>
{
    public T Get(StringMap map);
}

internal sealed record KeyGetter<T>(string Key) : Getter<T>
{
    public T Get(StringMap map) => _.Get<T>(map, Key);
}

internal sealed record PathGetter<T>(List<StringOrInt> keyPath) : Getter<T>
{
    public T Get(StringMap map) => _.Get<T>(map, keyPath);
} 