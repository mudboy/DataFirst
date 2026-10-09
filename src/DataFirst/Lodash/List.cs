namespace DataFirst.Lodash;

/// Literal syntax for lists: List.Of("alan-moore", "dave-gibbons").
public static class List
{
    public static DataList Of(params DataValue[] values) => DataList.Create(values);
}
