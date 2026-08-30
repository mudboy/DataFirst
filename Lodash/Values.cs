using System.Collections.Immutable;

namespace DataFirst.Lodash;

public static partial class _
{
    public static IndexedList Values(StringMap map) => map.Values.ToImmutableList();
}