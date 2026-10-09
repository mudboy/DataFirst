using DataFirst.Lodash;
using FsCheck;
using FsCheck.Fluent;

namespace DataFirst.Testing;

/// Lists drawn from a tiny pool of scalars, so duplicates and overlap between two
/// lists are the norm rather than a rare accident.
public static class ListGens
{
    public static Gen<DataValue> Pooled =>
        Gen.Elements<DataValue>("a", "b", "c", 1L, 2L, 1.5, true, DataNull.Instance, DataFirst.Lodash.Map.Of(("k", 1)), DataFirst.Lodash.List.Of(1, 2));

    public static Gen<DataList> Pool =>
        Gen.Choose(0, 8).SelectMany(n => Pooled.ArrayOf(n).Select(DataList.Create));

    public static Gen<DataList> Longs =>
        Gen.Choose(0, 8).SelectMany(n => Gen.Choose(-1000, 1000).ArrayOf(n)
            .Select(xs => DataList.Create(xs.Select(x => (DataValue)(long)x).ToList())));

    public static Gen<DataList> Doubles =>
        Gen.Choose(0, 8).SelectMany(n => Gen.Choose(-1000, 1000).ArrayOf(n)
            .Select(xs => DataList.Create(xs.Select(x => (DataValue)(x + 0.5)).ToList())));

    public static Gen<DataList> Nested =>
        Gen.Choose(0, 5).SelectMany(n => Gen.OneOf(Pooled, Pool.Select(l => (DataValue)l)).ArrayOf(n).Select(DataList.Create));
}
