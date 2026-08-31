using System.Collections;
using System.Collections.Immutable;

namespace DataFirst;

/// An immutable, positionally indexed list of DataValues.
///
/// Wraps ImmutableList for the same reason DataMap wraps ImmutableDictionary:
/// structural equality, which the underlying collection does not provide.
public sealed class DataList : IEquatable<DataList>, IEnumerable<DataValue>
{
    public static readonly DataList Empty = new(ImmutableList<DataValue>.Empty);

    private readonly ImmutableList<DataValue> items;

    private DataList(ImmutableList<DataValue> items) => this.items = items;

    public static DataList Create(IEnumerable<DataValue> values) => new(values.ToImmutableList());

    public int Count => items.Count;
    public bool IsEmpty => items.IsEmpty;

    public DataValue this[int index] =>
        index >= 0 && index < items.Count
            ? items[index]
            : throw new ArgumentOutOfRangeException(
                nameof(index), $"Index {index} is outside a list of {items.Count}");

    public DataList Add(DataValue value) => new(items.Add(value));
    public DataList SetItem(int index, DataValue value) => new(items.SetItem(index, value));
    public DataList Insert(int index, DataValue value) => new(items.Insert(index, value));

    /// Pads with DataNull so an index past the end can be written to.
    public DataList PadTo(int length) =>
        length <= items.Count
            ? this
            : new(items.AddRange(Enumerable.Repeat<DataValue>(DataNull.Instance, length - items.Count)));

    public bool Equals(DataList? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || other.items.Count != items.Count) return false;

        for (var i = 0; i < items.Count; i++)
            if (!items[i].Equals(other.items[i])) return false;

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as DataList);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in items) hash.Add(item);
        return hash.ToHashCode();
    }

    public IEnumerator<DataValue> GetEnumerator() => items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// JSON, so assertion failures show the actual content.
    public override string ToString() => DataJson.Serialize(this);
}
