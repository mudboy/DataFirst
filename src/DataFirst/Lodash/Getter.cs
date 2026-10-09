namespace DataFirst.Lodash;

/// <summary>
/// Creates reusable, typed accessors for one location in a structure, so a path can be
/// named once and applied to many values.
/// </summary>
public static class Getter
{
    /// <summary>
    /// Creates an accessor for a single key or index.
    /// </summary>
    /// <typeparam name="T">The type the value at the key is read as.</typeparam>
    /// <param name="key">A string key for a map, or an index for a list.</param>
    /// <returns>An accessor that reads <paramref name="key"/> from any value it is given.</returns>
    public static Getter<T> Create<T>(StringOrInt key) => new KeyGetter<T>(key);

    /// <summary>
    /// Creates an accessor for a path of keys and indices.
    /// </summary>
    /// <typeparam name="T">The type the value at the end of the path is read as.</typeparam>
    /// <param name="keyPath">The keys and indices to follow.</param>
    /// <returns>An accessor that walks <paramref name="keyPath"/> from any value it is given.</returns>
    public static Getter<T> Create<T>(IReadOnlyList<StringOrInt> keyPath) => new PathGetter<T>(keyPath);
}

/// <summary>
/// A typed accessor for one location in a structure.
/// </summary>
/// <typeparam name="T">The type of the value it reads.</typeparam>
public interface Getter<out T>
{
    /// <summary>
    /// Reads the location from a value.
    /// </summary>
    /// <param name="value">The value to read from.</param>
    /// <returns>The value at the location, as a <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">The location cannot be read from <paramref name="value"/>, or does not hold a <typeparamref name="T"/>.</exception>
    /// <exception cref="KeyNotFoundException">The location names a map key that is not present.</exception>
    T Get(DataValue value);
}

internal sealed class KeyGetter<T>(StringOrInt key) : Getter<T>
{
    public T Get(DataValue value) => _.Get<T>(value, key);
}

internal sealed class PathGetter<T>(IReadOnlyList<StringOrInt> keyPath) : Getter<T>
{
    public T Get(DataValue value) => _.Get<T>(value, keyPath);
}
