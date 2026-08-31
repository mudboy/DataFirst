namespace DataFirst;

/// One step in a path: a map key or a list index.
///
/// Lets a single path address both shapes, so ["catalog", "books", 0, "title"]
/// descends maps and lists alike.
public union StringOrInt(string, int);

public static class PathKeys
{
    public static string Describe(this StringOrInt key) =>
        key switch
        {
            string s => $"key '{s}'",
            int i => $"index {i}"
        };

    /// List indices arrive as strings when they come from Keys() on JSON-ish data,
    /// so accept either spelling when indexing a list.
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
