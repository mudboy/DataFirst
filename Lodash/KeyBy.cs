namespace DataFirst.Lodash;

public static partial class _
{
    public static StringMap KeyBy(IndexedList maps, string key) =>
        maps.Aggregate(MapBuilder(), (builder, obj) =>
            Add(builder, Get<string>(obj, key), obj)).ToImmutable();
}