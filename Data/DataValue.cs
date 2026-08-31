namespace DataFirst;

/// A value in the generic data representation: JSON's value set, as a union.
///
/// Every case is a distinct type, so switches over DataValue are checked for
/// exhaustiveness by the compiler rather than falling through to a runtime cast.
///
/// Beware default(DataValue): a union is a struct, and its default matches no
/// case at all, throwing SwitchExpressionException. Never leave one uninitialised
/// or hand one out of a failed lookup -- use DataNull.Instance for absent values.
public union DataValue(DataNull, string, long, double, bool, DataMap, DataList);

/// The explicit absence of a value, so that "no value" is a case you must handle
/// rather than a null reference that slips through.
public sealed record DataNull
{
    public static readonly DataNull Instance = new();
    public override string ToString() => "null";
}

public static class DataValues
{
    /// Unwraps a DataValue to the underlying CLR value, boxing scalars.
    /// Numbers come back as long or double; there is no int case.
    public static object? Unwrap(this DataValue value) =>
        value switch
        {
            DataNull => null,
            string s => s,
            long n => n,
            double d => d,
            bool b => b,
            DataMap m => m,
            DataList l => l
        };

    /// Unwraps to an expected type, failing with the actual case rather than a
    /// bare InvalidCastException.
    public static T As<T>(this DataValue value) =>
        value.Unwrap() switch
        {
            T typed => typed,
            _ => throw new InvalidOperationException(
                $"Expected {typeof(T).Name} but found {value.Describe()}")
        };

    /// True for the two composite cases -- the things a path can descend into.
    public static bool IsComposite(this DataValue value) =>
        value is DataMap or DataList;

    public static string Describe(this DataValue value) =>
        value switch
        {
            DataNull => "null",
            string => "string",
            long => "number (long)",
            double => "number (double)",
            bool => "bool",
            DataMap m => $"map[{m.Count}]",
            DataList l => $"list[{l.Count}]"
        };
}
