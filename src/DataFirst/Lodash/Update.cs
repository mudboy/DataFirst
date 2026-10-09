namespace DataFirst.Lodash;

public static partial class _
{
    public static DataValue Update(DataValue obj, StringOrInt key, Func<DataValue, DataValue> f) =>
        Set(obj, key, f(Get(obj, key)));

    public static DataValue Update(DataValue obj, IReadOnlyList<StringOrInt> path, Func<DataValue, DataValue> f) =>
        Set(obj, path, f(Get(obj, path)));

    public static DataMap Update(DataMap map, StringOrInt key, Func<DataValue, DataValue> f) =>
        Update((DataValue)map, key, f).As<DataMap>();

    public static DataMap Update(DataMap map, IReadOnlyList<StringOrInt> path, Func<DataValue, DataValue> f) =>
        Update((DataValue)map, path, f).As<DataMap>();
}
