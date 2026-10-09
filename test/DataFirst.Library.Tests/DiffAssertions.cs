using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

internal static class DiffAssertions
{
    public static void ChangedPathsAre(this DataMap diff, params string[] expected) =>
        _.ChangedPaths(diff).Select(p => p.ToString()).Should().BeEquivalentTo(expected);
}
