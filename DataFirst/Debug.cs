namespace DataFirst;

/// Writes a value out as JSON so it can be looked at.
///
/// Generic data is the whole point here, so there is one function rather than an
/// overload per arity: anything worth dumping is already a DataValue, and several of
/// them are a map.
public static class Debug
{
    private const string Directory = "test-data";

    /// Writes to test-data/&lt;context&gt;.json, creating the directory if it is not there.
    /// Returns the path written, so a caller can say where to look.
    public static string Dump(string context, DataValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        System.IO.Directory.CreateDirectory(Directory);

        var path = Path.Combine(Directory, Path.ChangeExtension(context, ".json"));
        File.WriteAllText(path, DataJson.Serialize(value));

        return path;
    }
}
