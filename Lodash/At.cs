namespace DataFirst.Lodash;

public static partial class _
{
    /// Reads several locations at once, returning their values in the order asked for.
    ///
    ///     _.At(book, "title", "isbn")   ->  ["Watchmen", "978-1779501127"]
    ///
    /// A key that is not there yields null rather than throwing, so At can project
    /// optional fields without the caller checking each one first. Duplicates are
    /// kept, and the result is always the same length as the key list -- which is
    /// what makes it safe to zip back against those keys.
    public static DataList At(DataValue obj, params IEnumerable<StringOrInt> keys) =>
        DataList.Create(keys.Select(key => GetOrNull(obj, key)));

    /// The same, for locations that are paths rather than single keys.
    ///
    ///     _.At(library, [DataPath.Of("catalog", "booksByIsbn"),
    ///                    DataPath.Of("userManagementData", "members")])
    ///
    /// Takes a collection rather than params, because two variadic overloads would
    /// be ambiguous for the empty call.
    public static DataList At(DataValue obj, IReadOnlyList<DataPath> paths) =>
        DataList.Create(paths.Select(path => GetOrNull(obj, path)));
}
