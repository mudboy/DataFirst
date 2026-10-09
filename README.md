# DataFirst

A data-first library system in C#: state is plain, immutable data (maps, lists and values) and behaviour is pure functions over it. Lodash-style helpers (`Get`, `Set`, `Merge`, `Diff`, ...) operate on that data generically.

## Layout

```
DataFirst.slnx            solution
global.json               SDK pin
src/
  DataFirst/              the library
    Data/                 DataValue, DataMap, DataList, DataPath: the immutable data representation
    Lodash/               generic functions over data: At, Get, Set, Update, Merge, Diff, GroupBy, KeyBy, Unwind, ...
    Validation/           schemas and validation results
    Store/                aggregate stores (snapshot and diff-indexed) behind IAggregateStore
    Db.cs                 reads database results into data
    Library*.cs, Catalog.cs, UserManagement.cs, Aggregates.cs, System*.cs
                          the library domain: users, books, lending and system-wide consistency
test/
  DataFirst.Tests/        xUnit tests; FsCheck property tests alongside example tests
```

## Namespaces

- `DataFirst`: the data types, domain and stores.
- `DataFirst.Lodash`: the generic data functions.
- `DataFirst.Tests`: all test code.

## Build and test

```
dotnet build
dotnet test
```

Requires the .NET SDK specified in `global.json`; the projects target `net11.0`.
