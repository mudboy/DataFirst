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
}
