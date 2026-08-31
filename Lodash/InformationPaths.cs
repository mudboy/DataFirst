namespace DataFirst.Lodash;

public static partial class _
{
    /// Every root-to-leaf path in a structure.
    ///
    /// Applied to a diff, this is the set of locations that diff touches -- which is
    /// what decides whether two concurrent changes conflict.
    public static IReadOnlyList<DataPath> InformationPaths(DataValue value) =>
        Collect(value, DataPath.Root, []);

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
