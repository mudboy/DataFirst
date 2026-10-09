namespace DataFirst.Lodash;

/// <summary>
/// The result of diffing two nodes: either they are the same, or <c>data2</c> replaced <c>data1</c>.
/// </summary>
/// <remarks>
/// Modelled as a union so the recursion in <c>DiffObjects</c> is exhaustive and a legitimate
/// data value can never be mistaken for the "unchanged" marker.
/// </remarks>
public union DiffResult(NoDiff, Changed);

/// <summary>
/// The two nodes were equivalent; there is nothing to apply.
/// </summary>
public sealed record NoDiff
{
    /// <summary>The single shared instance.</summary>
    public static readonly NoDiff Instance = new();
}

/// <summary>
/// The nodes differed.
/// </summary>
/// <param name="Value">
/// For composites, a nested structure holding only the differing leaves; for leaves, the new value.
/// </param>
public sealed record Changed(DataValue Value);
