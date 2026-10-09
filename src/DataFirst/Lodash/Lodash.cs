namespace DataFirst.Lodash;

public static partial class _
{
    /// Maps over a list's values or a map's values, always producing a list
    /// (as lodash does).
    public static DataList Map(DataValue coll, Func<DataValue, DataValue> f) =>
        coll switch
        {
            DataMap m => DataList.Create(m.Values.Select(f)),
            DataList l => DataList.Create(l.Select(f)),
            _ => throw new InvalidOperationException($"Cannot Map over a {coll.Describe()}")
        };

    public static DataList Filter(DataList list, Func<DataValue, bool> predicate) =>
        DataList.Create(list.Where(predicate));

    /// The keys of a map, or the indices of a list.
    public static IReadOnlyList<StringOrInt> Keys(DataValue obj) =>
        obj switch
        {
            DataMap m => m.Keys.Select(k => (StringOrInt)k).ToList(),
            DataList l => Enumerable.Range(0, l.Count).Select(i => (StringOrInt)i).ToList(),
            _ => throw new InvalidOperationException($"A {obj.Describe()} has no keys")
        };

    /// True for the composite cases -- the things a path can descend into.
    public static bool IsObject(DataValue obj) => obj.IsComposite();

    public static bool IsEmpty(DataValue obj) =>
        obj switch
        {
            DataMap m => m.IsEmpty,
            DataList l => l.IsEmpty,
            _ => true
        };

    public static IReadOnlyList<StringOrInt> Union(
        IReadOnlyList<StringOrInt> first, IReadOnlyList<StringOrInt> second) =>
        first.Concat(second).Distinct().ToList();

    /// Folds over a list's values (with each index) or a map's values (with each key).
    public static TAcc Reduce<TAcc>(DataValue coll, Func<TAcc, DataValue, StringOrInt, TAcc> f, TAcc initial) =>
        coll switch
        {
            DataMap m => m.Aggregate(initial, (acc, pair) => f(acc, pair.Value, pair.Key)),
            DataList l => l.Select((value, index) => (value, index))
                .Aggregate(initial, (acc, item) => f(acc, item.value, item.index)),
            _ => throw new InvalidOperationException($"Cannot Reduce a {coll.Describe()}")
        };

    /// Collapses rows sharing an id into one row, gathering fieldName into a list.
    public static DataList AggregateFields(
        DataList rows, string idFieldName, string fieldName, string aggregateFieldName)
    {
        var rowsByIdField = GroupBy(rows, idFieldName);
        var groupedRows = Values(rowsByIdField);
        return Map(groupedRows, group => AggregateField(group.As<DataList>(), fieldName, aggregateFieldName));
    }

    public static DataMap AggregateField(DataList rows, string fieldName, string newName)
    {
        var aggregatedValues = Map(rows, row => Get(row, fieldName));
        var firstRow = rows[0].As<DataMap>();
        return firstRow.SetItem(newName, aggregatedValues).Remove(fieldName);
    }
}
