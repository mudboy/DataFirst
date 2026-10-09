namespace DataFirst.Lodash;

public static partial class _
{
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
}
