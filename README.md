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
  DataFirst.Tests/          xUnit tests for the core and database; FsCheck property tests alongside example tests
  DataFirst.Library.Tests/  tests for the library domain
  DataFirst.Testing/        shared test support: generators and assertions
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
