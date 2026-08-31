namespace DataFirst.Lodash;

/// A reusable, typed accessor for one location in a structure, so a path can be
/// named once and applied to many values.
public static class Getter
{
    public static Getter<T> Create<T>(StringOrInt key) => new KeyGetter<T>(key);

    public static Getter<T> Create<T>(IReadOnlyList<StringOrInt> keyPath) => new PathGetter<T>(keyPath);
}

public interface Getter<out T>
{
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
