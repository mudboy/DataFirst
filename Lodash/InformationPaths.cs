namespace DataFirst.Lodash;

public static partial class _
{
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
