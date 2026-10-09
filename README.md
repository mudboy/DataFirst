# DataFirst

A data-first library system in C#: state is plain, immutable data (maps, lists and values) and behaviour is pure functions over it. Lodash-style helpers (`Get`, `Set`, `Merge`, `Diff`, ...) operate on that data generically.

It is based on the book [Data-Oriented Programming: Reduce software complexity](https://livebook.manning.com/book/data-oriented-programming) by Yehonathan Sharvit (Manning). The library example, the generic data and Lodash-style functions, schema validation, and the version-reconciliation and aggregate ideas follow the book; the C# design is this repository's own.

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
  DataFirst.Tests/              tests for the core and database (one file per test class)
  DataFirst.Library.Tests/      tests for the library domain
  DataFirst.Testing/            shared test support: generators and assertions
```

## Namespaces

- `DataFirst`: the data types, stores and system state.
- `DataFirst.Database`: SQLite access.
- `DataFirst.Library`: the library domain.
- `DataFirst.Lodash`: the generic data functions.
- `DataFirst.Tests`, `DataFirst.Library.Tests`: test code.
- `DataFirst.Testing`: shared generators and assertions for the tests.

## Lodash functions

The generic functions live in `DataFirst.Lodash` as static methods on `_`. Every function in the book's [appendix D](https://livebook.manning.com/book/data-oriented-programming/appendix-d) has an equivalent, grouped here the way the appendix groups them. All of them return new values and leave their arguments alone.

**Maps**

| Lodash | Here | Notes |
|---|---|---|
| `at` | `At` | keys, or paths |
| `get` | `Get`, `GetOrNull` | `GetOrNull` yields null rather than throwing |
| `has` | `ContainsKey` | one key, or a path |
| `merge` | `MergeDeep` | recursive; maps by key, lists by index, the second wins |
| `omit` | `Omit` | by path; absent paths are skipped, and list elements cannot be removed |
| `set` | `Set` | writes through a path, creating missing containers |
| `values` | `Values` | |

**Lists**

| Lodash | Here | Notes |
|---|---|---|
| `concat` | `Concat` | |
| `flatten` | `Flatten` | one level |
| `intersection` | `Intersection` | |
| `nth` | `Nth` | negative counts from the end; out of range is null |
| `sum` | `Sum` | a long for all-long input, otherwise a double |
| `union` | `Union` | |
| `uniq` | `Uniq` | |

**Collections (a list's elements, or a map's values)**

| Lodash | Here | Notes |
|---|---|---|
| `every` | `Every` | |
| `filter` | `Filter` | |
| `find` | `Find` | null when nothing matches |
| `forEach` | `ForEach` | returns the collection |
| `groupBy` | `GroupBy` | by function or field |
| `isArray` | `IsArray` | |
| `isEmpty` | `IsEmpty` | |
| `isEqual` | `IsEqual` | deep; tells a long from a double |
| `isObject` | `IsObject` | true for a map or a list |
| `keyBy` | `KeyBy` | by function or field |
| `map` | `Map` | always produces a list |
| `reduce` | `Reduce` | also passes each key or index |
| `size` | `Size` | |
| `sortBy` | `SortBy` | stable; by function or field |

Beyond the appendix, the core adds what the version-reconciliation and aggregate code needs: `Diff` and `DiffObjects`, `Merge` (applies a diff, as opposed to `MergeDeep`), `InformationPaths` and `ChangedPaths`, `Update`, `SetAt` and `InsertAt`, `Unwind`, `AggregateFields`, `Keys`, and `Getter` for naming a path once.

## Build and test

```
dotnet build
dotnet test
```

Requires the .NET SDK specified in `global.json`; the projects target `net11.0`.
