using DataFirst.Testing;
using System.Text.Json;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class DataPathTests
{
    [Property]
    public bool Equality_and_hash_follow_the_steps(DataPath path) =>
        path.Equals(DataPath.Of(path.ToList())) && path.GetHashCode() == DataPath.Of(path.ToList()).GetHashCode();

    [Property]
    public bool Then_a_step_appends_it(DataPath path, StringOrInt step)
    {
        var longer = path.Then(step);
        return longer.Count == path.Count + 1 && longer.Take(path.Count).SequenceEqual(path) && longer[path.Count].Equals(step);
    }

    [Property]
    public bool Then_a_path_concatenates(DataPath first, DataPath second) =>
        first.Then(second).SequenceEqual(first.Concat(second));

    [Property]
    public bool Root_is_the_identity_for_Then(DataPath path) =>
        path.Then(DataPath.Root).Equals(path) && DataPath.Root.Then(path).Equals(path);

    [Property]
    public bool Overlaps_is_reflexive(DataPath path) => path.Overlaps(path);

    [Property]
    public bool Overlaps_is_symmetric(DataPath a, DataPath b) => a.Overlaps(b) == b.Overlaps(a);

    [Property]
    public bool A_path_overlaps_everything_beneath_it(DataPath path, DataPath suffix) =>
        path.Overlaps(path.Then(suffix)) && path.Then(suffix).Overlaps(path);

    [Property]
    public bool Root_overlaps_everything(DataPath path) => DataPath.Root.Overlaps(path);

    [Property]
    public bool Paths_that_diverge_do_not_overlap(DataPath prefix, DataPath tail)
    {
        var left = prefix.Then("left").Then(tail);
        var right = prefix.Then("right").Then(tail);
        return !left.Overlaps(right);
    }

    [Property]
    public bool Overlap_matches_the_prefix_definition(DataPath a, DataPath b)
    {
        var shared = Math.Min(a.Count, b.Count);
        return a.Overlaps(b) == a.Take(shared).SequenceEqual(b.Take(shared));
    }

    [Fact]
    public void Describes_itself_with_dots_and_bracketed_indices()
    {
        DataPath.Root.ToString().Should().Be("(root)");
        DataPath.Of("catalog", "books", 0, "title").ToString().Should().Be("catalog.books.[0].title");
    }

    [Fact]
    public void A_string_step_and_the_same_number_as_an_int_step_are_different()
    {
        DataPath.Of("0").Equals(DataPath.Of(0)).Should().BeFalse();
        DataPath.Of("0").Overlaps(DataPath.Of(0)).Should().BeFalse();
    }

    [Fact]
    public void A_collection_expression_builds_the_same_path_as_Of()
    {
        DataPath literal = ["x", 1, "B"];

        literal.Equals(DataPath.Of("x", 1, "B")).Should().BeTrue();
        literal.GetHashCode().Should().Be(DataPath.Of("x", 1, "B").GetHashCode());
        literal.ToString().Should().Be("x.[1].B");
    }

    [Fact]
    public void An_empty_collection_expression_is_the_root_and_a_lone_step_is_a_one_step_path()
    {
        DataPath empty = [];
        DataPath single = ["a"];

        empty.Equals(DataPath.Root).Should().BeTrue();
        single.Equals(DataPath.Of("a")).Should().BeTrue();
    }

    [Fact]
    public void A_literal_can_be_passed_wherever_a_path_is_expected()
    {
        var map = Map.Of(("a", 1), ("b", Map.Of(("c", 2), ("d", 3))));

        _.Omit(map, [["a"], ["b", "c"]]).ShouldEqual(Map.Of(("b", Map.Of(("d", 3)))));
        SameAs(["x", 1, "B"], DataPath.Of("x", 1, "B")).Should().BeTrue();
    }

    [Property]
    public bool Spreading_a_path_into_a_literal_rebuilds_it(DataPath path)
    {
        DataPath rebuilt = [.. path];
        return rebuilt.Equals(path) && rebuilt.GetHashCode() == path.GetHashCode();
    }

    [Property]
    public bool A_literal_can_extend_a_path_with_a_spread(DataPath path, StringOrInt step)
    {
        DataPath extended = [.. path, step];
        return extended.Equals(path.Then(step));
    }

    [Property]
    public bool Building_from_a_literal_copies_the_steps(DataPath path)
    {
        var steps = path.ToArray();
        DataPath built = [.. steps];
        if (steps.Length > 0) steps[0] = "changed";
        return built.Equals(path);
    }

    private static bool SameAs(DataPath actual, DataPath expected) => actual.Equals(expected);

    [Fact]
    public void Paths_work_as_set_members()
    {
        var set = new HashSet<DataPath> { DataPath.Of("a", 1), DataPath.Of("a", 1), DataPath.Of("a", 2) };
        set.Should().HaveCount(2);
    }

    [Fact]
    public void Equals_handles_null_and_foreign_types()
    {
        DataPath.Of("a").Equals(null).Should().BeFalse();
        DataPath.Of("a").Equals((object)"a").Should().BeFalse();
    }
}
