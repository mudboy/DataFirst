namespace DataFirst.Lodash;

public static partial class _
{
    public static StringMap Update(StringMap map, string path, Func<object, object> func) =>
        Set(map, path, func(Get(map, path)));
}