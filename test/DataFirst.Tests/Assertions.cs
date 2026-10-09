using AwesomeAssertions;

namespace DataFirst.Tests;

public static class DataAssertions
{
    /// Asserts through DataMap/DataList's own structural equality. Both implement
    /// IEnumerable, so a plain Should().Be() would route to AwesomeAssertions'
    /// collection assertions and walk members instead.
    public static void ShouldEqual(this object actual, object expected) =>
        actual.Equals(expected).Should().BeTrue($"of\n  expected: {expected}\n  actual:   {actual}");
}
