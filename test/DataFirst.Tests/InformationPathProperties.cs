using DataFirst.Testing;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class InformationPathProperties
{
    [Property]
    public bool Every_path_in_a_value_resolves_to_a_leaf_or_an_empty_composite(DataMap map) =>
        _.InformationPaths(map).All(p =>
        {
            var at = _.GetOrNull(map, p.ToList());
            return !at.IsComposite() || _.IsEmpty(at);
        });

    [Property]
    public bool A_leaf_has_just_the_root_path(DataValue value) =>
        value.IsComposite() && !_.IsEmpty(value)
        || _.InformationPaths(value).SequenceEqual([DataPath.Root]);

    [Property]
    public bool Paths_are_unique(DataMap map)
    {
        var paths = _.InformationPaths(map);
        return paths.Distinct().Count() == paths.Count;
    }

    [Property]
    public bool No_path_is_inside_another(DataMap map)
    {
        var paths = _.InformationPaths(map);
        return paths.All(p => paths.Count(q => q.Overlaps(p)) == 1) || map.IsEmpty;
    }

    [Property]
    public bool A_non_empty_map_has_a_path_per_leaf(DataMap map)
    {
        int Leaves(DataValue v) => v switch
        {
            DataMap m when !m.IsEmpty => m.Values.Sum(Leaves),
            DataList l when !l.IsEmpty => l.Sum(Leaves),
            _ => 1
        };
        return _.InformationPaths(map).Count == Leaves(map);
    }

    [Fact]
    public void An_empty_diff_touches_nothing_but_empty_data_still_has_a_root()
    {
        _.ChangedPaths(DataMap.Empty).Should().BeEmpty();
        _.InformationPaths(DataMap.Empty).Should().Equal(DataPath.Root);
    }

    [Fact]
    public void Paths_run_from_the_root_to_each_leaf_including_empty_composites()
    {
        var paths = _.InformationPaths(Map.Of(
            ("a", 1), ("b", Map.Of(("c", List.Of("x", DataMap.Empty)))), ("d", DataList.Empty)));

        paths.Should().Equal(
            DataPath.Of("a"),
            DataPath.Of("b", "c", 0),
            DataPath.Of("b", "c", 1),
            DataPath.Of("d"));
    }
}
