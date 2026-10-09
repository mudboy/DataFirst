using System.Collections;
using System.Collections.Immutable;

namespace DataFirst;

/// <summary>
/// An immutable string-keyed map of <see cref="DataValue"/>s.
/// </summary>
/// <remarks>
/// Wraps <see cref="ImmutableDictionary{TKey, TValue}"/> for two reasons. Structural equality: the
/// underlying dictionary compares by reference, which would make <c>Diff</c> report every untouched
/// nested node as changed. And insertion order: <c>ImmutableDictionary</c> iterates in
/// hash order, which would make <c>Values</c>, JSON output and anything built from them
/// vary for no reason the data explains.
/// <para>
/// Order is a presentation concern only: equality ignores it, as a map should.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var book = Map.Of(("title", "Watchmen"), ("publicationYear", 1987));
/// book["title"]                              // "Watchmen"
/// book.SetItem("publicationYear", 1986)      // a new map; book is unchanged
/// book.Equals(Map.Of(("publicationYear", 1987), ("title", "Watchmen")))   // true: key order is ignored
/// </code>
/// </example>
public sealed class DataMap : IEquatable<DataMap>, IEnumerable<KeyValuePair<string, DataValue>>
{
    /// <summary>The map with no entries.</summary>
    public static readonly DataMap Empty =
        new(ImmutableDictionary<string, DataValue>.Empty, ImmutableList<string>.Empty);

    private readonly ImmutableDictionary<string, DataValue> entries;
    private readonly ImmutableList<string> order;

    private DataMap(ImmutableDictionary<string, DataValue> entries, ImmutableList<string> order)
    {
        this.entries = entries;
        this.order = order;
    }

    /// <summary>The number of entries.</summary>
    public int Count => entries.Count;

    /// <summary>True when the map has no entries.</summary>
    public bool IsEmpty => entries.IsEmpty;

    /// <summary>The keys, in insertion order.</summary>
    public IEnumerable<string> Keys => order;

    /// <summary>The values, in the insertion order of their keys.</summary>
    public IEnumerable<DataValue> Values => order.Select(key => entries[key]);

    /// <summary>
    /// The value stored under a key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <exception cref="KeyNotFoundException">The key is not present. Use <see cref="GetOrNull"/> to get null instead.</exception>
    /// <example>
    /// <code>
    /// Map.Of(("a", 1))["a"]   // 1
    /// Map.Of(("a", 1))["b"]   // throws KeyNotFoundException
    /// </code>
    /// </example>
    public DataValue this[string key] =>
        entries.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"No key '{key}' in map with keys [{string.Join(", ", entries.Keys)}]");

    /// <summary>
    /// Tests whether a key is present.
    /// </summary>
    /// <param name="key">The key to look for.</param>
    /// <returns>True when the map has an entry for the key.</returns>
    public bool ContainsKey(string key) => entries.ContainsKey(key);

    /// <summary>
    /// Reads a key, returning <see cref="DataNull"/> rather than <c>default(DataValue)</c>, which would match no union case.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The value, or <see cref="DataNull"/> when the key is absent.</returns>
    /// <example>
    /// <code>
    /// Map.Of(("a", 1)).GetOrNull("b")   // null
    /// </code>
    /// </example>
    public DataValue GetOrNull(string key) =>
        entries.TryGetValue(key, out var value) ? value : DataNull.Instance;

    /// <summary>
    /// Sets a key. Overwriting a key keeps its position; a new key is appended.
    /// </summary>
    /// <param name="key">The key to write.</param>
    /// <param name="value">The value to store.</param>
    /// <returns>A new map with the entry written; this map is not modified.</returns>
    /// <example>
    /// <code>
    /// Map.Of(("a", 1), ("b", 2)).SetItem("a", 9)   // { a: 9, b: 2 }
    /// Map.Of(("a", 1)).SetItem("b", 2)             // { a: 1, b: 2 }
    /// </code>
    /// </example>
    public DataMap SetItem(string key, DataValue value) =>
        new(entries.SetItem(key, value), entries.ContainsKey(key) ? order : order.Add(key));

    /// <summary>
    /// Removes a key.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>A new map without the entry, or this map when the key is not present.</returns>
    /// <example>
    /// <code>
    /// Map.Of(("a", 1), ("b", 2)).Remove("a")   // { b: 2 }
    /// </code>
    /// </example>
    public DataMap Remove(string key) =>
        entries.ContainsKey(key) ? new(entries.Remove(key), order.Remove(key)) : this;

    /// <summary>
    /// Starts a builder, for assembling a map from many entries without a copy per entry.
    /// </summary>
    /// <returns>An empty builder. Do not let it escape the function that creates it.</returns>
    /// <example>
    /// <code>
    /// DataMap.CreateBuilder().Set("a", 1).Set("b", 2).ToDataMap()   // { a: 1, b: 2 }
    /// </code>
    /// </example>
    public static Builder CreateBuilder() => new();

    /// <summary>
    /// Assembles a <see cref="DataMap"/> by adding entries, then freezes it with <see cref="ToDataMap"/>.
    /// </summary>
    public sealed class Builder
    {
        private readonly ImmutableDictionary<string, DataValue>.Builder inner =
            ImmutableDictionary.CreateBuilder<string, DataValue>();

        private readonly ImmutableList<string>.Builder order = ImmutableList.CreateBuilder<string>();

        /// <summary>
        /// Adds or overwrites an entry. Last write wins, matching lodash rather than throwing on
        /// duplicates. A repeated key keeps its original position.
        /// </summary>
        /// <param name="key">The key to write.</param>
        /// <param name="value">The value to store.</param>
        /// <returns>This builder, so calls can be chained.</returns>
        public Builder Set(string key, DataValue value)
        {
            if (!inner.ContainsKey(key)) order.Add(key);
            inner[key] = value;
            return this;
        }

        /// <summary>
        /// Freezes the entries added so far.
        /// </summary>
        /// <returns>An immutable map holding them.</returns>
        public DataMap ToDataMap() => new(inner.ToImmutable(), order.ToImmutable());
    }

    /// <summary>
    /// Structural equality: the same keys with equal values, regardless of insertion order.
    /// </summary>
    /// <param name="other">The map to compare with.</param>
    /// <returns>True when the maps are equal.</returns>
    public bool Equals(DataMap? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || other.entries.Count != entries.Count) return false;

        foreach (var (key, value) in entries)
            if (!other.entries.TryGetValue(key, out var otherValue) || !value.Equals(otherValue))
                return false;

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DataMap);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // XOR so the hash does not depend on enumeration order.
        var hash = 0;
        foreach (var (key, value) in entries) hash ^= HashCode.Combine(key, value);
        return hash;
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, DataValue>> GetEnumerator() =>
        order.Select(key => new KeyValuePair<string, DataValue>(key, entries[key])).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// The map as JSON, so assertion failures show the actual content.
    /// </summary>
    public override string ToString() => DataJson.Serialize(this);
}
