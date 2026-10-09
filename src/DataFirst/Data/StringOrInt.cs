namespace DataFirst;

/// <summary>
/// One step in a path: a map key or a list index.
/// </summary>
/// <remarks>
/// Lets a single path address both shapes, so <c>["catalog", "books", 0, "title"]</c>
/// descends maps and lists alike.
/// </remarks>
/// <example>
/// <code>
/// StringOrInt key = "title";   // a map key
/// StringOrInt index = 0;       // a list index
/// </code>
/// </example>
public union StringOrInt(string, int);

/// <summary>
/// Helpers for <see cref="StringOrInt"/> path steps.
/// </summary>
public static class PathKeys
{
    /// <summary>
    /// Describes a step for an error message.
    /// </summary>
    /// <param name="key">The step to describe.</param>
    /// <returns><c>key 'name'</c> for a string, <c>index 3</c> for an index.</returns>
    /// <example>
    /// <code>
    /// ((StringOrInt)"title").Describe()   // key 'title'
    /// ((StringOrInt)3).Describe()         // index 3
    /// </code>
    /// </example>
    public static string Describe(this StringOrInt key) =>
        key switch
        {
            string s => $"key '{s}'",
            int i => $"index {i}"
        };

    /// <summary>
    /// Reads a step as a list index.
    /// </summary>
    /// <remarks>
    /// List indices arrive as strings when they come from <c>Keys</c> on JSON-ish data,
    /// so either spelling is accepted when indexing a list.
    /// </remarks>
    /// <param name="key">The step to read.</param>
    /// <param name="index">The index, when the step is one; otherwise 0.</param>
    /// <returns>True for an int, or for a string that parses as one.</returns>
    /// <example>
    /// <code>
    /// ((StringOrInt)2).TryAsIndex(out var i)          // true, i == 2
    /// ((StringOrInt)"2").TryAsIndex(out var j)        // true, j == 2
    /// ((StringOrInt)"title").TryAsIndex(out var k)    // false
    /// </code>
    /// </example>
    public static bool TryAsIndex(this StringOrInt key, out int index)
    {
        switch (key)
        {
            case int i:
                index = i;
                return true;
            case string s when int.TryParse(s, out var parsed):
                index = parsed;
                return true;
            default:
                index = 0;
                return false;
        }
    }
}
