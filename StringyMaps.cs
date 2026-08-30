using System.Collections.Immutable;

namespace DataFirst;


public union ValueX(StringValue, BoolValue, ListValue, NumberValue, SMap);
public record class Cat(string Name);
public record class Dog(string Name);
public record class Bird(string Name);


public record class StringValue(string item)
{
    public static implicit operator string(StringValue valuexx) => valuexx.item;
    public static implicit operator StringValue(string valuexx) => new(valuexx);
}

public record SMap(ImmutableDictionary<string, ValueX> value)
{
    public static implicit operator ImmutableDictionary<string, ValueX>(SMap map) => map.value;
    public static implicit operator SMap(ImmutableDictionary<string, ValueX> map) => new (map);
    public static SMap New() => new(ImmutableDictionary<string, ValueX>.Empty);

    public SMap Add(string key, ValueX v) => value.Add(key, v);
}

public record NumberValue(decimal value)
{
    public static implicit operator decimal(NumberValue value) => value.value;
    public static implicit operator NumberValue(decimal value) => new(value);
}

public record BoolValue(bool value)
{
    public static implicit operator bool(BoolValue value) => value.value;
    public static implicit operator BoolValue(bool value) => new(value);
}

public record ListValue(List<ValueX> values)
{
    public static implicit operator List<ValueX>(ListValue valuex) => valuex.values;
    public static implicit operator ListValue(List<decimal> values) => new (values);
}

public static class Maps
{
    public static void Mains()
    {
        var x = SMap.New();
        var subMap = SMap.New();

        /*
        var updatedMap = x.Add("sd", "sdre")
            .Add("dksjkld", 123.3m)
            .Add("Sub", subMap);

        var xx = M(
                ("first", S("value")),
                            ("second", S("vv2")),
                            ("sub", 
                                M(
                                    ("ss", S("sadf")))));*/

    }

    public static StringValue S(string v) => new(v);
    public static SMap M(params IEnumerable<(string, ValueX)> pairs)
    {
        var x = SMap.New();
        return pairs.Aggregate(x, (map, pair) =>

            ((ValueX)pair.Item2) switch
            {
                BoolValue boolValue => map.Add(pair.Item1, boolValue),
                ListValue listValue => map.Add(pair.Item1, listValue),
                NumberValue numberValue => map.Add(pair.Item1, numberValue),
                SMap sx => map.Add(pair.Item1, sx),
                StringValue stringValue => map.Add(pair.Item1, stringValue),
            }
        );
    }
}
