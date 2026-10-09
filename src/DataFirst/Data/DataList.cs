using System.Collections;
using System.Collections.Immutable;

namespace DataFirst;

/// <summary>
/// An immutable, positionally indexed list of <see cref="DataValue"/>s.
/// </summary>
/// <remarks>
/// Wraps <see cref="ImmutableList{T}"/> for the same reason <see cref="DataMap"/> wraps
/// <see cref="ImmutableDictionary{TKey, TValue}"/>: structural equality, which the underlying
/// collection does not provide.
/// </remarks>
/// <example>
/// <code>
/// var authors = List.Of("alan-moore", "dave-gibbons");
/// authors[1]                        // "dave-gibbons"
/// authors.Add("john-higgins")       // a new, longer list; authors is unchanged
/// authors.Equals(List.Of("alan-moore", "dave-gibbons"))   // true: equality is structural
/// </code>
/// </example>
public sealed class DataList : IEquatable<DataList>, IEnumerable<DataValue>
{
    /// <summary>The list with no elements.</summary>
    public static readonly DataList Empty = new(ImmutableList<DataValue>.Empty);

    private readonly ImmutableList<DataValue> items;

    private DataList(ImmutableList<DataValue> items) => this.items = items;

    /// <summary>
    /// Builds a list from a sequence of values.
    /// </summary>
    /// <param name="values">The elements, in order. The sequence is copied.</param>
    /// <returns>A new list holding the elements.</returns>
    /// <example>
    /// <code>
    /// DataList.Create(new DataValue[] { "a", 1L })   // ["a", 1]
    /// </code>
    /// </example>
    public static DataList Create(IEnumerable<DataValue> values) => new(values.ToImmutableList());

    /// <summary>The number of elements.</summary>
    public int Count => items.Count;

    /// <summary>True when the list has no elements.</summary>
    public bool IsEmpty => items.IsEmpty;

    /// <summary>
    /// The element at an index.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative or not less than <see cref="Count"/>.</exception>
    /// <example>
    /// <code>
    /// List.Of("a", "b")[1]   // "b"
    /// List.Of("a", "b")[2]   // throws ArgumentOutOfRangeException
    /// </code>
    /// </example>
    public DataValue this[int index] =>
        index >= 0 && index < items.Count
            ? items[index]
            : throw new ArgumentOutOfRangeException(
                nameof(index), $"Index {index} is outside a list of {items.Count}");

    /// <summary>
    /// Appends a value.
    /// </summary>
    /// <param name="value">The value to add at the end.</param>
    /// <returns>A new list one element longer; this list is not modified.</returns>
    /// <example>
    /// <code>
    /// List.Of("a").Add("b")   // ["a", "b"]
    /// </code>
    /// </example>
    public DataList Add(DataValue value) => new(items.Add(value));

    /// <summary>
    /// Replaces the element at an index.
    /// </summary>
    /// <param name="index">The zero-based index of an existing element.</param>
    /// <param name="value">The replacement value.</param>
    /// <returns>A new list with the element replaced; this list is not modified.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the list.</exception>
    /// <example>
    /// <code>
    /// List.Of("a", "b").SetItem(0, "z")   // ["z", "b"]
    /// </code>
    /// </example>
    public DataList SetItem(int index, DataValue value) => new(items.SetItem(index, value));

    /// <summary>
    /// Inserts a value before the element at an index.
    /// </summary>
    /// <param name="index">The zero-based index to insert at; <see cref="Count"/> appends.</param>
    /// <param name="value">The value to insert.</param>
    /// <returns>A new list one element longer; this list is not modified.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative or greater than <see cref="Count"/>.</exception>
    /// <example>
    /// <code>
    /// List.Of("a", "c").Insert(1, "b")   // ["a", "b", "c"]
    /// </code>
    /// </example>
    public DataList Insert(int index, DataValue value) => new(items.Insert(index, value));

    /// <summary>
    /// Pads with <see cref="DataNull"/> so an index past the end can be written to.
    /// </summary>
    /// <param name="length">The length to pad the list to.</param>
    /// <returns>This list when it is already at least that long; otherwise a new list extended with nulls.</returns>
    /// <example>
    /// <code>
    /// List.Of("a").PadTo(3)   // ["a", null, null]
    /// </code>
    /// </example>
    public DataList PadTo(int length) =>
        length <= items.Count
            ? this
            : new(items.AddRange(Enumerable.Repeat<DataValue>(DataNull.Instance, length - items.Count)));

    /// <summary>
    /// Structural equality: the same length and equal elements in the same positions.
    /// </summary>
    /// <param name="other">The list to compare with.</param>
    /// <returns>True when the lists are equal.</returns>
    public bool Equals(DataList? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || other.items.Count != items.Count) return false;

        for (var i = 0; i < items.Count; i++)
            if (!items[i].Equals(other.items[i])) return false;

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DataList);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in items) hash.Add(item);
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public IEnumerator<DataValue> GetEnumerator() => items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// The list as JSON, so assertion failures show the actual content.
    /// </summary>
    public override string ToString() => DataJson.Serialize(this);
}
