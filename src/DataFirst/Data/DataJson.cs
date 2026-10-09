using System.Text;
using System.Text.Json;

namespace DataFirst;

/// <summary>
/// JSON serialisation for the generic representation.
/// </summary>
/// <remarks>
/// <c>System.Text.Json</c> would serialise the union wrapper's own shape rather than the
/// data inside it, so the union is walked explicitly.
/// </remarks>
public static class DataJson
{
    /// <summary>
    /// Writes a value as compact JSON.
    /// </summary>
    /// <remarks>
    /// Map keys keep their insertion order. Doubles are written as the shortest text
    /// that reads back to the same number, so a whole-valued double such as 2.0 comes out as
    /// <c>2</c>, indistinguishable from a long.
    /// </remarks>
    /// <param name="value">The value to write.</param>
    /// <returns>The JSON text.</returns>
    /// <example>
    /// <code>
    /// DataJson.Serialize(Map.Of(("title", "Watchmen"), ("authorIds", List.Of("alan-moore"))))
    /// // {"title":"Watchmen","authorIds":["alan-moore"]}
    /// </code>
    /// </example>
    public static string Serialize(DataValue value)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer)) Write(writer, value);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void Write(Utf8JsonWriter writer, DataValue value)
    {
        switch (value)
        {
            case DataNull:
                writer.WriteNullValue();
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case long n:
                writer.WriteNumberValue(n);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case DataMap map:
                writer.WriteStartObject();
                foreach (var (key, item) in map)
                {
                    writer.WritePropertyName(key);
                    Write(writer, item);
                }
                writer.WriteEndObject();
                break;
            case DataList list:
                writer.WriteStartArray();
                foreach (var item in list) Write(writer, item);
                writer.WriteEndArray();
                break;
        }
    }
}
