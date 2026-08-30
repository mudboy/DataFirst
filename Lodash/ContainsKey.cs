namespace DataFirst.Lodash;

public static partial class _
{
    public static bool ContainsKey(StringMap map, string key) =>
        ContainsKey(map, [key]);

    public static bool ContainsKey(StringMap map, List<string> pathKey) =>
        pathKey switch
        {
            [] => false, // empty list can't contain the key logically
            [var k] => map.ContainsKey(k), // list has single item, so check it
            [var k, .. var rest]
                => // peel off the first item in the path and check it stop if not in map else continue to check the rest of the list
                map.ContainsKey(k) switch
                {
                    true => ContainsKey(Get<StringMap>(map, k), rest),
                    _ => false
                }
        };
}