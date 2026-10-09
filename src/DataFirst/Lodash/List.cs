namespace DataFirst.Lodash;

/// <summary>
/// Literal syntax for lists.
/// </summary>
public static class List
{
    /// <summary>
    /// Builds a list from its elements.
    /// </summary>
    /// <param name="values">The elements, in order.</param>
    /// <returns>A new list.</returns>
    /// <example>
    /// <code>
    /// List.Of("alan-moore", "dave-gibbons")
    /// </code>
    /// </example>
    public static DataList Of(params DataValue[] values) => DataList.Create(values);
}
