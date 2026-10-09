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
public sealed class DataPathProperties
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
