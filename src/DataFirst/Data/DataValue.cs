namespace DataFirst;

/// <summary>
/// A value in the generic data representation: JSON's value set, as a union.
/// </summary>
/// <remarks>
/// Every case is a distinct type, so switches over <c>DataValue</c> are checked for
/// exhaustiveness by the compiler rather than falling through to a runtime cast.
/// <para>
/// Beware <c>default(DataValue)</c>: a union is a struct, and its default matches no
/// case at all, throwing <c>SwitchExpressionException</c>. Never leave one uninitialised
/// or hand one out of a failed lookup; use <see cref="DataNull.Instance"/> for absent values.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// DataValue title = "Watchmen";
/// DataValue year = 1987L;
/// DataValue authors = List.Of("alan-moore");
///
/// var kind = title switch { string => "text", long => "number", DataMap or DataList => "composite", _ => "other" };
/// </code>
/// </example>
public union DataValue(DataNull, string, long, double, bool, DataMap, DataList);

/// <summary>
/// The explicit absence of a value, so that "no value" is a case you must handle
/// rather than a null reference that slips through.
/// </summary>
public sealed record DataNull
{
    /// <summary>The single shared instance.</summary>
    public static readonly DataNull Instance = new();

    /// <inheritdoc/>
    public override string ToString() => "null";
}

/// <summary>
/// Helpers for working with <see cref="DataValue"/>.
/// </summary>
public static class DataValues
{
    /// <summary>
    /// Unwraps a value to the underlying CLR value, boxing scalars.
    /// </summary>
    /// <remarks>
    /// Numbers come back as <see cref="long"/> or <see cref="double"/>; there is no int case.
    /// </remarks>
    /// <param name="value">The value to unwrap.</param>
    /// <returns>The string, long, double, bool, <see cref="DataMap"/> or <see cref="DataList"/>; null for <see cref="DataNull"/>.</returns>
    /// <example>
    /// <code>
    /// ((DataValue)"Watchmen").Unwrap()           // "Watchmen", as an object
    /// ((DataValue)DataNull.Instance).Unwrap()    // null
    /// </code>
    /// </example>
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

    /// <summary>
    /// Unwraps to an expected type, failing with the actual case rather than a bare <see cref="InvalidCastException"/>.
    /// </summary>
    /// <typeparam name="T">The type the value is expected to have.</typeparam>
    /// <param name="value">The value to unwrap.</param>
    /// <returns>The underlying value as a <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">The value is not a <typeparamref name="T"/>; the message names what it is.</exception>
    /// <example>
    /// <code>
    /// ((DataValue)"Watchmen").As&lt;string&gt;()   // "Watchmen"
    /// ((DataValue)"Watchmen").As&lt;long&gt;()     // throws: Expected Int64 but found string
    /// </code>
    /// </example>
    public static T As<T>(this DataValue value) =>
        value.Unwrap() switch
        {
            T typed => typed,
            _ => throw new InvalidOperationException(
                $"Expected {typeof(T).Name} but found {value.Describe()}")
        };

    /// <summary>
    /// Tests for the two composite cases: the things a path can descend into.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns>True for a <see cref="DataMap"/> or <see cref="DataList"/>.</returns>
    /// <example>
    /// <code>
    /// ((DataValue)List.Of(1)).IsComposite()   // true
    /// ((DataValue)"text").IsComposite()       // false
    /// </code>
    /// </example>
    public static bool IsComposite(this DataValue value) =>
        value is DataMap or DataList;

    /// <summary>
    /// Describes a value's kind for an error message.
    /// </summary>
    /// <param name="value">The value to describe.</param>
    /// <returns>For example <c>string</c>, <c>number (long)</c>, <c>map[2]</c> or <c>list[0]</c>.</returns>
    /// <example>
    /// <code>
    /// ((DataValue)Map.Of(("a", 1), ("b", 2))).Describe()   // map[2]
    /// </code>
    /// </example>
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
