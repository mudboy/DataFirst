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
    Validation/           schemas and validation results
    Store/                aggregate stores (snapshot and diff-indexed) behind IAggregateStore
    Db.cs                 reads database results into data
    SystemState.cs, SystemConsistency.cs
                          the versioned state and how concurrent changes are reconciled
  DataFirst.Library/      the library domain, built on the core
    Library.cs, LibrarySystem.cs, Catalog.cs, UserManagement.cs, Aggregates.cs, Passwords.cs
                          users, books, lending and the aggregate boundaries
test/
  DataFirst.Tests/        xUnit tests; FsCheck property tests alongside example tests
```

## Namespaces

- `DataFirst`: the data types, stores and the library domain (both `DataFirst` and `DataFirst.Library` projects share it).
- `DataFirst.Lodash`: the generic data functions.
- `DataFirst.Tests`: all test code.

## Build and test

```
dotnet build
dotnet test
```

Requires the .NET SDK specified in `global.json`; the projects target `net11.0`.
