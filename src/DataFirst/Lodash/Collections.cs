namespace DataFirst.Lodash;

public static partial class _
{
    /// What the collection functions iterate: a list's elements, or a map's values.
    /// Anything else is not a collection.
    private static IEnumerable<DataValue> Elements(DataValue coll, string operation) =>
        coll switch
        {
            DataMap map => map.Values,
            DataList list => list,
            _ => throw new InvalidOperationException($"Cannot {operation} a {coll.Describe()}")
        };

    /// True when the predicate holds for every element. An empty collection satisfies
    /// it vacuously.
    public static bool Every(DataValue coll, Func<DataValue, bool> predicate) =>
        Elements(coll, "Every over").All(predicate);

    /// The first element for which the predicate holds, or null when none does.
    /// Null is also what a matching null element looks like, so a caller searching a
    /// collection that can hold nulls should use Filter instead.
    public static DataValue Find(DataValue coll, Func<DataValue, bool> predicate) =>
        Elements(coll, "Find in").Cast<DataValue?>().FirstOrDefault(element => predicate(element!.Value))
        ?? (DataValue)DataNull.Instance;

    /// Calls f on each element, in order, and hands the collection back so the call
    /// can sit in the middle of a pipeline.
    public static DataValue ForEach(DataValue coll, Action<DataValue> f)
    {
        foreach (var element in Elements(coll, "ForEach over")) f(element);
        return coll;
    }

    /// The number of elements of a list or entries of a map.
    public static int Size(DataValue coll) =>
        coll switch
        {
            DataMap map => map.Count,
            DataList list => list.Count,
            _ => throw new InvalidOperationException($"Cannot take the Size of a {coll.Describe()}")
        };

    public static bool IsArray(DataValue value) => value is DataList;

    /// Deep comparison. Maps compare regardless of key order, lists by position.
    ///
    /// It is the data's own equality, so it tells a long from a double: 1 and 1.0 are
    /// different values here, as they are to Diff.
    public static bool IsEqual(DataValue first, DataValue second) => first.Equals(second);

    /// The elements sorted ascending by the key f gives each. The sort is stable --
    /// elements with equal keys keep their original order -- and f runs once per
    /// element.
    ///
    /// Keys are ordered by kind first: null, then booleans (false first), then
    /// numbers, then strings. Numbers compare by value, a long against a double
    /// included, and strings ordinally. A map or list is not a sortable key.
    public static DataList SortBy(DataValue coll, Func<DataValue, DataValue> f) =>
        DataList.Create(Elements(coll, "SortBy")
            .Select(element => (Element: element, Key: Sortable(f(element))))
            .OrderBy(pair => pair.Key, SortKeyOrder)
            .Select(pair => pair.Element));

    /// Keys are checked as they are computed, not left to the comparer: a sort wraps
    /// whatever its comparer throws, and never calls it at all for a single element.
    private static DataValue Sortable(DataValue key) =>
        key.IsComposite() ? throw NotSortable(key) : key;

    /// Sorts maps by one of their fields.
    public static DataList SortBy(DataValue coll, string field) =>
        SortBy(coll, row => Get(row, field));

    private static readonly Comparer<DataValue> SortKeyOrder = Comparer<DataValue>.Create(CompareSortKeys);

    private static int CompareSortKeys(DataValue first, DataValue second) =>
        (first, second) switch
        {
            (DataMap or DataList, _) => throw NotSortable(first),
            (_, DataMap or DataList) => throw NotSortable(second),
            (long a, long b) => a.CompareTo(b),
            (long or double, long or double) => AsDouble(first).CompareTo(AsDouble(second)),
            (string a, string b) => string.CompareOrdinal(a, b),
            (bool a, bool b) => a.CompareTo(b),
            _ => SortRank(first).CompareTo(SortRank(second))
        };

    private static InvalidOperationException NotSortable(DataValue key) =>
        new($"Cannot sort by a {key.Describe()}; sort keys must be null, bool, number or string");

    private static double AsDouble(DataValue number) =>
        number switch
        {
            long n => n,
            double d => d,
            _ => throw new InvalidOperationException($"{number.Describe()} is not a number")
        };

    private static int SortRank(DataValue key) =>
        key switch
        {
            DataNull => 0,
            bool => 1,
            long or double => 2,
            string => 3,
            _ => throw NotSortable(key)
        };
}
