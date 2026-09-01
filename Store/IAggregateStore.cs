namespace DataFirst;

/// A value read from the store, with the version it was read at.
///
/// The version is the client's half of the optimistic concurrency handshake: it goes
/// out with the read and comes back with the write, and the store uses it to work out
/// what happened in between.
public readonly record struct Versioned(DataValue Value, long Version);

/// Raised when the store can no longer tell what changed since the client read.
///
/// Not a conflict -- it is an admission of ignorance. A store that retains a bounded
/// history has to say this rather than guess, and the only recovery is for the client
/// to read again and redo its work.
public sealed class StaleVersionException(DataPath aggregate, long clientVersion, long oldestKnown)
    : Exception($"Version {clientVersion} of '{aggregate}' is older than the retained history " +
                $"(oldest known is {oldestKnown}); re-read and retry")
{
    public DataPath Aggregate { get; } = aggregate;
    public long ClientVersion { get; } = clientVersion;
    public long OldestKnown { get; } = oldestKnown;
}

/// Where aggregates live.
///
/// The contract is deliberately the narrow one a remote store could honour: read a
/// value with its version, send back a diff and the version it was computed against.
/// Nothing here lets a caller hand over a whole system value or hold a reference into
/// the store, because across a network it could do neither.
public interface IAggregateStore
{
    /// The current value of an aggregate and the version it is at. An aggregate that
    /// does not exist reads as null at version 0, so a caller can create one without
    /// a separate code path.
    Versioned Read(DataPath aggregate);

    /// Applies a diff that was computed against `expectedVersion`.
    ///
    /// Throws ConcurrentModificationException when the diff touches a path that moved
    /// in the meantime, and StaleVersionException when the store cannot tell.
    Versioned Commit(DataPath aggregate, long expectedVersion, DataMap diff);
}
