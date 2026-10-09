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

    /// Maps over a list's values or a map's values, always producing a list
    /// (as lodash does).
    public static DataList Map(DataValue coll, Func<DataValue, DataValue> f) =>
        coll switch
        {
            DataMap m => DataList.Create(m.Values.Select(f)),
            DataList l => DataList.Create(l.Select(f)),
            _ => throw new InvalidOperationException($"Cannot Map over a {coll.Describe()}")
        };

    /// The elements of a list, or the values of a map, for which the predicate holds,
    /// always as a list (as lodash does).
    public static DataList Filter(DataValue coll, Func<DataValue, bool> predicate) =>
        DataList.Create(Elements(coll, "Filter").Where(predicate));

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

    /// Folds over a list's values (with each index) or a map's values (with each key).
    public static TAcc Reduce<TAcc>(DataValue coll, Func<TAcc, DataValue, StringOrInt, TAcc> f, TAcc initial) =>
        coll switch
        {
            DataMap m => m.Aggregate(initial, (acc, pair) => f(acc, pair.Value, pair.Key)),
            DataList l => l.Select((value, index) => (value, index))
                .Aggregate(initial, (acc, item) => f(acc, item.value, item.index)),
            _ => throw new InvalidOperationException($"Cannot Reduce a {coll.Describe()}")
        };

    public static DataMap GroupBy(DataValue coll, Func<DataValue, string> f)
    {
        var builder = DataMap.CreateBuilder();

        switch (coll)
        {
            case DataMap m:
                foreach (var group in m.Values.GroupBy(f))
                    builder.Set(group.Key, DataList.Create(group));
                break;
            case DataList l:
                foreach (var group in l.GroupBy(f))
                    builder.Set(group.Key, DataList.Create(group));
                break;
            default:
                throw new InvalidOperationException($"Cannot GroupBy a {coll.Describe()}");
        }

        return builder.ToDataMap();
    }

    public static DataMap GroupBy(DataValue coll, string idKey) =>
        GroupBy(coll, row => Get<string>(row, idKey));

    /// Indexes a list's elements, or a map's values, by the key f gives each. Last
    /// write wins on duplicate keys, as lodash does, and a key keeps the position of
    /// its first appearance.
    public static DataMap KeyBy(DataValue coll, Func<DataValue, string> f) =>
        Elements(coll, "KeyBy")
            .Aggregate(DataMap.CreateBuilder(), (builder, element) => builder.Set(f(element), element))
            .ToDataMap();

    /// Indexes a list of maps by one of their fields.
    public static DataMap KeyBy(DataValue coll, string key) =>
        KeyBy(coll, row => Get<string>(row, key));

    /// Diffs two nodes. Returns NoDiff when they are equivalent, otherwise the change:
    /// for composites that is a nested structure holding only the differing leaves,
    /// for leaves it is the new value.
    public static DiffResult Diff(DataValue data1, DataValue data2)
    {
        if (IsObject(data1) && IsObject(data2))
        {
            var diffed = DiffObjects(data1, data2);
            return IsEmpty(diffed) ? NoDiff.Instance : new Changed(diffed);
        }

        // leafs
        return data1.Equals(data2) ? NoDiff.Instance : new Changed(data2);
    }

    /// Diffs two composites, returning a map holding only what differs. An empty
    /// result means the two are equivalent.
    ///
    /// A diff is always a map, even when diffing lists -- list indices become string
    /// keys. Mirroring the list's shape instead would have to pad the unchanged slots,
    /// and that padding is indistinguishable from an element genuinely changed to null,
    /// which makes any merge over it silently wrong. Index keys carry only what changed.
    ///
    /// A key present on only one side diffs against null, so additions show up as the
    /// new value and removals as null.
    public static DataMap DiffObjects(DataValue data1, DataValue data2)
    {
        if (ReferenceEquals(data1.Unwrap(), data2.Unwrap())) return DataMap.Empty;

        var keys = Union(KeysOrEmpty(data1), KeysOrEmpty(data2));
        var diff = DataMap.CreateBuilder();

        foreach (var key in keys)
            if (Diff(GetOrNull(data1, key), GetOrNull(data2, key)) is Changed(var value))
                diff.Set(KeyName(key), value);

        return diff.ToDataMap();
    }

    /// A leaf -- most usefully a null -- contributes no keys, so diffing an aggregate
    /// that does not exist yet against its first value reports every key as added.
    /// That makes creating an aggregate the same operation as changing one.
    private static IReadOnlyList<StringOrInt> KeysOrEmpty(DataValue value) =>
        IsObject(value) ? Keys(value) : [];

    /// List indices address a map as their string form, which Get and Set accept
    /// on the way back into a list.
    private static string KeyName(StringOrInt key) =>
        key switch { string s => s, int i => i.ToString() };

    /// Every root-to-leaf path in a structure.
    ///
    /// Applied to a diff, this is the set of locations that diff touches -- which is
    /// what decides whether two concurrent changes conflict.
    public static IReadOnlyList<DataPath> InformationPaths(DataValue value) =>
        Collect(value, DataPath.Root, []);

    /// The paths a diff touches.
    ///
    /// An empty diff touches nothing. That is not what InformationPaths says, which
    /// reports the root of an empty map as a touched location -- correct for data
    /// (setting a field to {} is a change), wrong for a diff (no change at all). The
    /// difference matters once overlap is prefix-aware, because the root path is a
    /// prefix of everything and would collide with every concurrent write.
    public static IReadOnlyList<DataPath> ChangedPaths(DataMap diff) =>
        diff.IsEmpty ? [] : InformationPaths(diff);

    private static List<DataPath> Collect(DataValue value, DataPath path, List<DataPath> acc)
    {
        // An empty composite is a leaf: there is nothing inside it to descend to,
        // and it still marks this location as touched.
        if (!IsObject(value) || IsEmpty(value))
        {
            acc.Add(path);
            return acc;
        }

        foreach (var key in Keys(value)) Collect(Get(value, key), path.Then(key), acc);
        return acc;
    }
}
