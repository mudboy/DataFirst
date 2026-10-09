using System.Runtime.CompilerServices;

namespace DataFirst.Lodash;

/// <summary>
/// Lodash-style functions over the generic data types (<see cref="DataValue"/>, <see cref="DataMap"/> and <see cref="DataList"/>).
/// </summary>
/// <remarks>
/// Every function is pure: it returns a new value and never modifies its arguments.
/// The functions are grouped across three files named for the groups in the book's appendix:
/// maps (<c>At</c>, <c>Get</c>, <c>Set</c>, <c>Merge</c>, <c>Omit</c> and friends), arrays
/// (<c>Concat</c>, <c>Uniq</c>, <c>Union</c> and friends) and collections (<c>Map</c>, <c>Filter</c>,
/// <c>GroupBy</c>, <c>SortBy</c>, <c>Diff</c> and friends).
/// <para>
/// In the examples, <c>book</c> is <c>Map.Of(("title", "Watchmen"), ("isbn", "978-1779501127"))</c>,
/// <c>library</c> is the library seed data, and <c>books</c> and <c>rows</c> are lists of maps.
/// </para>
/// </remarks>
public static partial class _
{
    /// <summary>
    /// Reads several locations at once, returning their values in the order asked for.
    /// </summary>
    /// <remarks>
    /// A key that is not there yields null rather than throwing, so <c>At</c> can project
    /// optional fields without the caller checking each one first. Duplicates are
    /// kept, and the result is always the same length as the key list, which is
    /// what makes it safe to zip back against those keys.
    /// </remarks>
    /// <param name="obj">The map or list to read from.</param>
    /// <param name="keys">The keys or indices to read, in the order the values are wanted.</param>
    /// <returns>A list with one value per key, <see cref="DataNull"/> where the key is absent.</returns>
    /// <example>
    /// <code>
    /// _.At(book, "title", "isbn")   // ["Watchmen", "978-1779501127"]
    /// </code>
    /// </example>
    [OverloadResolutionPriority(1)]
    public static DataList At(DataValue obj, params IEnumerable<StringOrInt> keys) =>
        DataList.Create(keys.Select(key => GetOrNull(obj, key)));

    /// <summary>
    /// Reads several locations at once, for locations that are paths rather than single keys.
    /// </summary>
    /// <remarks>
    /// Takes a collection rather than <c>params</c>, because two variadic overloads would
    /// be ambiguous for the empty call. The keys overload is preferred for the literal
    /// <c>[]</c>, which both overloads accept and which reads nothing either way.
    /// </remarks>
    /// <param name="obj">The value to read from.</param>
    /// <param name="paths">The paths to read, in the order the values are wanted.</param>
    /// <returns>A list with one value per path, <see cref="DataNull"/> where any step of the path is absent.</returns>
    /// <example>
    /// <code>
    /// _.At(library, [DataPath.Of("catalog", "booksByIsbn"),
    ///                DataPath.Of("userManagementData", "members")])
    /// </code>
    /// </example>
    public static DataList At(DataValue obj, IReadOnlyList<DataPath> paths) =>
        DataList.Create(paths.Select(path => GetOrNull(obj, path)));

    /// <summary>
    /// Reads the value at a single key or index.
    /// </summary>
    /// <param name="obj">The map or list to read from.</param>
    /// <param name="key">A string key for a map, or an index for a list.</param>
    /// <returns>The value at <paramref name="key"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="obj"/> is neither a map nor a list, or the key's kind does not suit it
    /// (a numeric index into a map, a non-numeric key into a list).
    /// </exception>
    /// <exception cref="KeyNotFoundException">The key is not present in a map. Use <c>GetOrNull</c> to get null instead.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside a list.</exception>
    /// <example>
    /// <code>
    /// _.Get(book, "title")                 // "Watchmen"
    /// _.Get(List.Of("a", "b"), 1)          // "b"
    /// _.Get(book, "missing")               // throws KeyNotFoundException
    /// </code>
    /// </example>
    public static DataValue Get(DataValue obj, StringOrInt key) =>
        (obj, key) switch
        {
            (DataMap m, string k) => m[k],
            (DataList l, _) when key.TryAsIndex(out var i) => l[i],
            (DataMap, int i) => throw new InvalidOperationException(
                $"Cannot index a map with {i}; maps are keyed by string"),
            (DataList, string s) => throw new InvalidOperationException(
                $"Cannot index a list with key '{s}'; list indices must be numeric"),
            _ => throw new InvalidOperationException(
                $"Cannot read {key.Describe()} from a {obj.Describe()}")
        };

    /// <summary>
    /// Walks a path of keys and indices.
    /// </summary>
    /// <param name="obj">The value to start from.</param>
    /// <param name="path">The keys and indices to follow. An empty path returns the value unchanged.</param>
    /// <returns>The value at the end of the path.</returns>
    /// <exception cref="InvalidOperationException">A step cannot be taken from the value it is applied to.</exception>
    /// <exception cref="KeyNotFoundException">A step names a key that is not present in a map.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A step names an index outside a list.</exception>
    /// <example>
    /// <code>
    /// _.Get(library, ["catalog", "booksByIsbn", "978-1779501127", "title"])   // "Watchmen"
    /// _.Get(library, [])                                                     // library itself
    /// </code>
    /// </example>
    public static DataValue Get(DataValue obj, IReadOnlyList<StringOrInt> path)
    {
        var current = obj;
        foreach (var key in path) current = Get(current, key);
        return current;
    }

    /// <summary>
    /// Like <c>Get</c>, but yields null rather than throwing when the key is absent.
    /// </summary>
    /// <param name="obj">The map or list to read from.</param>
    /// <param name="key">A string key for a map, or an index for a list.</param>
    /// <returns>The value at <paramref name="key"/>, or <see cref="DataNull"/> when it is not there.</returns>
    /// <example>
    /// <code>
    /// _.GetOrNull(book, "title")     // "Watchmen"
    /// _.GetOrNull(book, "missing")   // null
    /// </code>
    /// </example>
    public static DataValue GetOrNull(DataValue obj, StringOrInt key) =>
        ContainsKey(obj, key) ? Get(obj, key) : DataNull.Instance;

    /// <summary>
    /// Like <c>Get</c> for a path, yielding null rather than throwing when any step of it is absent.
    /// </summary>
    /// <param name="obj">The value to start from.</param>
    /// <param name="path">The keys and indices to follow. An empty path addresses the value itself.</param>
    /// <returns>The value at the end of the path, or <see cref="DataNull"/> when the path does not exist.</returns>
    /// <example>
    /// <code>
    /// _.GetOrNull(library, ["catalog", "nothing", "here"])   // null
    /// </code>
    /// </example>
    public static DataValue GetOrNull(DataValue obj, IReadOnlyList<StringOrInt> path)
    {
        if (path.Count == 0) return obj;
        return ContainsKey(obj, path) ? Get(obj, path) : DataNull.Instance;
    }

    /// <summary>
    /// Reads the value at a single key or index, converted to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type the value is expected to have.</typeparam>
    /// <param name="obj">The map or list to read from.</param>
    /// <param name="key">A string key for a map, or an index for a list.</param>
    /// <returns>The value at <paramref name="key"/> as a <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">The value is not a <typeparamref name="T"/>, or the read itself fails as for <c>Get</c>.</exception>
    /// <example>
    /// <code>
    /// _.Get&lt;string&gt;(book, "title")   // "Watchmen"
    /// _.Get&lt;long&gt;(book, "title")     // throws: Expected Int64 but found string
    /// </code>
    /// </example>
    public static T Get<T>(DataValue obj, StringOrInt key) => Get(obj, key).As<T>();

    /// <summary>
    /// Walks a path of keys and indices, converting the value found to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type the value is expected to have.</typeparam>
    /// <param name="obj">The value to start from.</param>
    /// <param name="path">The keys and indices to follow.</param>
    /// <returns>The value at the end of the path as a <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">The value is not a <typeparamref name="T"/>, or the walk itself fails as for <c>Get</c>.</exception>
    /// <example>
    /// <code>
    /// _.Get&lt;string&gt;(library, ["catalog", "booksByIsbn", "978-1779501127", "title"])   // "Watchmen"
    /// </code>
    /// </example>
    public static T Get<T>(DataValue obj, IReadOnlyList<StringOrInt> path) => Get(obj, path).As<T>();

    /// <summary>
    /// Tests whether a single key or index is present.
    /// </summary>
    /// <param name="obj">The value to look in. Anything other than a map or list has no keys.</param>
    /// <param name="key">A string key for a map, or an index for a list.</param>
    /// <returns>True when the key is present; false otherwise, including for a value that is neither map nor list.</returns>
    /// <example>
    /// <code>
    /// _.ContainsKey(book, "title")        // true
    /// _.ContainsKey(List.Of("a"), 1)      // false: the list has only index 0
    /// </code>
    /// </example>
    public static bool ContainsKey(DataValue obj, StringOrInt key) =>
        (obj, key) switch
        {
            (DataMap m, string k) => m.ContainsKey(k),
            (DataList l, _) when key.TryAsIndex(out var i) => i >= 0 && i < l.Count,
            _ => false
        };

    /// <summary>
    /// Tests whether every step of a path is present.
    /// </summary>
    /// <remarks>
    /// Unlike a map-only check this walks lists too, and returns false rather than
    /// throwing when an intermediate node is a leaf.
    /// </remarks>
    /// <param name="obj">The value to start from.</param>
    /// <param name="path">The keys and indices to follow.</param>
    /// <returns>True when the whole path exists. An empty path is never contained.</returns>
    /// <example>
    /// <code>
    /// _.ContainsKey(library, ["catalog", "booksByIsbn"])   // true
    /// _.ContainsKey(library, ["catalog", "nothing"])       // false
    /// </code>
    /// </example>
    public static bool ContainsKey(DataValue obj, IReadOnlyList<StringOrInt> path)
    {
        if (path.Count == 0) return false;

        var current = obj;
        for (var i = 0; i < path.Count; i++)
        {
            if (!ContainsKey(current, path[i])) return false;
            if (i < path.Count - 1) current = Get(current, path[i]);
        }

        return true;
    }

    /// <summary>
    /// Applies a diff produced by <c>DiffObjects</c> onto a value, returning a new value.
    /// </summary>
    /// <remarks>
    /// Walks the diff's information paths rather than merging structurally, so every
    /// location the diff records is written exactly as recorded, including one
    /// genuinely changed to null. An empty diff records nothing, so the target comes
    /// back unchanged (<c>InformationPaths</c> would report the diff's own root, and writing
    /// that would replace the target with <c>{}</c>).
    /// </remarks>
    /// <param name="target">The value to apply the diff to. Null is treated as an empty map when the diff has changes.</param>
    /// <param name="diff">The diff to apply, as produced by <c>DiffObjects</c>.</param>
    /// <returns>A new value with the diff applied; <paramref name="target"/> is not modified.</returns>
    /// <seealso cref="Merge(DataValue, DataValue)"/>
    /// <example>
    /// <code>
    /// var before = Map.Of(("title", "Watchmen"), ("year", 1986));
    /// var after = Map.Of(("title", "Watchmen"), ("year", 1987));
    ///
    /// _.ApplyDiff(before, _.DiffObjects(before, after))   // equals after
    /// </code>
    /// </example>
    public static DataValue ApplyDiff(DataValue target, DataMap diff)
    {
        var paths = ChangedPaths(diff);
        return paths.Aggregate(Seed(target, paths), (acc, path) => Set(acc, path, Get(diff, path)));
    }

    /// <summary>
    /// Merging into nothing builds a map.
    /// </summary>
    /// <remarks>
    /// It cannot build a list: a diff is always a map, with list indices rendered as
    /// string keys, so by the time a change reaches here the root container type has
    /// been erased. Creating a map-rooted aggregate from nothing works; creating a
    /// list-rooted one needs the value, not just the diff. Aggregates are maps in
    /// practice, so this has not bitten, but it is a real edge of the encoding.
    /// </remarks>
    private static DataValue Seed(DataValue target, IReadOnlyList<DataPath> paths) =>
        target is DataNull && paths.Count > 0 ? DataMap.Empty : target;

    /// <summary>
    /// Applies a diff produced by <c>DiffObjects</c> onto a map, returning a new map.
    /// </summary>
    /// <param name="target">The map to apply the diff to.</param>
    /// <param name="diff">The diff to apply, as produced by <c>DiffObjects</c>.</param>
    /// <returns>A new map with the diff applied; <paramref name="target"/> is not modified.</returns>
    /// <inheritdoc cref="ApplyDiff(DataValue, DataMap)" path="/remarks"/>
    /// <example>
    /// <code>
    /// var diff = _.DiffObjects(before, after);   // { year: 1987 }
    /// _.ApplyDiff(before, diff)                   // equals after
    /// </code>
    /// </example>
    public static DataMap ApplyDiff(DataMap target, DataMap diff) =>
        ApplyDiff((DataValue)target, diff).As<DataMap>();

    /// <summary>
    /// Recursively merges two values, the second taking precedence. This is lodash's <c>merge</c>.
    /// </summary>
    /// <remarks>
    /// Two maps merge key by key; a key in only one of them is kept. Two lists merge
    /// by index, element against element, and the tail of the longer one is kept.
    /// Anything else (a scalar, or two values of different kinds) is replaced by
    /// the second, null included: null is a value here, not an absence, so it
    /// overwrites.
    /// <para>
    /// Not the same as <see cref="ApplyDiff(DataValue, DataMap)"/>, which applies a diff and treats its keys as paths.
    /// </para>
    /// </remarks>
    /// <param name="first">The base value.</param>
    /// <param name="second">The value merged over it; it wins wherever the two disagree.</param>
    /// <returns>The merged value. Neither argument is modified.</returns>
    /// <example>
    /// <code>
    /// _.Merge(
    ///     Map.Of(("a", List.Of(Map.Of(("b", 2)), Map.Of(("d", 4))))),
    ///     Map.Of(("a", List.Of(Map.Of(("c", 3)), Map.Of(("e", 5))))))
    /// // { a: [ { b: 2, c: 3 }, { d: 4, e: 5 } ] }
    /// </code>
    /// </example>
    public static DataValue Merge(DataValue first, DataValue second) =>
        (first, second) switch
        {
            (DataMap a, DataMap b) => MergeMaps(a, b),
            (DataList a, DataList b) => MergeLists(a, b),
            _ => second
        };

    /// <summary>
    /// Recursively merges two maps, the second taking precedence.
    /// </summary>
    /// <param name="first">The base map.</param>
    /// <param name="second">The map merged over it; it wins wherever the two disagree.</param>
    /// <returns>The merged map. Neither argument is modified.</returns>
    /// <inheritdoc cref="Merge(DataValue, DataValue)" path="/remarks"/>
    /// <example>
    /// <code>
    /// _.Merge(Map.Of(("a", 1), ("b", 2)), Map.Of(("b", 3), ("c", 4)))   // { a: 1, b: 3, c: 4 }
    /// </code>
    /// </example>
    public static DataMap Merge(DataMap first, DataMap second) => MergeMaps(first, second);

    private static DataMap MergeMaps(DataMap first, DataMap second) =>
        second.Aggregate(first, (merged, entry) =>
            merged.SetItem(
                entry.Key,
                first.ContainsKey(entry.Key) ? Merge(first[entry.Key], entry.Value) : entry.Value));

    private static DataList MergeLists(DataList first, DataList second) =>
        DataList.Create(Enumerable.Range(0, Math.Max(first.Count, second.Count)).Select(index =>
            (index < first.Count, index < second.Count) switch
            {
                (true, true) => Merge(first[index], second[index]),
                (true, false) => first[index],
                _ => second[index]
            }));

    /// <summary>
    /// Returns a map without the fields at the given paths.
    /// </summary>
    /// <remarks>
    /// A path that is not there is skipped, so the same list can be applied to maps
    /// of different shapes. Only map fields can be omitted: removing from a list would
    /// shift every later index, so a path that ends inside a list is an error rather
    /// than a quiet renumbering. The root cannot be omitted.
    /// </remarks>
    /// <param name="map">The map to remove fields from.</param>
    /// <param name="paths">The paths of the fields to remove.</param>
    /// <returns>A new map without those fields; <paramref name="map"/> is not modified.</returns>
    /// <exception cref="ArgumentException">A path is the root.</exception>
    /// <exception cref="InvalidOperationException">A path ends inside a list rather than at a map field.</exception>
    /// <example>
    /// <code>
    /// _.Omit(member, [DataPath.Of("password"), DataPath.Of("address", "line2")])
    /// </code>
    /// </example>
    public static DataMap Omit(DataMap map, IReadOnlyList<DataPath> paths) =>
        paths.Aggregate(map, OmitOne);

    private static DataMap OmitOne(DataMap map, DataPath path)
    {
        if (path.Count == 0) throw new ArgumentException("Cannot omit the root", nameof(path));
        if (!ContainsKey(map, path)) return map;

        var parentPath = path.Take(path.Count - 1).ToList();
        var parent = parentPath.Count == 0 ? map : Get(map, parentPath);

        return (parent, path[^1]) switch
        {
            (DataMap owner, string key) when parentPath.Count == 0 => owner.Remove(key),
            (DataMap owner, string key) => Set(map, parentPath, owner.Remove(key)),
            _ => throw new InvalidOperationException(
                $"Cannot omit {path[^1].Describe()} from a {parent.Describe()}; only map fields can be omitted")
        };
    }

    /// <summary>
    /// Writes a value at a single key or index, returning a new structure.
    /// </summary>
    /// <param name="obj">The map or list to write into.</param>
    /// <param name="key">A string key for a map, or an index for a list. An index past the end of a list pads it with nulls.</param>
    /// <param name="value">The value to write.</param>
    /// <returns>A new structure with the value written; <paramref name="obj"/> is not modified.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="obj"/> is neither a map nor a list, or the key's kind does not suit it.
    /// </exception>
    /// <example>
    /// <code>
    /// _.Set(book, "title", "Watchmen (2nd ed.)")   // a new map with the title changed
    /// _.Set(List.Of("a"), 2, "c")                   // ["a", null, "c"]
    /// </code>
    /// </example>
    public static DataValue Set(DataValue obj, StringOrInt key, DataValue value) =>
        (obj, key) switch
        {
            (DataMap m, string k) => m.SetItem(k, value),
            (DataList l, _) when key.TryAsIndex(out var i) => SetAt(l, i, value),
            (DataMap, int i) => throw new InvalidOperationException(
                $"Cannot index a map with {i}; maps are keyed by string"),
            (DataList, string s) => throw new InvalidOperationException(
                $"Cannot index a list with key '{s}'; list indices must be numeric"),
            _ => throw new InvalidOperationException(
                $"Cannot write {key.Describe()} into a {obj.Describe()}")
        };

    /// <summary>
    /// Writes a value at a path, rebuilding each node along the way.
    /// </summary>
    /// <remarks>
    /// Containers missing along the path are created: the following step decides
    /// which, a name making a map and an index making a list.
    /// </remarks>
    /// <param name="obj">The value to write into.</param>
    /// <param name="path">The keys and indices to follow. An empty path replaces the whole structure.</param>
    /// <param name="value">The value to write.</param>
    /// <returns>A new structure with the value written; <paramref name="obj"/> is not modified.</returns>
    /// <exception cref="InvalidOperationException">A step cannot be taken from the value it is applied to.</exception>
    /// <example>
    /// <code>
    /// _.Set(library, ["catalog", "booksByIsbn", "978-1779501127", "publicationYear"], 1986)
    /// _.Set(Map.Of(), ["a", "b"], 1)    // { a: { b: 1 } }: missing containers are created
    /// _.Set(Map.Of(), ["a", 0], 1)      // { a: [1] }: an index makes a list
    /// </code>
    /// </example>
    public static DataValue Set(DataValue obj, IReadOnlyList<StringOrInt> path, DataValue value)
    {
        if (path.Count == 0) return value;

        var key = path[0];
        if (path.Count == 1) return Set(obj, key, value);

        var rest = path.Skip(1).ToList();
        return Set(obj, key, Set(Descend(obj, key, rest[0]), rest, value));
    }

    /// <summary>
    /// The node at <paramref name="key"/>, or a new empty container when there is nothing there yet, so
    /// a path can be written into a structure that does not have it. The following
    /// step decides which container: a name makes a map, an index makes a list.
    /// </summary>
    /// <remarks>
    /// Without this, applying a diff that introduces a nested value throws, because
    /// the descent walks through a key the target does not have.
    /// </remarks>
    private static DataValue Descend(DataValue obj, StringOrInt key, StringOrInt nextStep)
    {
        if (ContainsKey(obj, key) && Get(obj, key) is var existing and not DataNull)
            return existing;

        return nextStep is int ? DataList.Empty : DataMap.Empty;
    }

    /// <summary>
    /// Writes a value at a single key of a map, returning a new map.
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="key">The key to write; a numeric key is an error for a map.</param>
    /// <param name="value">The value to write.</param>
    /// <returns>A new map with the value written; <paramref name="map"/> is not modified.</returns>
    /// <exception cref="InvalidOperationException">The key is an index.</exception>
    /// <example>
    /// <code>
    /// _.Set(Map.Of(("a", 1)), "b", 2)   // { a: 1, b: 2 }
    /// </code>
    /// </example>
    public static DataMap Set(DataMap map, StringOrInt key, DataValue value) =>
        Set((DataValue)map, key, value).As<DataMap>();

    /// <summary>
    /// Writes a value at a path of a map, returning a new map.
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="path">The keys and indices to follow.</param>
    /// <param name="value">The value to write.</param>
    /// <returns>A new map with the value written; <paramref name="map"/> is not modified.</returns>
    /// <exception cref="InvalidOperationException">A step cannot be taken, or the path is empty and <paramref name="value"/> is not a map.</exception>
    /// <inheritdoc cref="Set(DataValue, IReadOnlyList{StringOrInt}, DataValue)" path="/remarks"/>
    /// <example>
    /// <code>
    /// _.Set(Map.Of(("a", Map.Of(("b", 1)))), ["a", "b"], 2)   // { a: { b: 2 } }
    /// </code>
    /// </example>
    public static DataMap Set(DataMap map, IReadOnlyList<StringOrInt> path, DataValue value) =>
        Set((DataValue)map, path, value).As<DataMap>();

    /// <summary>
    /// The values of a map, in the map's insertion order.
    /// </summary>
    /// <param name="map">The map whose values are wanted.</param>
    /// <returns>A list of the map's values.</returns>
    /// <example>
    /// <code>
    /// _.Values(Map.Of(("a", 1), ("b", 2)))   // [1, 2]
    /// </code>
    /// </example>
    public static DataList Values(DataMap map) => DataList.Create(map.Values);

    /// <summary>
    /// Replaces the value at a key or index with the result of a function of it.
    /// </summary>
    /// <param name="obj">The map or list to update.</param>
    /// <param name="key">A string key for a map, or an index for a list. It must already exist.</param>
    /// <param name="f">Computes the new value from the current one.</param>
    /// <returns>A new structure with the updated value; <paramref name="obj"/> is not modified.</returns>
    /// <exception cref="KeyNotFoundException">The key is not present in a map.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside a list.</exception>
    /// <exception cref="InvalidOperationException">The key's kind does not suit <paramref name="obj"/>.</exception>
    /// <example>
    /// <code>
    /// _.Update(Map.Of(("count", 1)), "count", n => n.As&lt;long&gt;() + 1)   // { count: 2 }
    /// </code>
    /// </example>
    public static DataValue Update(DataValue obj, StringOrInt key, Func<DataValue, DataValue> f) =>
        Set(obj, key, f(Get(obj, key)));

    /// <summary>
    /// Replaces the value at a path with the result of a function of it.
    /// </summary>
    /// <param name="obj">The value to update.</param>
    /// <param name="path">The keys and indices to follow. The location must already exist.</param>
    /// <param name="f">Computes the new value from the current one.</param>
    /// <returns>A new structure with the updated value; <paramref name="obj"/> is not modified.</returns>
    /// <exception cref="KeyNotFoundException">A step names a key that is not present.</exception>
    /// <exception cref="InvalidOperationException">A step cannot be taken from the value it is applied to.</exception>
    /// <example>
    /// <code>
    /// _.Update(order, ["lines", 0, "quantity"], n => n.As&lt;long&gt;() + 1)
    /// </code>
    /// </example>
    public static DataValue Update(DataValue obj, IReadOnlyList<StringOrInt> path, Func<DataValue, DataValue> f) =>
        Set(obj, path, f(Get(obj, path)));

    /// <summary>
    /// Replaces the value at a key of a map with the result of a function of it.
    /// </summary>
    /// <param name="map">The map to update.</param>
    /// <param name="key">The key to update. It must already exist.</param>
    /// <param name="f">Computes the new value from the current one.</param>
    /// <returns>A new map with the updated value; <paramref name="map"/> is not modified.</returns>
    /// <exception cref="KeyNotFoundException">The key is not present.</exception>
    /// <example>
    /// <code>
    /// _.Update(Map.Of(("name", "watchmen")), "name", s => s.As&lt;string&gt;().ToUpperInvariant())   // { name: "WATCHMEN" }
    /// </code>
    /// </example>
    public static DataMap Update(DataMap map, StringOrInt key, Func<DataValue, DataValue> f) =>
        Update((DataValue)map, key, f).As<DataMap>();

    /// <summary>
    /// Replaces the value at a path of a map with the result of a function of it.
    /// </summary>
    /// <param name="map">The map to update.</param>
    /// <param name="path">The keys and indices to follow. The location must already exist.</param>
    /// <param name="f">Computes the new value from the current one.</param>
    /// <returns>A new map with the updated value; <paramref name="map"/> is not modified.</returns>
    /// <exception cref="KeyNotFoundException">A step names a key that is not present.</exception>
    /// <exception cref="InvalidOperationException">A step cannot be taken from the value it is applied to.</exception>
    /// <example>
    /// <code>
    /// _.Update(Map.Of(("a", Map.Of(("n", 1)))), ["a", "n"], n => n.As&lt;long&gt;() + 1)   // { a: { n: 2 } }
    /// </code>
    /// </example>
    public static DataMap Update(DataMap map, IReadOnlyList<StringOrInt> path, Func<DataValue, DataValue> f) =>
        Update((DataValue)map, path, f).As<DataMap>();

    /// <summary>
    /// Expands a map holding a list at a key into one map per element.
    /// </summary>
    /// <param name="map">The map holding the list.</param>
    /// <param name="key">The key of the list. Each result is <paramref name="map"/> with that key set to one element.</param>
    /// <returns>A list of maps, one per element of the list, in order.</returns>
    /// <exception cref="KeyNotFoundException">The key is not present.</exception>
    /// <exception cref="InvalidOperationException">The value at the key is not a list.</exception>
    /// <example>
    /// <code>
    /// _.Unwind(Map.Of(("id", 1), ("tags", List.Of("a", "b"))), "tags")
    /// // [ { id: 1, tags: "a" }, { id: 1, tags: "b" } ]
    /// </code>
    /// </example>
    public static DataList Unwind(DataMap map, string key)
    {
        var elements = Get<DataList>(map, key);
        return DataList.Create(elements.Select(element => (DataValue)map.SetItem(key, element)));
    }
}
