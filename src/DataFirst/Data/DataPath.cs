using System.Collections;

namespace DataFirst;

/// <summary>
/// A path through a structure, as a sequence of map keys and list indices.
/// </summary>
/// <remarks>
/// Exists as its own type rather than a bare list because the concurrency control
/// puts paths in sets and intersects them, which needs value equality and hashing.
/// </remarks>
/// <example>
/// <code>
/// var title = DataPath.Of("catalog", "booksByIsbn", "978-1779501127", "title");
/// var firstAuthor = DataPath.Of("authors", 0, "name");   // keys and indices mix freely
/// title.ToString()          // catalog.booksByIsbn.978-1779501127.title
/// firstAuthor.ToString()    // authors.[0].name
/// </code>
/// </example>
public sealed class DataPath : IEquatable<DataPath>, IReadOnlyList<StringOrInt>
{
    /// <summary>The empty path, which addresses a structure itself.</summary>
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

    /// <summary>
    /// Builds a path from its steps.
    /// </summary>
    /// <param name="steps">The map keys and list indices to follow, in order.</param>
    /// <returns>A new path. With no steps it equals <see cref="Root"/>.</returns>
    /// <example>
    /// <code>
    /// DataPath.Of("items", 1, "id")
    /// </code>
    /// </example>
    public static DataPath Of(params StringOrInt[] steps) => new(steps);

    /// <summary>
    /// Builds a path from a sequence of steps.
    /// </summary>
    /// <param name="steps">The map keys and list indices to follow, in order. The sequence is copied.</param>
    /// <returns>A new path.</returns>
    public static DataPath Of(IEnumerable<StringOrInt> steps) => new(steps.ToArray());

    /// <summary>The number of steps. The root has none.</summary>
    public int Count => steps.Length;

    /// <summary>
    /// The step at a position.
    /// </summary>
    /// <param name="index">The zero-based position in the path.</param>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the path.</exception>
    public StringOrInt this[int index] => steps[index];

    /// <summary>
    /// Extends the path by one step.
    /// </summary>
    /// <param name="step">The key or index to add on the end.</param>
    /// <returns>A new path with one more step on the end; this path is not modified.</returns>
    /// <example>
    /// <code>
    /// DataPath.Of("items").Then(0)   // items.[0]
    /// </code>
    /// </example>
    public DataPath Then(StringOrInt step) => new([.. steps, step]);

    /// <summary>
    /// Appends another path, for re-rooting an error reported against a nested value onto the whole structure.
    /// </summary>
    /// <param name="suffix">The path to add on the end.</param>
    /// <returns>A new path made of this path followed by <paramref name="suffix"/>.</returns>
    /// <example>
    /// <code>
    /// DataPath.Of("book").Then(DataPath.Of("authors", 0))   // book.authors.[0]
    /// </code>
    /// </example>
    public DataPath Then(DataPath suffix) => new([.. steps, .. suffix.steps]);

    /// <summary>
    /// Tests whether two paths address overlapping data: equal, or one inside the other.
    /// </summary>
    /// <remarks>
    /// Changing <c>items</c> and changing <c>items[1]</c> are not the same path, but they are
    /// not independent either: one replaces what the other is reaching into. Exact
    /// set intersection misses that and lets both changes through.
    /// </remarks>
    /// <param name="other">The path to compare with.</param>
    /// <returns>True when one path is a prefix of the other, or they are equal. The root overlaps everything.</returns>
    /// <example>
    /// <code>
    /// DataPath.Of("items").Overlaps(DataPath.Of("items", 1))      // true
    /// DataPath.Of("items", 0).Overlaps(DataPath.Of("items", 1))   // false: siblings are independent
    /// </code>
    /// </example>
    public bool Overlaps(DataPath other)
    {
        var shared = Math.Min(steps.Length, other.steps.Length);

        for (var i = 0; i < shared; i++)
            if (!steps[i].Equals(other.steps[i])) return false;

        return true;
    }

    /// <summary>
    /// Value equality: the same steps in the same order.
    /// </summary>
    /// <param name="other">The path to compare with.</param>
    /// <returns>True when the paths are equal.</returns>
    public bool Equals(DataPath? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || other.steps.Length != steps.Length || other.hash != hash) return false;

        for (var i = 0; i < steps.Length; i++)
            if (!steps[i].Equals(other.steps[i])) return false;

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DataPath);

    /// <inheritdoc/>
    public override int GetHashCode() => hash;

    /// <inheritdoc/>
    public IEnumerator<StringOrInt> GetEnumerator() => ((IEnumerable<StringOrInt>)steps).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// The path as text: keys joined by dots, indices in brackets, and <c>(root)</c> for the empty path.
    /// </summary>
    public override string ToString() =>
        steps.Length == 0
            ? "(root)"
            : string.Join(".", steps.Select(s => s switch { string k => k, int i => $"[{i}]" }));
}
