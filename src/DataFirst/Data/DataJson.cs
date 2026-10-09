using System.Text;
using System.Text.Json;

namespace DataFirst;

/// JSON serialisation for the generic representation.
///
/// System.Text.Json would serialise the union wrapper's own shape rather than the
/// data inside it, so the union is walked explicitly.
public static class DataJson
{
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
