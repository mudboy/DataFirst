using System.Collections.Immutable;

namespace DataFirst;

public static class List
{
    public static ImmutableList<T> Of<T>(params T[] values)
    {
        return ImmutableList.Create(values);
    }
}

public static partial class _
{
    public static T Get<T>(ImmutableList<T> list, int index) => list[index];

    public static ImmutableList<T> Set<T>(ImmutableList<T> list, int index, T value) => list.SetItem(index, value);
    
    public static ImmutableList<R> Map<T, R>(ImmutableList<T> list, Func<T, R> f) =>
        list.Select(f).ToImmutableList();

    public static ImmutableDictionary<string, object> KeyBy(ImmutableList<ImmutableDictionary<string, object>> maps, string key) =>
        maps.Aggregate(ImmutableDictionary.CreateBuilder<string,object>(), (builder, obj) =>
        {
            builder[_.Get<string>(obj, key)] = obj;
            return builder;
        }).ToImmutable();

    public static ImmutableList<T> Filter<T>(ImmutableList<T> list, Func<T, bool> predicate)
    {
        return list.Where(predicate).ToImmutableList();
    }
}