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
}
