namespace DataFirst.Lodash;

public static partial class _
{
    /// Reads several locations at once, returning their values in the order asked for.
    ///
    ///     _.At(book, "title", "isbn")   ->  ["Watchmen", "978-1779501127"]
    ///
    /// A key that is not there yields null rather than throwing, so At can project
    /// optional fields without the caller checking each one first. Duplicates are
    /// kept, and the result is always the same length as the key list -- which is
    /// what makes it safe to zip back against those keys.
    public static DataList At(DataValue obj, params IEnumerable<StringOrInt> keys) =>
        DataList.Create(keys.Select(key => GetOrNull(obj, key)));

    /// The same, for locations that are paths rather than single keys.
    ///
    ///     _.At(library, [DataPath.Of("catalog", "booksByIsbn"),
    ///                    DataPath.Of("userManagementData", "members")])
    ///
    /// Takes a collection rather than params, because two variadic overloads would
    /// be ambiguous for the empty call.
    public static DataList At(DataValue obj, IReadOnlyList<DataPath> paths) =>
        DataList.Create(paths.Select(path => GetOrNull(obj, path)));

    /// Reads the value at a single key or index.
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

    /// Walks a path of keys and indices. An empty path returns the value unchanged.
    public static DataValue Get(DataValue obj, IReadOnlyList<StringOrInt> path)
    {
        var current = obj;
        foreach (var key in path) current = Get(current, key);
        return current;
    }

    /// Like Get, but yields null rather than throwing when the key is absent.
    public static DataValue GetOrNull(DataValue obj, StringOrInt key) =>
        ContainsKey(obj, key) ? Get(obj, key) : DataNull.Instance;

    /// Like Get for a path, yielding null rather than throwing when any step of it
    /// is absent. An empty path addresses the value itself.
    public static DataValue GetOrNull(DataValue obj, IReadOnlyList<StringOrInt> path)
    {
        if (path.Count == 0) return obj;
        return ContainsKey(obj, path) ? Get(obj, path) : DataNull.Instance;
    }

    public static T Get<T>(DataValue obj, StringOrInt key) => Get(obj, key).As<T>();

    public static T Get<T>(DataValue obj, IReadOnlyList<StringOrInt> path) => Get(obj, path).As<T>();

    /// True when a single key or index is present.
    public static bool ContainsKey(DataValue obj, StringOrInt key) =>
        (obj, key) switch
        {
            (DataMap m, string k) => m.ContainsKey(k),
            (DataList l, _) when key.TryAsIndex(out var i) => i >= 0 && i < l.Count,
            _ => false
        };

    /// True when every step of the path is present. Unlike the map-only version
    /// this walks lists too, and returns false rather than throwing when an
    /// intermediate node is a leaf.
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

    /// Applies a diff produced by DiffObjects onto a value, returning a new value.
    ///
    /// Walks the diff's information paths rather than merging structurally, so every
    /// location the diff records is written exactly as recorded -- including one
    /// genuinely changed to null. An empty diff records nothing, so the target comes
    /// back unchanged (InformationPaths would report the diff's own root, and writing
    /// that would replace the target with {}).
    public static DataValue Merge(DataValue target, DataMap diff)
    {
        var paths = ChangedPaths(diff);
        return paths.Aggregate(Seed(target, paths), (acc, path) => Set(acc, path, Get(diff, path)));
    }

    /// Merging into nothing builds a map.
    ///
    /// It cannot build a list: a diff is always a map, with list indices rendered as
    /// string keys, so by the time a change reaches here the root container type has
    /// been erased. Creating a map-rooted aggregate from nothing works; creating a
    /// list-rooted one needs the value, not just the diff. Aggregates are maps in
    /// practice, so this has not bitten -- but it is a real edge of the encoding.
    private static DataValue Seed(DataValue target, IReadOnlyList<DataPath> paths) =>
        target is DataNull && paths.Count > 0 ? DataMap.Empty : target;

    public static DataMap Merge(DataMap target, DataMap diff) =>
        Merge((DataValue)target, diff).As<DataMap>();

    /// Recursively merges two values, the second taking precedence -- lodash's merge.
    ///
    ///     _.MergeDeep(
    ///         Map.Of(("a", List.Of(Map.Of(("b", 2)), Map.Of(("d", 4))))),
    ///         Map.Of(("a", List.Of(Map.Of(("c", 3)), Map.Of(("e", 5))))))
    ///     ->  { a: [ { b: 2, c: 3 }, { d: 4, e: 5 } ] }
    ///
    /// Two maps merge key by key; a key in only one of them is kept. Two lists merge
    /// by index, element against element, and the tail of the longer one is kept.
    /// Anything else -- a scalar, or two values of different kinds -- is replaced by
    /// the second, null included: null is a value here, not an absence, so it
    /// overwrites.
    ///
    /// Not the same as Merge, which applies a diff and treats its keys as paths.
    public static DataValue MergeDeep(DataValue first, DataValue second) =>
        (first, second) switch
        {
            (DataMap a, DataMap b) => MergeMaps(a, b),
            (DataList a, DataList b) => MergeLists(a, b),
            _ => second
        };

    public static DataMap MergeDeep(DataMap first, DataMap second) => MergeMaps(first, second);

    private static DataMap MergeMaps(DataMap first, DataMap second) =>
        second.Aggregate(first, (merged, entry) =>
            merged.SetItem(
                entry.Key,
                first.ContainsKey(entry.Key) ? MergeDeep(first[entry.Key], entry.Value) : entry.Value));

    private static DataList MergeLists(DataList first, DataList second) =>
        DataList.Create(Enumerable.Range(0, Math.Max(first.Count, second.Count)).Select(index =>
            (index < first.Count, index < second.Count) switch
            {
                (true, true) => MergeDeep(first[index], second[index]),
                (true, false) => first[index],
                _ => second[index]
            }));

    /// A map without the fields at the given paths.
    ///
    ///     _.Omit(member, [DataPath.Of("password"), DataPath.Of("address", "line2")])
    ///
    /// A path that is not there is skipped, so the same list can be applied to maps
    /// of different shapes. Only map fields can be omitted: removing from a list would
    /// shift every later index, so a path that ends inside a list is an error rather
    /// than a quiet renumbering. The root cannot be omitted.
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

    /// Writes a value at a single key or index, returning a new structure.
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

    /// Writes a value at a path, rebuilding each node along the way.
    /// An empty path replaces the whole structure.
    public static DataValue Set(DataValue obj, IReadOnlyList<StringOrInt> path, DataValue value)
    {
        if (path.Count == 0) return value;

        var key = path[0];
        if (path.Count == 1) return Set(obj, key, value);

        var rest = path.Skip(1).ToList();
        return Set(obj, key, Set(Descend(obj, key, rest[0]), rest, value));
    }

    /// The node at key, or a new empty container when there is nothing there yet, so
    /// a path can be written into a structure that does not have it. The following
    /// step decides which container: a name makes a map, an index makes a list.
    ///
    /// Without this, merging a diff that introduces a nested value throws, because
    /// the descent walks through a key the target does not have.
    private static DataValue Descend(DataValue obj, StringOrInt key, StringOrInt nextStep)
    {
        if (ContainsKey(obj, key) && Get(obj, key) is var existing and not DataNull)
            return existing;

        return nextStep is int ? DataList.Empty : DataMap.Empty;
    }

    public static DataMap Set(DataMap map, StringOrInt key, DataValue value) =>
        Set((DataValue)map, key, value).As<DataMap>();

    public static DataMap Set(DataMap map, IReadOnlyList<StringOrInt> path, DataValue value) =>
        Set((DataValue)map, path, value).As<DataMap>();

    public static DataList Values(DataMap map) => DataList.Create(map.Values);

    public static DataValue Update(DataValue obj, StringOrInt key, Func<DataValue, DataValue> f) =>
        Set(obj, key, f(Get(obj, key)));

    public static DataValue Update(DataValue obj, IReadOnlyList<StringOrInt> path, Func<DataValue, DataValue> f) =>
        Set(obj, path, f(Get(obj, path)));

    public static DataMap Update(DataMap map, StringOrInt key, Func<DataValue, DataValue> f) =>
        Update((DataValue)map, key, f).As<DataMap>();

    public static DataMap Update(DataMap map, IReadOnlyList<StringOrInt> path, Func<DataValue, DataValue> f) =>
        Update((DataValue)map, path, f).As<DataMap>();

    /// Expands a map holding a list at key into one map per element.
    public static DataList Unwind(DataMap map, string key)
    {
        var elements = Get<DataList>(map, key);
        return DataList.Create(elements.Select(element => (DataValue)map.SetItem(key, element)));
    }
}
