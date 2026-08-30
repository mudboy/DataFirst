using System.Collections.Immutable;

namespace DataFirst.Lodash;

public static class List
{
    public static IndexedList Of(params object[] values)
    {
        return ImmutableList.Create(values);
    }
}