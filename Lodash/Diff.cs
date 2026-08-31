namespace DataFirst.Lodash;

/// The result of diffing two nodes: either they are the same, or data2 replaced data1.
/// Modelled as a union so the recursion in DiffObjects is exhaustive and a legitimate
/// data value can never be mistaken for the "unchanged" marker.
public union DiffResult(NoDiff, Changed);

public sealed record NoDiff
{
    public static readonly NoDiff Instance = new();
}

public sealed record Changed(DataValue Value);

public static partial class _
{
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

    /// Diffs two composites, returning a structure of the same shape containing only
    /// the leaves that differ. An empty result means the two are equivalent.
    ///
    /// A key present on only one side diffs against null, so additions show up as the
    /// new value and removals as null.
    public static DataValue DiffObjects(DataValue data1, DataValue data2)
    {
        DataValue empty = data1 is DataList ? DataList.Empty : DataMap.Empty;

        if (ReferenceEquals(data1.Unwrap(), data2.Unwrap())) return empty;

        var keys = Union(Keys(data1), Keys(data2));

        return keys.Aggregate(empty, (acc, key) =>
            Diff(GetOrNull(data1, key), GetOrNull(data2, key)) switch
            {
                NoDiff => acc,
                Changed(var value) => Set(acc, key, value)
            });
    }
}
