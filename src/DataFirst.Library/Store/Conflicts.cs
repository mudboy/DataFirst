using DataFirst.Lodash;

namespace DataFirst.Library;

public static class Conflicts
{
    /// The locations both diffs touch. Empty means the two changes are independent
    /// and can both be kept.
    ///
    /// Overlap, not equality: a change to `items` collides with a change to
    /// `items[1]`, because one replaces what the other reaches into.
    public static IReadOnlyList<DataPath> CommonPaths(DataMap diff1, DataMap diff2)
    {
        var first = _.ChangedPaths(diff1);

        return _.ChangedPaths(diff2)
            .Where(path => first.Any(path.Overlaps))
            .ToList();
    }
}
