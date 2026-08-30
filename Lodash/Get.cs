namespace DataFirst.Lodash;


public union StringOrInt(string, int)
{
public static implicit operator StringOrInt(int x) => x;
public static implicit operator StringOrInt(string x) => x; 
}

public static partial class _
{

    
    public static T Get<T>(object map, StringOrInt key) =>
        (T)Get(map, [key]);

    public static T Get<T>(object map, List<StringOrInt> keyPath) =>
        (T)Get(map, keyPath);

    public static object Get(object obj, List<StringOrInt> keyPath)
    {
        return keyPath switch
        {
            [] => obj,
            [var k] => Get(obj, k),
            [var k, .. var rest] => Get(Get(obj, k), rest)
        };
    }

    public static object Get(object obj, StringOrInt key)
    {
        return (obj, key) switch
        {
            (StringMap m, string k) => m[k],
            (IndexedList l, int i) => l[i],
            (IndexedList l, string si) => l[int.Parse(si)],
            _ => throw new Exception($"Unknown combo: {obj.GetType()} :: {key.GetType()}")
        };
    }
}