using DataFirst.Testing;
using System.Collections.Immutable;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

/// What one commit did, reduced to what both stores must agree on.
public abstract record Outcome
{
    public sealed record Committed(long Version, string Value) : Outcome;
    public sealed record Conflicted(string Paths) : Outcome;
    public sealed record Stale : Outcome;
}
