# DataFirst

A data-first library system in C#: state is plain, immutable data (maps, lists and values) and behaviour is pure functions over it. Lodash-style helpers (`Get`, `Set`, `Merge`, `Diff`, ...) operate on that data generically.

## Layout

```
DataFirst.slnx            solution
global.json               SDK pin
src/
  DataFirst/              the generic core
    Data/                 DataValue, DataMap, DataList, DataPath: the immutable data representation
    Lodash/               generic functions over data: At, Get, Set, Update, Merge, Diff, GroupBy, KeyBy, Unwind, ...
    Validation/           the schema validator and its results
    Store/                aggregate stores (snapshot and diff-indexed) behind IAggregateStore
    SystemState.cs, SystemConsistency.cs
                          the versioned state and how concurrent changes are reconciled
  DataFirst.Database/     SQLite access: Db.cs reads results into data, With.cs is a throwaway test database
  DataFirst.Library/      the library domain, built on the core
    LibraryOperations.cs, LibrarySystem.cs, Catalog.cs, UserManagement.cs, Aggregates.cs, Passwords.cs, Schemas.cs
                          users, books, lending and the aggregate boundaries
test/
  DataFirst.Tests/              core and database; FsCheck property tests alongside example tests
    DataTypesTests.cs           DataMap, DataList, DataPath, DataValue and JSON round trips
    LodashTests.cs              Get/Set, list operations, grouping, literals, diff, information paths
    ReadingDataTests.cs         getters, At, paths through maps and lists
    WritingDataTests.cs         Set, Update, InsertAt, immutability, insertion order
    CollectionOperationTests.cs aggregate, KeyBy, Unwind, reduce
    DiffAndMergeTests.cs        diffing a value and merging a diff back
    ConcurrentChangeTests.cs    reconciling concurrent commits and writer conflicts
    StoreTests.cs               the aggregate store contract, retention, SystemState
    ValidationTests.cs          schema validator keywords and error reporting
    SchemaKeywordTests.cs       keywords that do not apply to a value
    DataValueUnionTests.cs      exhaustive switch over DataValue
    InfrastructureTests.cs      database reads and debug dumps
  DataFirst.Library.Tests/      the library domain
    DomainTests.cs              user management, catalogue, authorisation, LibrarySystem, aggregate paths
    CatalogSearchTests.cs       author names, search, request validation
    UserAccountTests.cs         roles, authentication, adding members
    BookLendingTests.cs         reading and describing lendings, authorisation
    BookItemTests.cs            adding book items
    AggregateScopeTests.cs      what contends and what does not across aggregates
    LibraryStoreTests.cs        the library running over both stores
    StoreRetentionTests.cs      clients older than the retained history
    LibrarySystemIntegrationTests.cs  the system layer and parallel writes
    LibrarySchemaTests.cs       the schemas and the seed data
    PasswordTests.cs            password hashing and verification
  DataFirst.Testing/            shared test support: generators and assertions
```

## Namespaces

- `DataFirst`: the data types, stores and system state.
- `DataFirst.Database`: SQLite access.
- `DataFirst.Library`: the library domain.
- `DataFirst.Lodash`: the generic data functions.
- `DataFirst.Tests`, `DataFirst.Library.Tests`: test code.
- `DataFirst.Testing`: shared generators and assertions for the tests.

## Build and test

```
dotnet build
dotnet test
```

Requires the .NET SDK specified in `global.json`; the projects target `net11.0`.
