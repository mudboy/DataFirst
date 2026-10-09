using System.Collections;

namespace DataFirst;

/// A path through a structure, as a sequence of map keys and list indices.
///
/// Exists as its own type rather than a bare list because the concurrency control
/// puts paths in sets and intersects them, which needs value equality and hashing.
public sealed class DataPath : IEquatable<DataPath>, IReadOnlyList<StringOrInt>
{
    public static readonly DataPath Root = new([]);

    private readonly StringOrInt[] steps;
    private readonly int hash;

    private DataPath(StringOrInt[] steps)
    {
        this.steps = steps;

        var accumulated = new HashCode();
        foreach (var step in steps) accumulated.Add(step);
        hash = accumulated.ToHashCode();
    }

    public static DataPath Of(params StringOrInt[] steps) => new(steps);

    public static DataPath Of(IEnumerable<StringOrInt> steps) => new(steps.ToArray());

    public int Count => steps.Length;
    public StringOrInt this[int index] => steps[index];

    /// A new path with one more step on the end.
    public DataPath Then(StringOrInt step) => new([.. steps, step]);

    /// A new path with another path appended, for re-rooting an error reported
    /// against a nested value onto the whole structure.
    public DataPath Then(DataPath suffix) => new([.. steps, .. suffix.steps]);

    /// True when two paths address overlapping data: equal, or one inside the other.
    ///
    /// Changing `items` and changing `items[1]` are not the same path, but they are
    /// not independent either -- one replaces what the other is reaching into. Exact
    /// set intersection misses that and lets both changes through.
    public bool Overlaps(DataPath other)
    {
        var shared = Math.Min(steps.Length, other.steps.Length);

        for (var i = 0; i < shared; i++)
            if (!steps[i].Equals(other.steps[i])) return false;

        return true;
    }

    public bool Equals(DataPath? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || other.steps.Length != steps.Length || other.hash != hash) return false;

        for (var i = 0; i < steps.Length; i++)
            if (!steps[i].Equals(other.steps[i])) return false;

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as DataPath);
    public override int GetHashCode() => hash;

    public IEnumerator<StringOrInt> GetEnumerator() => ((IEnumerable<StringOrInt>)steps).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() =>
        steps.Length == 0
            ? "(root)"
            : string.Join(".", steps.Select(s => s switch { string k => k, int i => $"[{i}]" }));
}
