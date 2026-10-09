using DataFirst.Lodash;
using FsCheck;
using FsCheck.Fluent;

namespace DataFirst.Testing;

/// Generators for the generic data representation.
///
/// Keys and strings come from deliberately small alphabets so that independently
/// generated values collide often -- which is what exercises overwrite, overlap and
/// conflict paths. Doubles are always fractional so they cannot be confused with
/// longs after a JSON round trip, and strings avoid lone surrogates, which JSON
/// cannot represent.
public static class Gens
{
    private static readonly string[] KeyNames = ["a", "b", "c", "d", "e"];
    private static readonly string[] Words = ["", "x", "Watchmen", "7 Habits", "quote\"d", "back\\slash", "new\nline", "é☃", "978-1779501127"];

    public static Gen<string> Key => Gen.Elements(KeyNames);
    public static Gen<string> Word => Gen.Elements(Words);

    public static Gen<DataValue> Scalar =>
        Gen.OneOf(
            Gen.Constant<DataValue>(DataNull.Instance),
            Word.Select(s => (DataValue)s),
            Gen.Choose(-1000, 1000).Select(n => (DataValue)(long)n),
            Gen.Choose(-1000, 1000).Select(n => (DataValue)(n + 0.5)),
            Gen.Elements(true, false).Select(b => (DataValue)b));

    public static Gen<DataValue> Value => Gen.Sized(size => ValueOfSize(Math.Min(size, 12)));

    private static Gen<DataValue> ValueOfSize(int size) =>
        size <= 0
            ? Scalar
            : Gen.Frequency(
                (3, Scalar),
                (1, MapOfSize(size / 2).Select(m => (DataValue)m)),
                (1, ListOfSize(size / 2).Select(l => (DataValue)l)));

    private static Gen<DataMap> MapOfSize(int size) =>
        Gen.Choose(0, 4).SelectMany(count =>
            Gen.Zip(Key, ValueOfSize(size)).ArrayOf(count).Select(pairs =>
            {
                var builder = DataMap.CreateBuilder();
                foreach (var (key, value) in pairs) builder.Set(key, value);
                return builder.ToDataMap();
            }));

    private static Gen<DataList> ListOfSize(int size) =>
        Gen.Choose(0, 4).SelectMany(count =>
            ValueOfSize(size).ArrayOf(count).Select(DataList.Create));

    public static Gen<DataMap> Map => Gen.Sized(size => MapOfSize(Math.Min(size, 12)));
    public static Gen<DataList> List => Gen.Sized(size => ListOfSize(Math.Min(size, 12)));

    public static Gen<StringOrInt> Step =>
        Gen.OneOf(Key.Select(k => (StringOrInt)k), Gen.Choose(0, 4).Select(i => (StringOrInt)i));

    public static Gen<DataPath> Path =>
        Gen.Choose(0, 4).SelectMany(n => Step.ArrayOf(n).Select(steps => DataPath.Of(steps)));

    /// A non-empty map-only path, which is always writable into a map.
    public static Gen<DataPath> KeyPath =>
        Gen.Choose(1, 4).SelectMany(n => Key.ArrayOf(n).Select(keys => DataPath.Of(keys.Select(k => (StringOrInt)k))));

    /// A value together with a path that exists inside it.
    public static Gen<(DataMap Map, DataPath Path)> MapWithExistingPath =>
        Map.Where(m => !m.IsEmpty).SelectMany(m =>
        {
            var paths = _.InformationPaths(m).Where(p => p.Count > 0).ToArray();
            return paths.Length == 0
                ? Gen.Constant((m, DataPath.Of("a")))
                : Gen.Elements(paths).Select(p => (m, p));
        });
}
