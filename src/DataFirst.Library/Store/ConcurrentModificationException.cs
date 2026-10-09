namespace DataFirst.Library;

/// Raised when two mutations, started from the same version, changed the same
/// location, so neither can be applied on top of the other without losing one.
public sealed class ConcurrentModificationException(IReadOnlyList<DataPath> conflictingPaths)
    : Exception($"Conflicting concurrent mutations at: {string.Join(", ", conflictingPaths)}")
{
    public IReadOnlyList<DataPath> ConflictingPaths { get; } = conflictingPaths;
}
