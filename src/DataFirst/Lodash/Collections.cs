namespace DataFirst.Lodash;

public static partial class _
{
    /// <summary>
    /// What the collection functions iterate: a list's elements, or a map's values.
    /// Anything else is not a collection.
    /// </summary>
    private static IEnumerable<DataValue> Elements(DataValue coll, string operation) =>
        coll switch
        {
            DataMap map => map.Values,
            DataList list => list,
            _ => throw new InvalidOperationException($"Cannot {operation} a {coll.Describe()}")
        };

    /// <summary>
    /// Tests whether a predicate holds for every element.
    /// </summary>
    /// <param name="coll">A list, or a map whose values are tested.</param>
    /// <param name="predicate">The test applied to each element.</param>
    /// <returns>True when the predicate holds for every element; an empty collection satisfies it vacuously.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="coll"/> is neither a list nor a map.</exception>
    /// <example>
    /// <code>
    /// _.Every(List.Of(2, 4), n => n.As&lt;long&gt;() % 2 == 0)   // true
    /// _.Every(List.Of(), n => false)                          // true: nothing to contradict it
    /// </code>
    /// </example>
    public static bool Every(DataValue coll, Func<DataValue, bool> predicate) =>
        Elements(coll, "Every over").All(predicate);

    /// <summary>
    /// Finds the first element for which a predicate holds.
    /// </summary>
    /// <remarks>
    /// Null is also what a matching null element looks like, so a caller searching a
    /// collection that can hold nulls should use <see cref="Filter"/> instead.
    /// </remarks>
    /// <param name="coll">A list, or a map whose values are searched.</param>
    /// <param name="predicate">The test applied to each element.</param>
    /// <returns>The first matching element, or <see cref="DataNull"/> when none matches.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="coll"/> is neither a list nor a map.</exception>
    /// <example>
    /// <code>
    /// _.Find(List.Of(1, 2, 3), n => n.As&lt;long&gt;() > 1)    // 2
    /// _.Find(List.Of(1, 2, 3), n => n.As&lt;long&gt;() > 9)    // null
    /// </code>
    /// </example>
    public static DataValue Find(DataValue coll, Func<DataValue, bool> predicate) =>
        Elements(coll, "Find in").Cast<DataValue?>().FirstOrDefault(element => predicate(element!.Value))
        ?? (DataValue)DataNull.Instance;

    /// <summary>
    /// Calls an action on each element, in order.
    /// </summary>
    /// <param name="coll">A list, or a map whose values are visited.</param>
    /// <param name="f">The action to run for each element.</param>
    /// <returns><paramref name="coll"/> itself, so the call can sit in the middle of a pipeline.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="coll"/> is neither a list nor a map.</exception>
    /// <example>
    /// <code>
    /// _.ForEach(List.Of("a", "b"), item => Console.WriteLine(item));   // prints a, then b
    /// </code>
    /// </example>
    public static DataValue ForEach(DataValue coll, Action<DataValue> f)
    {
        foreach (var element in Elements(coll, "ForEach over")) f(element);
        return coll;
    }

    /// <summary>
    /// Counts the elements of a list or the entries of a map.
    /// </summary>
    /// <param name="coll">A list or a map.</param>
    /// <returns>The number of elements or entries.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="coll"/> is neither a list nor a map.</exception>
    /// <example>
    /// <code>
    /// _.Size(List.Of("a", "b"))        // 2
    /// _.Size(Map.Of(("a", 1)))         // 1
    /// </code>
    /// </example>
    public static int Size(DataValue coll) =>
        coll switch
        {
            DataMap map => map.Count,
            DataList list => list.Count,
            _ => throw new InvalidOperationException($"Cannot take the Size of a {coll.Describe()}")
        };

    /// <summary>
    /// Tests whether a value is a list.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns>True when the value is a <see cref="DataList"/>.</returns>
    /// <example>
    /// <code>
    /// _.IsArray(List.Of())    // true
    /// _.IsArray(Map.Of())     // false
    /// </code>
    /// </example>
    public static bool IsArray(DataValue value) => value is DataList;

    /// <summary>
    /// Deep comparison of two values.
    /// </summary>
    /// <remarks>
    /// Maps compare regardless of key order, lists by position. It is the data's own
    /// equality, so it tells a long from a double: 1 and 1.0 are different values
    /// here, as they are to <see cref="Diff"/>.
    /// </remarks>
    /// <param name="first">The first value.</param>
    /// <param name="second">The second value.</param>
    /// <returns>True when the two are equal.</returns>
    /// <example>
    /// <code>
    /// _.IsEqual(Map.Of(("a", 1), ("b", 2)), Map.Of(("b", 2), ("a", 1)))   // true: key order is ignored
    /// _.IsEqual(1L, 1.0)                                                  // false: a long is not a double
    /// </code>
    /// </example>
    public static bool IsEqual(DataValue first, DataValue second) => first.Equals(second);

    /// <summary>
    /// Sorts elements ascending by a key computed for each.
    /// </summary>
    /// <remarks>
    /// The sort is stable (elements with equal keys keep their original order) and
    /// <paramref name="f"/> runs once per element.
    /// <para>
    /// Keys are ordered by kind first: null, then booleans (false first), then
    /// numbers, then strings. Numbers compare by value, a long against a double
    /// included, and strings ordinally. A map or list is not a sortable key.
    /// </para>
    /// </remarks>
    /// <param name="coll">A list, or a map whose values are sorted.</param>
    /// <param name="f">Computes the sort key of an element.</param>
    /// <returns>A new list of the elements in key order.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="coll"/> is not a collection, or a key is a map or list.</exception>
    /// <example>
    /// <code>
    /// _.SortBy(List.Of(3, 1, 2), n => n)                               // [1, 2, 3]
    /// _.SortBy(books, book => _.Get(book, "publicationYear"))          // oldest first
    /// </code>
    /// </example>
    public static DataList SortBy(DataValue coll, Func<DataValue, DataValue> f) =>
        DataList.Create(Elements(coll, "SortBy")
            .Select(element => (Element: element, Key: Sortable(f(element))))
            .OrderBy(pair => pair.Key, SortKeyOrder)
            .Select(pair => pair.Element));

    /// <summary>
    /// Keys are checked as they are computed, not left to the comparer: a sort wraps
    /// whatever its comparer throws, and never calls it at all for a single element.
    /// </summary>
    private static DataValue Sortable(DataValue key) =>
        key.IsComposite() ? throw NotSortable(key) : key;

    /// <summary>
    /// Sorts maps by one of their fields.
    /// </summary>
    /// <param name="coll">A list of maps, or a map whose values are maps.</param>
    /// <param name="field">The field whose value is the sort key.</param>
    /// <returns>A new list of the elements in key order.</returns>
    /// <exception cref="KeyNotFoundException">An element has no such field.</exception>
    /// <inheritdoc cref="SortBy(DataValue, Func{DataValue, DataValue})" path="/remarks"/>
    /// <example>
    /// <code>
    /// _.SortBy(books, "title")   // alphabetical by title
    /// </code>
    /// </example>
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

    /// <summary>
    /// Applies a function to each element, always producing a list (as lodash does).
    /// </summary>
    /// <param name="coll">A list, or a map whose values are mapped.</param>
    /// <param name="f">Computes the new value for an element.</param>
    /// <returns>A list of the results, in order.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="coll"/> is neither a list nor a map.</exception>
    /// <example>
    /// <code>
    /// _.Map(List.Of(1, 2, 3), n => n.As&lt;long&gt;() * 2)             // [2, 4, 6]
    /// _.Map(Map.Of(("a", 1), ("b", 2)), n => n.As&lt;long&gt;() * 2)   // [2, 4]: a map yields a list
    /// </code>
    /// </example>
    public static DataList Map(DataValue coll, Func<DataValue, DataValue> f) =>
        coll switch
        {
            DataMap m => DataList.Create(m.Values.Select(f)),
            DataList l => DataList.Create(l.Select(f)),
            _ => throw new InvalidOperationException($"Cannot Map over a {coll.Describe()}")
        };

    /// <summary>
    /// Keeps the elements for which a predicate holds, always as a list (as lodash does).
    /// </summary>
    /// <param name="coll">A list, or a map whose values are filtered.</param>
    /// <param name="predicate">The test applied to each element.</param>
    /// <returns>A list of the elements that passed, in order.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="coll"/> is neither a list nor a map.</exception>
    /// <example>
    /// <code>
    /// _.Filter(List.Of(1, 2, 3, 4), n => n.As&lt;long&gt;() > 2)   // [3, 4]
    /// </code>
    /// </example>
    public static DataList Filter(DataValue coll, Func<DataValue, bool> predicate) =>
        DataList.Create(Elements(coll, "Filter").Where(predicate));

    /// <summary>
    /// Lists the keys of a map, or the indices of a list.
    /// </summary>
    /// <param name="obj">A map or a list.</param>
    /// <returns>The keys in insertion order, or the indices <c>0..n-1</c>.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="obj"/> is neither a map nor a list.</exception>
    /// <example>
    /// <code>
    /// _.Keys(Map.Of(("a", 1), ("b", 2)))   // "a", "b"
    /// _.Keys(List.Of("x", "y"))            // 0, 1
    /// </code>
    /// </example>
    public static IReadOnlyList<StringOrInt> Keys(DataValue obj) =>
        obj switch
        {
            DataMap m => m.Keys.Select(k => (StringOrInt)k).ToList(),
            DataList l => Enumerable.Range(0, l.Count).Select(i => (StringOrInt)i).ToList(),
            _ => throw new InvalidOperationException($"A {obj.Describe()} has no keys")
        };

    /// <summary>
    /// Tests whether a value is composite: the things a path can descend into.
    /// </summary>
    /// <param name="obj">The value to test.</param>
    /// <returns>True for a map or a list.</returns>
    /// <example>
    /// <code>
    /// _.IsObject(Map.Of())    // true
    /// _.IsObject("text")      // false
    /// </code>
    /// </example>
    public static bool IsObject(DataValue obj) => obj.IsComposite();

    /// <summary>
    /// Tests whether a value is empty.
    /// </summary>
    /// <param name="obj">The value to test.</param>
    /// <returns>True for an empty map or list. Any other value, null and scalars included, counts as empty.</returns>
    /// <example>
    /// <code>
    /// _.IsEmpty(List.Of())            // true
    /// _.IsEmpty(Map.Of(("a", 1)))     // false
    /// </code>
    /// </example>
    public static bool IsEmpty(DataValue obj) =>
        obj switch
        {
            DataMap m => m.IsEmpty,
            DataList l => l.IsEmpty,
            _ => true
        };

    /// <summary>
    /// Folds a collection into a single value.
    /// </summary>
    /// <typeparam name="TAcc">The type of the accumulated value.</typeparam>
    /// <param name="coll">A list, folded with each element's index, or a map, folded with each value's key.</param>
    /// <param name="f">Combines the accumulator, an element and its index or key into the next accumulator.</param>
    /// <param name="initial">The starting accumulator.</param>
    /// <returns>The final accumulator; <paramref name="initial"/> for an empty collection.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="coll"/> is neither a list nor a map.</exception>
    /// <example>
    /// <code>
    /// _.Reduce(List.Of(1, 2, 3), (long total, DataValue n, StringOrInt index) => total + n.As&lt;long&gt;(), 0L)   // 6
    /// </code>
    /// </example>
    public static TAcc Reduce<TAcc>(DataValue coll, Func<TAcc, DataValue, StringOrInt, TAcc> f, TAcc initial) =>
        coll switch
        {
            DataMap m => m.Aggregate(initial, (acc, pair) => f(acc, pair.Value, pair.Key)),
            DataList l => l.Select((value, index) => (value, index))
                .Aggregate(initial, (acc, item) => f(acc, item.value, item.index)),
            _ => throw new InvalidOperationException($"Cannot Reduce a {coll.Describe()}")
        };

    /// <summary>
    /// Groups elements by a key computed for each.
    /// </summary>
    /// <param name="coll">A list, or a map whose values are grouped.</param>
    /// <param name="f">Computes the group key of an element.</param>
    /// <returns>A map from each key to the list of elements that produced it, in order of first appearance.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="coll"/> is neither a list nor a map.</exception>
    /// <example>
    /// <code>
    /// _.GroupBy(List.Of("apple", "avocado", "banana"), s => s.As&lt;string&gt;()[..1])
    /// // { a: ["apple", "avocado"], b: ["banana"] }
    /// </code>
    /// </example>
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

    /// <summary>
    /// Groups maps by the string value of one of their fields.
    /// </summary>
    /// <param name="coll">A list of maps, or a map whose values are maps.</param>
    /// <param name="idKey">The field whose value is the group key.</param>
    /// <returns>A map from each field value to the list of elements that held it.</returns>
    /// <exception cref="KeyNotFoundException">An element has no such field.</exception>
    /// <exception cref="InvalidOperationException">An element's field is not a string.</exception>
    /// <example>
    /// <code>
    /// _.GroupBy(rows, "isbn")   // { "1": [ ...rows with isbn "1" ], "2": [ ... ] }
    /// </code>
    /// </example>
    public static DataMap GroupBy(DataValue coll, string idKey) =>
        GroupBy(coll, row => Get<string>(row, idKey));

    /// <summary>
    /// Indexes elements by a key computed for each.
    /// </summary>
    /// <remarks>
    /// Last write wins on duplicate keys, as lodash does, and a key keeps the position
    /// of its first appearance.
    /// </remarks>
    /// <param name="coll">A list, or a map whose values are indexed.</param>
    /// <param name="f">Computes the key of an element.</param>
    /// <returns>A map from each key to its element.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="coll"/> is neither a list nor a map.</exception>
    /// <example>
    /// <code>
    /// _.KeyBy(List.Of("apple", "banana"), s => s.As&lt;string&gt;()[..1])   // { a: "apple", b: "banana" }
    /// </code>
    /// </example>
    public static DataMap KeyBy(DataValue coll, Func<DataValue, string> f) =>
        Elements(coll, "KeyBy")
            .Aggregate(DataMap.CreateBuilder(), (builder, element) => builder.Set(f(element), element))
            .ToDataMap();

    /// <summary>
    /// Indexes maps by one of their fields.
    /// </summary>
    /// <param name="coll">A list of maps, or a map whose values are maps.</param>
    /// <param name="key">The field whose string value is the key.</param>
    /// <returns>A map from each field value to its element.</returns>
    /// <exception cref="KeyNotFoundException">An element has no such field.</exception>
    /// <exception cref="InvalidOperationException">An element's field is not a string.</exception>
    /// <inheritdoc cref="KeyBy(DataValue, Func{DataValue, string})" path="/remarks"/>
    /// <example>
    /// <code>
    /// _.KeyBy(books, "isbn")   // { "978-1779501127": { isbn: "978-1779501127", ... }, ... }
    /// </code>
    /// </example>
    public static DataMap KeyBy(DataValue coll, string key) =>
        KeyBy(coll, row => Get<string>(row, key));

    /// <summary>
    /// Diffs two nodes.
    /// </summary>
    /// <param name="data1">The original node.</param>
    /// <param name="data2">The new node.</param>
    /// <returns>
    /// <see cref="NoDiff"/> when they are equivalent; otherwise <see cref="Changed"/>. For composites the change is
    /// a nested structure holding only the differing leaves, for leaves it is the new value.
    /// </returns>
    /// <example>
    /// <code>
    /// _.Diff(1L, 1L)                                    // NoDiff
    /// _.Diff(Map.Of(("a", 1)), Map.Of(("a", 2)))        // Changed({ a: 2 })
    /// _.Diff("old", "new")                              // Changed("new")
    /// </code>
    /// </example>
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

    /// <summary>
    /// Diffs two composites, returning a map holding only what differs.
    /// </summary>
    /// <remarks>
    /// An empty result means the two are equivalent.
    /// <para>
    /// A diff is always a map, even when diffing lists: list indices become string
    /// keys. Mirroring the list's shape instead would have to pad the unchanged slots,
    /// and that padding is indistinguishable from an element genuinely changed to null,
    /// which makes any merge over it silently wrong. Index keys carry only what changed.
    /// </para>
    /// <para>
    /// A key present on only one side diffs against null, so additions show up as the
    /// new value and removals as null.
    /// </para>
    /// </remarks>
    /// <param name="data1">The original value.</param>
    /// <param name="data2">The new value.</param>
    /// <returns>A map of the differences; apply it with <see cref="ApplyDiff(DataValue, DataMap)"/>.</returns>
    /// <example>
    /// <code>
    /// _.DiffObjects(Map.Of(("a", 1), ("b", 2)), Map.Of(("a", 1), ("b", 3)))   // { b: 3 }
    /// _.DiffObjects(List.Of("x", "y"), List.Of("x", "z"))                     // { "1": "z" }
    /// _.DiffObjects(Map.Of(("a", 1)), Map.Of())                               // { a: null }
    /// </code>
    /// </example>
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

    /// <summary>
    /// A leaf (most usefully a null) contributes no keys, so diffing an aggregate
    /// that does not exist yet against its first value reports every key as added.
    /// That makes creating an aggregate the same operation as changing one.
    /// </summary>
    private static IReadOnlyList<StringOrInt> KeysOrEmpty(DataValue value) =>
        IsObject(value) ? Keys(value) : [];

    /// <summary>
    /// List indices address a map as their string form, which <c>Get</c> and <c>Set</c> accept
    /// on the way back into a list.
    /// </summary>
    private static string KeyName(StringOrInt key) =>
        key switch { string s => s, int i => i.ToString() };

    /// <summary>
    /// Lists every root-to-leaf path in a structure.
    /// </summary>
    /// <remarks>
    /// Applied to a diff, this is the set of locations that diff touches, which is
    /// what decides whether two concurrent changes conflict.
    /// </remarks>
    /// <param name="value">The structure to walk.</param>
    /// <returns>One path per leaf. An empty map or list counts as a leaf, and a scalar yields the root path.</returns>
    /// <seealso cref="ChangedPaths"/>
    /// <example>
    /// <code>
    /// _.InformationPaths(Map.Of(("a", Map.Of(("b", 1))), ("c", List.Of(2))))   // a.b, c.[0]
    /// </code>
    /// </example>
    public static IReadOnlyList<DataPath> InformationPaths(DataValue value) =>
        Collect(value, DataPath.Root, []);

    /// <summary>
    /// Lists the paths a diff touches.
    /// </summary>
    /// <remarks>
    /// An empty diff touches nothing. That is not what <see cref="InformationPaths"/> says, which
    /// reports the root of an empty map as a touched location: correct for data
    /// (setting a field to <c>{}</c> is a change), wrong for a diff (no change at all). The
    /// difference matters once overlap is prefix-aware, because the root path is a
    /// prefix of everything and would collide with every concurrent write.
    /// </remarks>
    /// <param name="diff">A diff, as produced by <see cref="DiffObjects"/>.</param>
    /// <returns>The paths the diff changes; empty for an empty diff.</returns>
    /// <example>
    /// <code>
    /// var diff = _.DiffObjects(Map.Of(("a", 1), ("b", 2)), Map.Of(("a", 1), ("b", 3)));
    /// _.ChangedPaths(diff)           // b
    /// _.ChangedPaths(Map.Of())       // none: an empty diff touches nothing
    /// </code>
    /// </example>
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
