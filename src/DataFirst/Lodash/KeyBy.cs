namespace DataFirst.Lodash;

public static partial class _
{
    /// Indexes a list's elements, or a map's values, by the key f gives each. Last
    /// write wins on duplicate keys, as lodash does, and a key keeps the position of
    /// its first appearance.
    public static DataMap KeyBy(DataValue coll, Func<DataValue, string> f) =>
        Elements(coll, "KeyBy")
            .Aggregate(DataMap.CreateBuilder(), (builder, element) => builder.Set(f(element), element))
            .ToDataMap();

    /// Indexes a list of maps by one of their fields.
    public static DataMap KeyBy(DataValue coll, string key) =>
        KeyBy(coll, row => Get<string>(row, key));
}
