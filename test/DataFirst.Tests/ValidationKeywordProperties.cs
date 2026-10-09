using DataFirst.Testing;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class ValidationKeywordProperties
{
    private static bool Valid(DataValue schema, DataValue data) => Validation.Validate(schema, data).IsValid();

    private static IReadOnlyList<ValidationError> ErrorsOf(DataValue schema, DataValue data) =>
        Validation.Validate(schema, data).Errors();

    // type

    [Property]
    public bool The_empty_schema_accepts_everything(DataValue data) => Valid(DataMap.Empty, data);

    [Property]
    public bool Type_accepts_exactly_the_values_of_that_type(DataValue data)
    {
        var types = new (string Name, Func<DataValue, bool> Matches)[]
        {
            ("null", v => v is DataNull),
            ("boolean", v => v is bool),
            ("integer", v => v is long),
            ("number", v => v is long or double),
            ("string", v => v is string),
            ("array", v => v is DataList),
            ("object", v => v is DataMap)
        };

        return types.All(t => Valid(Map.Of(("type", t.Name)), data) == t.Matches(data));
    }

    [Property]
    public bool A_list_of_types_accepts_any_of_them(DataValue data) =>
        Valid(Map.Of(("type", List.Of("string", "null"))), data) == (data is string or DataNull);

    [Property]
    public bool A_type_mismatch_is_reported_once_at_the_value(DataValue data) =>
        data is string
        || ErrorsOf(Map.Of(("type", "string")), data) is [{ Path.Count: 0, Message: var m }] && m.Contains("expected string");

    // numbers

    [Property]
    public bool Minimum_and_maximum_are_inclusive_bounds(int lo, int hi, int n) =>
        Valid(Map.Of(("minimum", lo), ("maximum", hi)), (long)n) == (n >= lo && n <= hi);

    [Property]
    public bool Bounds_apply_to_doubles_too(int lo, int n) =>
        Valid(Map.Of(("minimum", lo)), n + 0.5) == (n + 0.5 >= lo);

    [Property]
    public bool Number_keywords_ignore_non_numbers(string text) =>
        Valid(Map.Of(("minimum", 5), ("maximum", 1)), text ?? "");

    // strings

    [Property]
    public bool Length_bounds_are_inclusive(NonNegativeInt lo, NonNegativeInt hi, NonNegativeInt length)
    {
        var text = new string('x', length.Get % 40);
        var (least, most) = (lo.Get % 40, hi.Get % 40);
        return Valid(Map.Of(("minLength", least), ("maxLength", most)), text)
               == (text.Length >= least && text.Length <= most);
    }

    [Property]
    public bool String_keywords_ignore_non_strings(long n) =>
        Valid(Map.Of(("minLength", 5), ("maxLength", 1), ("pattern", "^$")), n);

    [Property]
    public bool Pattern_matches_like_a_regex(NonNegativeInt digits, bool trailingLetter)
    {
        var text = new string('7', digits.Get % 8) + (trailingLetter ? "z" : "");
        return Valid(Map.Of(("pattern", "^[0-9]+$")), text)
               == (digits.Get % 8 > 0 && !trailingLetter);
    }

    // arrays

    [Property]
    public bool Item_count_bounds_are_inclusive(NonNegativeInt lo, NonNegativeInt hi, NonNegativeInt count)
    {
        var list = DataList.Create(Enumerable.Repeat((DataValue)1, count.Get % 10).ToList());
        var (least, most) = (lo.Get % 10, hi.Get % 10);
        return Valid(Map.Of(("minItems", least), ("maxItems", most)), list)
               == (list.Count >= least && list.Count <= most);
    }

    [Property]
    public bool Unique_items_accepts_exactly_the_lists_without_duplicates(DataList list) =>
        Valid(Map.Of(("uniqueItems", true)), list) == (list.Distinct().Count() == list.Count);

    [Property]
    public bool Unique_items_set_to_false_allows_duplicates(DataList list) =>
        Valid(Map.Of(("uniqueItems", false)), list);

    [Property]
    public bool Items_are_each_checked_and_errors_carry_the_index(NonNegativeInt count, NonNegativeInt badAt)
    {
        var size = count.Get % 8 + 1;
        var bad = badAt.Get % size;
        var list = DataList.Create(Enumerable.Range(0, size).Select(i => i == bad ? (DataValue)"x" : 1L).ToList());

        return ErrorsOf(Map.Of(("items", Map.Of(("type", "integer")))), list)
            is [{ Path: var p }] && p.Equals(DataPath.Of(bad));
    }

    [Property]
    public bool Array_keywords_ignore_non_arrays(long n) =>
        Valid(Map.Of(("minItems", 3), ("items", Map.Of(("type", "string"))), ("uniqueItems", true)), n);

    // enum, const

    [Property]
    public bool Enum_accepts_exactly_its_members(DataList permitted, DataValue data) =>
        Valid(Map.Of(("enum", permitted)), data) == permitted.Contains(data);

    [Property]
    public bool Const_accepts_exactly_that_value(DataValue expected, DataValue data) =>
        Valid(Map.Of(("const", expected)), data) == expected.Equals(data);

    // objects

    [Property]
    public bool Each_missing_required_property_is_its_own_error(bool a, bool b, bool c)
    {
        var present = new[] { ("a", a), ("b", b), ("c", c) };
        var data = present.Where(p => p.Item2).Aggregate(DataMap.Empty, (m, p) => m.SetItem(p.Item1, 1));
        var schema = Map.Of(("required", List.Of("a", "b", "c")));

        var errors = ErrorsOf(schema, data);
        var missing = present.Where(p => !p.Item2).Select(p => p.Item1).ToList();

        return errors.Select(e => e.Path.ToString()).OrderBy(x => x).SequenceEqual(missing)
               && errors.All(e => e.Message.Contains("required"));
    }

    [Property]
    public bool Properties_are_checked_only_when_present(DataMap data)
    {
        var schema = Map.Of(("properties", Map.Of(("zzz", Map.Of(("type", "string"))))));
        return Valid(schema, data);
    }

    [Property]
    public bool Additional_properties_false_flags_every_unnamed_key(DataMap data)
    {
        var schema = Map.Of(("additionalProperties", false), ("properties", Map.Of(("a", DataMap.Empty))));
        var errors = ErrorsOf(schema, data);
        var extra = data.Keys.Where(k => k != "a").Order().ToList();

        return errors.Select(e => e.Path.ToString()).Order().SequenceEqual(extra);
    }

    [Property]
    public bool Additional_properties_true_or_absent_allows_extras(DataMap data) =>
        Valid(Map.Of(("additionalProperties", true)), data) && Valid(DataMap.Empty, data);

    [Property]
    public bool Object_keywords_ignore_non_objects(long n) =>
        Valid(Map.Of(("required", List.Of("a")), ("additionalProperties", false)), n);

    [Property]
    public bool Errors_nest_through_properties_and_arrays(NonNegativeInt index)
    {
        var i = index.Get % 3;
        var data = Map.Of(("books", DataList.Create(Enumerable.Range(0, 3)
            .Select(n => (DataValue)Map.Of(("title", n == i ? (DataValue)5 : "ok"))).ToList())));
        var schema = Map.Of(("properties", Map.Of(("books", Map.Of(("items", Map.Of(("properties", Map.Of(("title", Map.Of(("type", "string"))))))))))));

        return ErrorsOf(schema, data) is [{ Path: var p }] && p.Equals(DataPath.Of("books", i, "title"));
    }

    // combinators

    [Property]
    public bool AllOf_holds_exactly_when_every_branch_holds(DataValue data)
    {
        var branches = new DataValue[] { Map.Of(("type", "integer")), Map.Of(("minimum", 0)), Map.Of(("maximum", 10)) };
        return Valid(Map.Of(("allOf", DataList.Create(branches))), data) == branches.All(b => Valid(b, data));
    }

    [Property]
    public bool AnyOf_holds_exactly_when_some_branch_holds(DataValue data)
    {
        var branches = new DataValue[] { Map.Of(("type", "string")), Map.Of(("type", "null")), Map.Of(("const", 5)) };
        return Valid(Map.Of(("anyOf", DataList.Create(branches))), data) == branches.Any(b => Valid(b, data));
    }

    [Property]
    public bool A_failed_AnyOf_is_one_error_not_one_per_branch(long n)
    {
        var branches = List.Of(Map.Of(("type", "string")), Map.Of(("type", "null")), Map.Of(("type", "boolean")));
        return ErrorsOf(Map.Of(("anyOf", branches)), n).Count == 1;
    }

    [Property]
    public bool Every_failed_AllOf_branch_reports_its_own_error(long n)
    {
        var schema = Map.Of(("allOf", List.Of(Map.Of(("type", "string")), Map.Of(("type", "boolean")), Map.Of(("type", "integer")))));
        return ErrorsOf(schema, n).Count == 2;
    }

    // all errors, not just the first

    [Fact]
    public void Every_violation_is_reported_together()
    {
        var schema = Map.Of(
            ("type", "object"),
            ("required", List.Of("id")),
            ("additionalProperties", false),
            ("properties", Map.Of(
                ("name", Map.Of(("type", "string"), ("minLength", 3))),
                ("age", Map.Of(("type", "integer"), ("minimum", 0))))));

        var errors = Validation.Validate(schema, Map.Of(("name", "x"), ("age", -1), ("extra", true))).Errors();

        errors.Select(e => e.Path.ToString()).Should().BeEquivalentTo("id", "name", "age", "extra");
    }

    [Fact]
    public void One_value_can_break_several_keywords_at_once()
    {
        var errors = Validation.Validate(Map.Of(("minLength", 5), ("pattern", "^[0-9]+$")), "ab").Errors();
        errors.Should().HaveCount(2);
    }

    // schema misuse

    [Fact]
    public void A_schema_that_is_not_a_map_is_an_error_at_that_position()
    {
        var errors = Validation.Validate("string", 5).Errors();
        errors.Should().ContainSingle().Which.Message.Should().Contain("schema must be a map");

        Validation.Validate(Map.Of(("properties", Map.Of(("a", 5)))), Map.Of(("a", 1))).Errors()
            .Should().ContainSingle().Which.Path.ToString().Should().Be("a");
    }

    [Fact]
    public void An_unknown_type_name_is_a_schema_bug_and_throws()
    {
        new Action(() => Validation.Validate(Map.Of(("type", "float")), 1))
            .Should().Throw<ArgumentException>().WithMessage("*float*");
        new Action(() => Validation.Validate(Map.Of(("type", 5)), 1))
            .Should().Throw<ArgumentException>().WithMessage("*string or list*");
    }

    [Fact]
    public void A_non_numeric_bound_is_ignored()
    {
        Validation.Validate(Map.Of(("minimum", "five")), 1).IsValid().Should().BeTrue();
    }

    // ValidateOrThrow

    [Property]
    public bool ValidateOrThrow_returns_valid_data_untouched(DataValue data) =>
        Validation.ValidateOrThrow(DataMap.Empty, data).Equals(data);

    [Fact]
    public void ValidateOrThrow_throws_with_every_error_and_a_readable_message()
    {
        var schema = Map.Of(("required", List.Of("a", "b")));

        var act = () => Validation.ValidateOrThrow(schema, DataMap.Empty);

        var thrown = act.Should().Throw<SchemaViolationException>().Which;
        thrown.Errors.Should().HaveCount(2);
        thrown.Message.Should().Contain("a: is required but missing").And.Contain("b: is required but missing");
    }

    [Fact]
    public void Results_describe_themselves()
    {
        Valid(Map.Of(("type", "string")), "x").Should().BeTrue();
        DataFirst.Valid.Instance.ToString().Should().Be("valid");
        new Invalid([new ValidationError(DataPath.Of("a"), "bad"), new ValidationError(DataPath.Root, "worse")])
            .ToString().Should().Be("a: bad; (root): worse");
        new ValidationError(DataPath.Of("a", 0), "bad").ToString().Should().Be("a.[0]: bad");
    }
}
