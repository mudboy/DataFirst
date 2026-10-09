namespace DataFirst.Lodash;

public static partial class _
{
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
}
