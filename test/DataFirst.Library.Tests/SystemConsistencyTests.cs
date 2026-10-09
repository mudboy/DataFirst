using DataFirst.Testing;
using System.Collections.Immutable;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class SystemConsistencyTests
{
    private static readonly DataMap Base = Map.Of(("a", 0), ("b", 0), ("c", 0), ("nested", Map.Of(("x", 0), ("y", 0))));

    private static readonly Gen<DataPath> BasePath =
        Gen.Elements(
            DataPath.Of("a"), DataPath.Of("b"), DataPath.Of("c"),
            DataPath.Of("nested", "x"), DataPath.Of("nested", "y"));

    [Property]
    public bool Nothing_in_between_means_the_mutation_stands(DataMap previous, DataMap next) =>
        SystemConsistency.Reconcile(previous, previous, next).Equals(next);

    [Property]
    public Property A_mutation_that_changed_nothing_returns_whatever_the_system_became() =>
        Prop.ForAll(BasePath.ToArbitrary(), path =>
        {
            var current = _.Set(Base, path, 7);
            return SystemConsistency.Reconcile(current, Base, Base).Equals(current);
        });

    [Property]
    public Property Changes_to_different_places_both_survive_in_either_order() =>
        Prop.ForAll((from p1 in BasePath from p2 in BasePath.Where(p => p != p1) select (p1, p2)).ToArbitrary(), t =>
        {
            var one = _.Set(Base, t.p1, 1);
            var two = _.Set(Base, t.p2, 2);
            var expected = _.Set(one, t.p2, 2);

            return SystemConsistency.Reconcile(one, Base, two).Equals(expected)
                   && SystemConsistency.Reconcile(two, Base, one).Equals(_.Set(two, t.p1, 1));
        });

    [Property]
    public Property Changes_to_the_same_place_conflict_and_name_it() =>
        Prop.ForAll(BasePath.ToArbitrary(), path =>
        {
            var current = _.Set(Base, path, 1);
            var next = _.Set(Base, path, 2);

            try
            {
                SystemConsistency.Reconcile(current, Base, next);
                return false;
            }
            catch (ConcurrentModificationException e)
            {
                return e.ConflictingPaths.Single().Equals(path);
            }
        });

    [Property]
    public Property Common_paths_emptiness_is_symmetric() =>
        Prop.ForAll(Gens.Map.Zip(Gens.Map, (a, b) => (a, b)).ToArbitrary(), t =>
            Conflicts.CommonPaths(t.a, t.b).Count == 0
            == (Conflicts.CommonPaths(t.b, t.a).Count == 0));

    [Property]
    public bool A_non_empty_diff_is_in_common_with_itself(DataMap diff) =>
        diff.IsEmpty
        || Conflicts.CommonPaths(diff, diff).Count == _.ChangedPaths(diff).Count;

    [Property]
    public bool An_empty_diff_has_nothing_in_common_with_anything(DataMap diff) =>
        Conflicts.CommonPaths(DataMap.Empty, diff).Count == 0
        && Conflicts.CommonPaths(diff, DataMap.Empty).Count == 0;

    [Fact]
    public void A_conflict_message_lists_the_paths()
    {
        var current = _.Set(Base, ["nested", "x"], 1);
        var next = _.Set(Base, ["nested", "x"], 2);

        var act = () => SystemConsistency.Reconcile(current, Base, next);

        act.Should().Throw<ConcurrentModificationException>().WithMessage("*nested.x*");
    }

    [Fact]
    public void Reconcile_of_values_that_are_not_maps_and_have_moved_fails_loudly()
    {
        var act = () => SystemConsistency.Reconcile((DataValue)"a", "b", "c");
        act.Should().Throw<InvalidOperationException>();
    }
}
