using System.Collections;
using System.Collections.Immutable;

namespace DataFirst;

/// An immutable string-keyed map of DataValues.
///
/// Wraps ImmutableDictionary for two reasons. Structural equality: the underlying
/// dictionary compares by reference, which would make Diff report every untouched
/// nested node as changed. And insertion order: ImmutableDictionary iterates in
/// hash order, which would make Values(), JSON output and anything built from them
/// vary for no reason the data explains.
///
/// Order is a presentation concern only -- equality ignores it, as a map should.
public sealed class DataMap : IEquatable<DataMap>, IEnumerable<KeyValuePair<string, DataValue>>
{
    public static readonly DataMap Empty =
        new(ImmutableDictionary<string, DataValue>.Empty, ImmutableList<string>.Empty);

    private readonly ImmutableDictionary<string, DataValue> entries;
    private readonly ImmutableList<string> order;

    private DataMap(ImmutableDictionary<string, DataValue> entries, ImmutableList<string> order)
    {
        this.entries = entries;
        this.order = order;
    }

    public int Count => entries.Count;
    public bool IsEmpty => entries.IsEmpty;
    public IEnumerable<string> Keys => order;
    public IEnumerable<DataValue> Values => order.Select(key => entries[key]);

    public DataValue this[string key] =>
        entries.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"No key '{key}' in map with keys [{string.Join(", ", entries.Keys)}]");

    public bool ContainsKey(string key) => entries.ContainsKey(key);

    /// Returns DataNull rather than default(DataValue), which would match no union case.
    public DataValue GetOrNull(string key) =>
        entries.TryGetValue(key, out var value) ? value : DataNull.Instance;

    /// Overwriting a key keeps its position; a new key is appended.
    public DataMap SetItem(string key, DataValue value) =>
        new(entries.SetItem(key, value), entries.ContainsKey(key) ? order : order.Add(key));

    public DataMap Remove(string key) =>
        entries.ContainsKey(key) ? new(entries.Remove(key), order.Remove(key)) : this;

    public static Builder CreateBuilder() => new();

    public sealed class Builder
    {
        private readonly ImmutableDictionary<string, DataValue>.Builder inner =
            ImmutableDictionary.CreateBuilder<string, DataValue>();

        private readonly ImmutableList<string>.Builder order = ImmutableList.CreateBuilder<string>();

        /// Last write wins, matching lodash rather than throwing on duplicates.
        /// A repeated key keeps its original position.
        public Builder Set(string key, DataValue value)
        {
            if (!inner.ContainsKey(key)) order.Add(key);
            inner[key] = value;
            return this;
        }

        public DataMap ToDataMap() => new(inner.ToImmutable(), order.ToImmutable());
    }

    public bool Equals(DataMap? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || other.entries.Count != entries.Count) return false;

        foreach (var (key, value) in entries)
            if (!other.entries.TryGetValue(key, out var otherValue) || !value.Equals(otherValue))
                return false;

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as DataMap);

    public override int GetHashCode()
    {
        // XOR so the hash does not depend on enumeration order.
        var hash = 0;
        foreach (var (key, value) in entries) hash ^= HashCode.Combine(key, value);
        return hash;
    }

    public IEnumerator<KeyValuePair<string, DataValue>> GetEnumerator() =>
        order.Select(key => new KeyValuePair<string, DataValue>(key, entries[key])).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// JSON, so assertion failures show the actual content.
    public override string ToString() => DataJson.Serialize(this);
}
