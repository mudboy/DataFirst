namespace DataFirst.Lodash;

/// The result of diffing two nodes: either they are the same, or data2 replaced data1.
/// Modelled as a union so the recursion in DiffObjects is exhaustive and a legitimate
/// data value can never be mistaken for the "unchanged" marker.
public union DiffResult(NoDiff, Changed);

public sealed record NoDiff
{
    public static readonly NoDiff Instance = new();
}

public sealed record Changed(object Value);

public static partial class _
{
    /// Diffs two nodes. Returns NoDiff when they are equivalent, otherwise the change:
    /// for objects that is a nested structure holding only the differing leaves, for
    /// leaves it is the new value.
    public static DiffResult Diff(object data1, object data2)
    {
        if (IsObject(data1) && IsObject(data2))
        {
            var diffed = DiffObjects(data1, data2);
            return IsEmpty(diffed) ? NoDiff.Instance : new Changed(diffed);
        }

        // leafs
        return Equals(data1, data2) ? NoDiff.Instance : new Changed(data2);
    }

    /// Diffs two objects, returning a structure of the same shape containing only the
    /// leaves that differ. An empty result means the two objects are equivalent.
    public static object DiffObjects(object data1, object data2)
    {
        object emptyObject = data1 is IndexedList
            ? IndexedList.Empty
            : StringMap.Empty;

        if (ReferenceEquals(data1, data2)) return emptyObject;

        var keys = Union(Keys(data1), Keys(data2));

        return keys.Aggregate(emptyObject, (acc, kObj) =>
        {
            var k = (string)kObj;
            return Diff(Get(data1, k), Get(data2, k)) switch
            {
                NoDiff => acc,
                Changed(var value) => Set(acc, k, value)
            };
        });
    }
}
