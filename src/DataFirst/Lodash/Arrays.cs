namespace DataFirst.Lodash;

public static partial class _
{
    /// A new list holding first's elements and then second's.
    public static DataList Concat(DataList first, DataList second) =>
        DataList.Create(first.Concat(second));

    /// Flattens one level: a list element is replaced by its own elements, anything
    /// else (maps included) is kept as it is. Deeper nesting is left alone.
    public static DataList Flatten(DataList list) =>
        DataList.Create(list.SelectMany(element =>
            element switch
            {
                DataList inner => (IEnumerable<DataValue>)inner,
                _ => [element]
            }));

    /// The distinct values of a list, each at its first position.
    public static DataList Uniq(DataList list) => DataList.Create(list.Distinct());

    /// The distinct values present in both lists, in the order of their first
    /// appearance in the first list.
    public static DataList Intersection(DataList first, DataList second) =>
        DataList.Create(first.Distinct().Where(second.Contains));

    /// The distinct values present in either list, first list's order first.
    public static DataList Union(DataList first, DataList second) => Uniq(Concat(first, second));

    /// The same for lists of keys, which is how Diff pairs up the keys of two nodes.
    public static IReadOnlyList<StringOrInt> Union(
        IReadOnlyList<StringOrInt> first, IReadOnlyList<StringOrInt> second) =>
        first.Concat(second).Distinct().ToList();

    /// The element at index n; a negative n counts back from the end, as in lodash.
    /// An index outside the list yields null rather than throwing, like GetOrNull.
    public static DataValue Nth(DataList list, int n) =>
        (n < 0 ? list.Count + n : n) is var index && index >= 0 && index < list.Count
            ? list[index]
            : DataNull.Instance;

    /// The total of a list of numbers: a long when every element is a long, otherwise
    /// a double. The sum of nothing is 0. Overflow throws rather than wrapping, and a
    /// non-number is an error rather than being skipped.
    public static DataValue Sum(DataList list) =>
        list.All(value => value is long)
            ? (DataValue)list.Aggregate(0L, (total, value) => checked(total + value.As<long>()))
            : list.All(value => value is long or double)
                ? (DataValue)list.Sum(value => value switch
                {
                    long n => (double)n,
                    double d => d,
                    _ => throw new InvalidOperationException($"Cannot Sum a {value.Describe()}")
                })
                : throw new InvalidOperationException(
                    $"Cannot Sum a list containing a {list.First(value => value is not (long or double)).Describe()}");

    /// Replaces the element at index, or extends the list (padding with nulls)
    /// when index is past the end. Unlike InsertAt this never grows a list whose
    /// index already exists.
    public static DataList SetAt(DataList list, int index, DataValue value) =>
        index < list.Count
            ? list.SetItem(index, value)
            : list.PadTo(index).Add(value);

    /// Inserts before the element at index, growing the list.
    public static DataList InsertAt(DataList list, int index, DataValue value) =>
        index <= list.Count
            ? list.Insert(index, value)
            : list.PadTo(index).Add(value);

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
