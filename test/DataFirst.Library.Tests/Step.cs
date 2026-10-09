using DataFirst.Testing;
using System.Collections.Immutable;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

/// One step of a random history: which earlier read to work from, and which key to set.
/// Every write stores a fresh number, so no key ever returns to an earlier value --
/// see the A-B-A test below for why that matters.
public sealed record Step(int Lag, string Key);
