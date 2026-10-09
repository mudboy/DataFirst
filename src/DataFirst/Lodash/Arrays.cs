namespace DataFirst.Lodash;

public static partial class _
{
    /// <summary>
    /// Joins two lists end to end.
    /// </summary>
    /// <param name="first">The list whose elements come first.</param>
    /// <param name="second">The list whose elements follow.</param>
    /// <returns>A new list holding <paramref name="first"/>'s elements and then <paramref name="second"/>'s.</returns>
    public static DataList Concat(DataList first, DataList second) =>
        DataList.Create(first.Concat(second));

    /// <summary>
    /// Flattens one level of nesting.
    /// </summary>
    /// <remarks>
    /// A list element is replaced by its own elements; anything else (maps included)
    /// is kept as it is. Deeper nesting is left alone.
    /// </remarks>
    /// <param name="list">The list to flatten.</param>
    /// <returns>A new, flattened list.</returns>
    public static DataList Flatten(DataList list) =>
        DataList.Create(list.SelectMany(element =>
            element switch
            {
                DataList inner => (IEnumerable<DataValue>)inner,
                _ => [element]
            }));

    /// <summary>
    /// Removes duplicate values from a list.
    /// </summary>
    /// <param name="list">The list to deduplicate.</param>
    /// <returns>The distinct values of the list, each at its first position.</returns>
    public static DataList Uniq(DataList list) => DataList.Create(list.Distinct());

    /// <summary>
    /// The values present in both lists.
    /// </summary>
    /// <param name="first">The list that decides the order of the result.</param>
    /// <param name="second">The list the values must also appear in.</param>
    /// <returns>The distinct values present in both lists, in the order of their first appearance in <paramref name="first"/>.</returns>
    public static DataList Intersection(DataList first, DataList second) =>
        DataList.Create(first.Distinct().Where(second.Contains));

    /// <summary>
    /// The values present in either list.
    /// </summary>
    /// <param name="first">The list whose values come first.</param>
    /// <param name="second">The list whose remaining values follow.</param>
    /// <returns>The distinct values present in either list, <paramref name="first"/>'s order first.</returns>
    public static DataList Union(DataList first, DataList second) => Uniq(Concat(first, second));

    /// <summary>
    /// The distinct keys present in either list of keys, which is how <c>Diff</c> pairs up the keys of two nodes.
    /// </summary>
    /// <param name="first">The keys that come first.</param>
    /// <param name="second">The keys whose remaining members follow.</param>
    /// <returns>The distinct keys of both, <paramref name="first"/>'s order first.</returns>
    public static IReadOnlyList<StringOrInt> Union(
        IReadOnlyList<StringOrInt> first, IReadOnlyList<StringOrInt> second) =>
        first.Concat(second).Distinct().ToList();

    /// <summary>
    /// The element at an index; a negative index counts back from the end, as in lodash.
    /// </summary>
    /// <param name="list">The list to read from.</param>
    /// <param name="n">The index, or a negative offset from the end.</param>
    /// <returns>The element, or <see cref="DataNull"/> when the index is outside the list; it does not throw, like <c>GetOrNull</c>.</returns>
    public static DataValue Nth(DataList list, int n) =>
        (n < 0 ? list.Count + n : n) is var index && index >= 0 && index < list.Count
            ? list[index]
            : DataNull.Instance;

    /// <summary>
    /// The total of a list of numbers.
    /// </summary>
    /// <remarks>
    /// The result is a long when every element is a long, otherwise a double. The sum
    /// of nothing is 0. Overflow throws rather than wrapping, and a non-number is an
    /// error rather than being skipped.
    /// </remarks>
    /// <param name="list">The numbers to add up.</param>
    /// <returns>The sum, as a long or a double.</returns>
    /// <exception cref="InvalidOperationException">An element is not a number.</exception>
    /// <exception cref="OverflowException">A sum of longs does not fit in a long.</exception>
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

    /// <summary>
    /// Replaces the element at an index, or extends the list when the index is past the end.
    /// </summary>
    /// <remarks>
    /// Past the end the list is padded with nulls up to the index. Unlike
    /// <see cref="InsertAt"/> this never grows a list whose index already exists.
    /// </remarks>
    /// <param name="list">The list to change.</param>
    /// <param name="index">The index to write.</param>
    /// <param name="value">The value to write.</param>
    /// <returns>A new list with the value written; <paramref name="list"/> is not modified.</returns>
    public static DataList SetAt(DataList list, int index, DataValue value) =>
        index < list.Count
            ? list.SetItem(index, value)
            : list.PadTo(index).Add(value);

    /// <summary>
    /// Inserts a value before the element at an index, growing the list.
    /// </summary>
    /// <remarks>
    /// An index past the end pads the list with nulls up to the index, then appends.
    /// </remarks>
    /// <param name="list">The list to change.</param>
    /// <param name="index">The index to insert before.</param>
    /// <param name="value">The value to insert.</param>
    /// <returns>A new, longer list; <paramref name="list"/> is not modified.</returns>
    public static DataList InsertAt(DataList list, int index, DataValue value) =>
        index <= list.Count
            ? list.Insert(index, value)
            : list.PadTo(index).Add(value);

    /// <summary>
    /// Collapses rows sharing an id into one row, gathering a field into a list.
    /// </summary>
    /// <param name="rows">The rows, each a map.</param>
    /// <param name="idFieldName">The field whose value says which rows belong together.</param>
    /// <param name="fieldName">The field whose values are gathered.</param>
    /// <param name="aggregateFieldName">The name of the list field that replaces <paramref name="fieldName"/> in each collapsed row.</param>
    /// <returns>One row per distinct id, in order of first appearance, each taken from the group's first row.</returns>
    public static DataList AggregateFields(
        DataList rows, string idFieldName, string fieldName, string aggregateFieldName)
    {
        var rowsByIdField = GroupBy(rows, idFieldName);
        var groupedRows = Values(rowsByIdField);
        return Map(groupedRows, group => AggregateField(group.As<DataList>(), fieldName, aggregateFieldName));
    }

    /// <summary>
    /// Collapses one group of rows into a single row, gathering a field into a list.
    /// </summary>
    /// <param name="rows">The rows of one group. There must be at least one.</param>
    /// <param name="fieldName">The field whose values are gathered.</param>
    /// <param name="newName">The name of the list field that replaces <paramref name="fieldName"/>.</param>
    /// <returns>The first row, with <paramref name="fieldName"/> removed and <paramref name="newName"/> holding every row's value of it.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rows"/> is empty.</exception>
    public static DataMap AggregateField(DataList rows, string fieldName, string newName)
    {
        var aggregatedValues = Map(rows, row => Get(row, fieldName));
        var firstRow = rows[0].As<DataMap>();
        return firstRow.SetItem(newName, aggregatedValues).Remove(fieldName);
    }
}
