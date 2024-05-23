using System.Collections.Immutable;

namespace DataFirst;

public static class List
{
    public static ImmutableList<T> Of<T>(params T[] values)
    {
        return ImmutableList.Create(values);
    }
}

public static partial class _
{
    public static T Get<T>(ImmutableList<T> list, int index) => list[index];

    public static ImmutableList<T> Set<T>(ImmutableList<T> list, int index, T value) => list.SetItem(index, value);
    
    public static ImmutableList<R> Map<T, R>(ImmutableList<T> list, Func<T, R> f) =>
        list.Select(f).ToImmutableList();

    public static StringMap KeyBy(ImmutableList<StringMap> maps, string key) =>
        maps.Aggregate(ImmutableDictionary.CreateBuilder<string,object>(), (builder, obj) =>
        {
            builder[Get<string>(obj, key)] = obj;
            return builder;
        }).ToImmutable();

    public static ImmutableList<T> Filter<T>(ImmutableList<T> list, Func<T, bool> predicate) 
        => list.Where(predicate).ToImmutableList();
    
    public static ImmutableList<StringMap> AggregateFields(ImmutableList<StringMap> rows, string idFieldName, string fieldName,
        string aggregateFieldName)
    {
        var rowsByIdField = _.GroupBy(rows, idFieldName);
        var groupedRows = _.Values(rowsByIdField);
        return _.Map(groupedRows, x => AggregateField(x, fieldName, aggregateFieldName));
    }
    
    public static StringMap AggregateField(ImmutableList<StringMap> rows, string fieldName, string newName)
    {
        var aggregatedValues = _.Map(rows, x => _.Get(x, fieldName));
        var firstRow = rows[0];
        var firstRowWithAggregatedValues = _.Set(firstRow, newName, aggregatedValues);
        return firstRowWithAggregatedValues.Remove(fieldName);
    }
}