using DataFirst.Lodash;
using FsCheck;
using FsCheck.Fluent;

namespace DataFirst.Testing;

/// Registers the generators above so [Property] methods can take these types.
public static class Arbs
{
    public static Arbitrary<DataValue> Value() => Gens.Value.ToArbitrary();
    public static Arbitrary<DataMap> Map() => Gens.Map.ToArbitrary();
    public static Arbitrary<DataList> List() => Gens.List.ToArbitrary();
    public static Arbitrary<DataPath> Path() => Gens.Path.ToArbitrary();
    public static Arbitrary<StringOrInt> Step() => Gens.Step.ToArbitrary();
}
