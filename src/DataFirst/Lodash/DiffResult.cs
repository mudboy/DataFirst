namespace DataFirst.Lodash;

/// The result of diffing two nodes: either they are the same, or data2 replaced data1.
/// Modelled as a union so the recursion in DiffObjects is exhaustive and a legitimate
/// data value can never be mistaken for the "unchanged" marker.
public union DiffResult(NoDiff, Changed);

public sealed record NoDiff
{
    public static readonly NoDiff Instance = new();
}

public sealed record Changed(DataValue Value);
