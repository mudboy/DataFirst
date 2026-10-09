using System.Text.RegularExpressions;

namespace DataFirst;

/// Validates data against a schema, where the schema is itself data.
///
/// This is what separates schema from representation: a schema is a DataMap that can
/// be built, stored, diffed and passed around like any other value, and this code
/// interprets it. Nothing about a book's shape is encoded in a C# type.
///
/// The schema language is a subset of JSON Schema:
///
///   type                  "null", "boolean", "integer", "number", "string",
///                         "array", "object" -- or a list of those
///   enum                  list of permitted values
///   const                 a single permitted value
///   properties            map of property name to schema
///   required              list of property names that must be present
///   additionalProperties  false to reject properties not named in properties
///   items                 schema every element must match
///   minItems, maxItems    bounds on list length
///   uniqueItems           true to reject duplicate elements
///   minimum, maximum      inclusive bounds on numbers
///   minLength, maxLength  bounds on string length
///   pattern               regular expression a string must match
///   allOf, anyOf          lists of schemas
///
/// As in JSON Schema, a keyword that does not apply to the value's type is ignored,
/// so minimum says nothing about a string. Every error is collected rather than
/// stopping at the first.
public static class Validation
{
    public static ValidationResult Validate(DataValue schema, DataValue data)
    {
        var errors = new List<ValidationError>();
        Check(schema, data, DataPath.Root, errors);
        return errors.Count == 0 ? Valid.Instance : new Invalid(errors);
    }

    /// Validates, throwing when the data does not conform. For boundaries where
    /// carrying on with bad data is not an option.
    public static DataValue ValidateOrThrow(DataValue schema, DataValue data) =>
        Validate(schema, data) switch
        {
            Valid => data,
            Invalid invalid => throw new SchemaViolationException(invalid)
        };

    private static void Check(DataValue schema, DataValue data, DataPath path, List<ValidationError> errors)
    {
        if (schema is not DataMap rules)
        {
            errors.Add(new ValidationError(path, $"schema must be a map, but was {schema.Describe()}"));
            return;
        }

        CheckType(rules, data, path, errors);
        CheckEnum(rules, data, path, errors);
        CheckObject(rules, data, path, errors);
        CheckArray(rules, data, path, errors);
        CheckNumber(rules, data, path, errors);
        CheckString(rules, data, path, errors);
        CheckCombinators(rules, data, path, errors);
    }

    private static void CheckType(DataMap rules, DataValue data, DataPath path, List<ValidationError> errors)
    {
        if (!rules.ContainsKey("type")) return;

        IReadOnlyList<string> permitted = rules["type"] switch
        {
            string single => [single],
            DataList many => many.Select(t => t.As<string>()).ToList(),
            var other => throw new ArgumentException($"type must be a string or list, but was {other.Describe()}")
        };

        if (!permitted.Any(t => HasType(data, t)))
            errors.Add(new ValidationError(
                path, $"expected {string.Join(" or ", permitted)}, but found {TypeName(data)}"));
    }

    private static void CheckEnum(DataMap rules, DataValue data, DataPath path, List<ValidationError> errors)
    {
        if (rules.ContainsKey("enum"))
        {
            var permitted = rules["enum"].As<DataList>();
            if (!permitted.Any(v => v.Equals(data)))
                errors.Add(new ValidationError(path, $"must be one of {permitted}, but was {Show(data)}"));
        }

        if (rules.ContainsKey("const") && !rules["const"].Equals(data))
            errors.Add(new ValidationError(path, $"must be {Show(rules["const"])}, but was {Show(data)}"));
    }

    private static void CheckObject(DataMap rules, DataValue data, DataPath path, List<ValidationError> errors)
    {
        if (data is not DataMap map) return;

        var properties = rules.ContainsKey("properties") ? rules["properties"].As<DataMap>() : DataMap.Empty;

        if (rules.ContainsKey("required"))
            foreach (var name in rules["required"].As<DataList>().Select(n => n.As<string>()))
                if (!map.ContainsKey(name))
                    errors.Add(new ValidationError(path.Then(name), "is required but missing"));

        foreach (var (name, propertySchema) in properties)
            if (map.ContainsKey(name))
                Check(propertySchema, map[name], path.Then(name), errors);

        if (rules.ContainsKey("additionalProperties") && rules["additionalProperties"] is false)
            foreach (var name in map.Keys)
                if (!properties.ContainsKey(name))
                    errors.Add(new ValidationError(path.Then(name), "is not a permitted property"));
    }

    private static void CheckArray(DataMap rules, DataValue data, DataPath path, List<ValidationError> errors)
    {
        if (data is not DataList list) return;

        if (rules.ContainsKey("items"))
            for (var i = 0; i < list.Count; i++)
                Check(rules["items"], list[i], path.Then(i), errors);

        if (rules.ContainsKey("minItems"))
        {
            var least = rules["minItems"].As<long>();
            if (list.Count < least)
                errors.Add(new ValidationError(path, $"must have at least {least} items, but had {list.Count}"));
        }

        if (rules.ContainsKey("maxItems"))
        {
            var most = rules["maxItems"].As<long>();
            if (list.Count > most)
                errors.Add(new ValidationError(path, $"must have at most {most} items, but had {list.Count}"));
        }

        if (rules.ContainsKey("uniqueItems") && rules["uniqueItems"] is true
            && list.Distinct().Count() != list.Count)
            errors.Add(new ValidationError(path, "must not contain duplicates"));
    }

    private static void CheckNumber(DataMap rules, DataValue data, DataPath path, List<ValidationError> errors)
    {
        if (!TryAsNumber(data, out var number)) return;

        if (rules.ContainsKey("minimum") && TryAsNumber(rules["minimum"], out var min) && number < min)
            errors.Add(new ValidationError(path, $"must be at least {min}, but was {number}"));

        if (rules.ContainsKey("maximum") && TryAsNumber(rules["maximum"], out var max) && number > max)
            errors.Add(new ValidationError(path, $"must be at most {max}, but was {number}"));
    }

    private static void CheckString(DataMap rules, DataValue data, DataPath path, List<ValidationError> errors)
    {
        if (data is not string text) return;

        if (rules.ContainsKey("minLength"))
        {
            var least = rules["minLength"].As<long>();
            if (text.Length < least)
                errors.Add(new ValidationError(
                    path, $"must be at least {least} characters, but was {text.Length}"));
        }

        if (rules.ContainsKey("maxLength"))
        {
            var most = rules["maxLength"].As<long>();
            if (text.Length > most)
                errors.Add(new ValidationError(
                    path, $"must be at most {most} characters, but was {text.Length}"));
        }

        if (rules.ContainsKey("pattern"))
        {
            var pattern = rules["pattern"].As<string>();
            if (!Regex.IsMatch(text, pattern))
                errors.Add(new ValidationError(path, $"must match {pattern}, but was {Show(data)}"));
        }
    }

    private static void CheckCombinators(DataMap rules, DataValue data, DataPath path, List<ValidationError> errors)
    {
        if (rules.ContainsKey("allOf"))
            foreach (var branch in rules["allOf"].As<DataList>())
                Check(branch, data, path, errors);

        if (rules.ContainsKey("anyOf"))
        {
            var branches = rules["anyOf"].As<DataList>();
            // Report only that nothing matched: each branch's own errors are noise,
            // since failing branches are expected.
            if (!branches.Any(branch => Validate(branch, data).IsValid()))
                errors.Add(new ValidationError(path, $"did not match any permitted schema, was {Show(data)}"));
        }
    }

    private static bool HasType(DataValue data, string type) =>
        type switch
        {
            "null" => data is DataNull,
            "boolean" => data is bool,
            "integer" => data is long,
            "number" => data is long or double,
            "string" => data is string,
            "array" => data is DataList,
            "object" => data is DataMap,
            _ => throw new ArgumentException($"Unknown schema type {type}")
        };

    private static string TypeName(DataValue data) =>
        data switch
        {
            DataNull => "null",
            bool => "boolean",
            long => "integer",
            double => "number",
            string => "string",
            DataList => "array",
            DataMap => "object"
        };

    private static bool TryAsNumber(DataValue data, out double number)
    {
        switch (data)
        {
            case long n:
                number = n;
                return true;
            case double d:
                number = d;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static string Show(DataValue data) => DataJson.Serialize(data);
}
