namespace DataFirst.Lodash;

public static partial class _
{
    public static DataMap GroupBy(DataValue coll, Func<DataValue, string> f)
    {
        var builder = DataMap.CreateBuilder();

        switch (coll)
        {
            case DataMap m:
                foreach (var group in m.Values.GroupBy(f))
                    builder.Set(group.Key, DataList.Create(group));
                break;
            case DataList l:
                foreach (var group in l.GroupBy(f))
                    builder.Set(group.Key, DataList.Create(group));
                break;
            default:
                throw new InvalidOperationException($"Cannot GroupBy a {coll.Describe()}");
        }

        return builder.ToDataMap();
    }

    public static DataMap GroupBy(DataValue coll, string idKey) =>
        GroupBy(coll, row => Get<string>(row, idKey));
}
