using DataFirst.Lodash;
using DataFirst.Testing;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class OmitTests
{
    /// A map and a path inside it whose last step is a map field -- the only kind
    /// Omit accepts.
    private static readonly Gen<(DataMap Map, DataPath Path)> OmittablePath =
        Gens.MapWithExistingPath.Where(t =>
            t.Path.Count > 0
            && (t.Path.Count == 1
                ? t.Path[0] is string
                : _.Get(t.Map, t.Path.Take(t.Path.Count - 1).ToList()) is DataMap && t.Path[^1] is string));

    private static Property Over(Func<(DataMap Map, DataPath Path), bool> check) =>
        Prop.ForAll(OmittablePath.ToArbitrary(), check);

    [Property]
    public Property The_path_is_gone_afterwards() =>
        Over(t => !_.ContainsKey(_.Omit(t.Map, [t.Path]), t.Path));

    [Property]
    public Property Everything_else_is_untouched_and_the_field_can_be_put_back() =>
        Over(t => _.Set(_.Omit(t.Map, [t.Path]), t.Path, _.Get(t.Map, t.Path)).Equals(t.Map));

    [Property]
    public Property Omitting_is_idempotent() =>
        Over(t =>
        {
            var once = _.Omit(t.Map, [t.Path]);
            return _.Omit(once, [t.Path]).Equals(once);
        });

    [Property]
    public Property Omitting_leaves_the_original_alone() =>
        Over(t =>
        {
            var before = t.Map.ToString();
            _.Omit(t.Map, [t.Path]);
            return t.Map.ToString() == before;
        });

    [Property]
    public Property A_top_level_omit_removes_exactly_one_entry_and_keeps_the_others_in_order() =>
        Prop.ForAll(Gens.Map.Where(m => !m.IsEmpty).ToArbitrary(), map =>
        {
            var key = map.Keys.Last();
            var omitted = _.Omit(map, [DataPath.Of(key)]);
            return omitted.Count == map.Count - 1 && omitted.Keys.SequenceEqual(map.Keys.Where(k => k != key));
        });

    [Property]
    public bool No_paths_changes_nothing(DataMap map) => ReferenceEquals(_.Omit(map, []), map);

    [Property]
    public bool A_path_that_is_not_there_is_skipped(DataMap map) =>
        ReferenceEquals(_.Omit(map, [DataPath.Of("zzz"), DataPath.Of("zzz", "deeper"), DataPath.Of("a", "zzz", "q")]), map);

    [Property]
    public Property Two_paths_that_do_not_overlap_can_be_omitted_in_either_order() =>
        Prop.ForAll(
            (from m in Gens.Map
             let candidates = _.InformationPaths(m).Where(p => p.Count == 1 && p[0] is string).ToArray()
             where candidates.Length >= 2
             from p1 in Gen.Elements(candidates)
             from p2 in Gen.Elements(candidates).Where(p => !p.Equals(p1))
             select (m, p1, p2)).ToArbitrary(),
            t =>
            {
                var omitted = _.Omit(t.m, [t.p1, t.p2]);
                return omitted.Equals(_.Omit(t.m, [t.p2, t.p1]))
                       && !_.ContainsKey(omitted, t.p1)
                       && !_.ContainsKey(omitted, t.p2);
            });

    [Property]
    public Property Omitting_a_parent_covers_omitting_its_children_in_any_order() =>
        Prop.ForAll(
            Gens.MapWithExistingPath.Where(t => t.Path.Count >= 2 && t.Path.All(s => s is string)
                && _.Get(t.Map, t.Path.Take(t.Path.Count - 1).ToList()) is DataMap).ToArbitrary(),
            t =>
            {
                var parent = DataPath.Of(t.Path.Take(1));
                return _.Omit(t.Map, [t.Path, parent]).Equals(_.Omit(t.Map, [parent]))
                       && _.Omit(t.Map, [parent, t.Path]).Equals(_.Omit(t.Map, [parent]));
            });

    [Fact]
    public void Nested_fields_are_removed_in_place_and_empty_parents_stay()
    {
        var member = Map.Of(("email", "a@b.co"), ("password", Map.Of(("salt", "s"), ("hash", "h"))), ("address", Map.Of(("line1", "x"))));

        _.Omit(member, [DataPath.Of("password", "hash"), DataPath.Of("address", "line1")])
            .ShouldEqual(Map.Of(("email", "a@b.co"), ("password", Map.Of(("salt", "s"))), ("address", DataMap.Empty)));
    }

    [Fact]
    public void A_whole_subtree_can_be_omitted()
    {
        _.Omit(Map.Of(("a", 1), ("b", Map.Of(("c", 2)))), [DataPath.Of("b")]).ShouldEqual(Map.Of(("a", 1)));
    }

    [Fact]
    public void Paths_into_a_scalar_or_off_the_end_of_a_list_are_skipped()
    {
        var map = Map.Of(("leaf", 5), ("items", List.Of(1, 2)));
        _.Omit(map, [DataPath.Of("leaf", "deeper"), DataPath.Of("items", 9)]).ShouldEqual(map);
    }

    [Fact]
    public void Removing_from_a_list_is_an_error_rather_than_a_silent_renumbering()
    {
        var map = Map.Of(("items", List.Of(1, 2, 3)));
        new Action(() => _.Omit(map, [DataPath.Of("items", 1)]))
            .Should().Throw<InvalidOperationException>().WithMessage("*list*");
        new Action(() => _.Omit(Map.Of(("a", List.Of(Map.Of(("b", 1))))), [DataPath.Of("a", 0)]))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_field_inside_a_list_element_can_be_omitted_because_its_parent_is_a_map()
    {
        _.Omit(Map.Of(("items", List.Of(Map.Of(("id", 1), ("secret", 2))))), [DataPath.Of("items", 0, "secret")])
            .ShouldEqual(Map.Of(("items", List.Of(Map.Of(("id", 1))))));
    }

    [Fact]
    public void The_root_cannot_be_omitted()
    {
        new Action(() => _.Omit(Map.Of(("a", 1)), [DataPath.Root])).Should().Throw<ArgumentException>().WithMessage("*root*");
    }
}
