using DataFirst.Lodash;

namespace DataFirst;

/// The shapes the library expects, expressed as data.
///
/// These are values, not types: they can be composed, nested, stored alongside the
/// data they describe, and diffed against each other. Adding a field to a book means
/// editing a map here, not changing a class and everything that touches it.
public static class Schemas
{
    private static readonly DataMap Isbn = Map.Of(
        "type", "string",
        "pattern", "^[0-9-]{10,17}$");

    public static readonly DataMap BookItem = Map.Of(
        "type", "object",
        "required", List.Of("id", "libId", "isLent"),
        "additionalProperties", false,
        "properties", Map.Of(
            "id", Map.Of("type", "string", "minLength", 1),
            "libId", Map.Of("type", "string", "minLength", 1),
            "isLent", Map.Of("type", "boolean")));

    public static readonly DataMap Book = Map.Of(
        "type", "object",
        "required", List.Of("isbn", "title", "authorIds"),
        "properties", Map.Of(
            "isbn", Isbn,
            "title", Map.Of("type", "string", "minLength", 1),
            "publicationYear", Map.Of("type", "integer", "minimum", 1400, "maximum", 2100),
            "authorIds", Map.Of(
                "type", "array",
                "minItems", 1,
                "uniqueItems", true,
                "items", Map.Of("type", "string", "minLength", 1)),
            "bookItems", Map.Of("type", "array", "items", BookItem)));

    public static readonly DataMap Author = Map.Of(
        "type", "object",
        "required", List.Of("name", "bookIsbns"),
        "additionalProperties", false,
        "properties", Map.Of(
            "name", Map.Of("type", "string", "minLength", 1),
            "bookIsbns", Map.Of("type", "array", "minItems", 1, "items", Isbn)));

    private static readonly DataMap Password = Map.Of(
        "type", "object",
        "required", List.Of("salt", "hash", "iterations"),
        "additionalProperties", false,
        "properties", Map.Of(
            "salt", Map.Of("type", "string", "minLength", 1),
            "hash", Map.Of("type", "string", "minLength", 1),
            "iterations", Map.Of("type", "integer", "minimum", 1)));

    private static readonly DataMap Email = Map.Of(
        "type", "string",
        "pattern", @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    public static readonly DataMap BookLending = Map.Of(
        "type", "object",
        "required", List.Of("bookItemId", "bookIsbn", "lendingDate"),
        "additionalProperties", false,
        "properties", Map.Of(
            "bookItemId", Map.Of("type", "string", "minLength", 1),
            "bookIsbn", Isbn,
            "lendingDate", Map.Of("type", "string", "pattern", "^[0-9]{4}-[0-9]{2}-[0-9]{2}$")));

    public static readonly DataMap Librarian = Map.Of(
        "type", "object",
        "required", List.Of("email", "password"),
        "additionalProperties", false,
        "properties", Map.Of(
            "email", Email,
            "password", Password));

    public static readonly DataMap Member = Map.Of(
        "type", "object",
        "required", List.Of("email", "password"),
        "additionalProperties", false,
        "properties", Map.Of(
            "email", Email,
            "password", Password,
            "isVip", Map.Of("type", "boolean"),
            "isSuper", Map.Of("type", "boolean"),
            "isBlocked", Map.Of("type", "boolean"),
            "bookLendings", Map.Of("type", "array", "items", BookLending)));

    public static readonly DataMap UserManagementData = Map.Of(
        "type", "object",
        "properties", Map.Of(
            "librarians", Map.Of("type", "object"),
            "members", Map.Of("type", "object")));

    /// What a caller may add: the book it belongs to, and the item identity.
    /// isLent is not accepted -- a new item starts out not lent.
    public static readonly DataMap BookItemInfo = Map.Of(
        "type", "object",
        "required", List.Of("isbn", "id", "libId"),
        "additionalProperties", false,
        "properties", Map.Of(
            "isbn", Isbn,
            "id", Map.Of("type", "string", "minLength", 1),
            "libId", Map.Of("type", "string", "minLength", 1)));

    /// Every criterion is optional; they combine with AND.
    public static readonly DataMap SearchQuery = Map.Of(
        "type", "object",
        "additionalProperties", false,
        "properties", Map.Of(
            "title", Map.Of("type", "string", "minLength", 1, "maxLength", 200),
            "author", Map.Of("type", "string", "minLength", 1, "maxLength", 200),
            "publishedAfter", Map.Of("type", "integer", "minimum", 1400, "maximum", 2100),
            "publishedBefore", Map.Of("type", "integer", "minimum", 1400, "maximum", 2100)));

    public static readonly DataMap CatalogData = Map.Of(
        "type", "object",
        "required", List.Of("booksByIsbn", "authorsById"),
        "properties", Map.Of(
            "booksByIsbn", Map.Of("type", "object"),
            "authorsById", Map.Of("type", "object")));

    public static readonly DataMap LibraryData = Map.Of(
        "type", "object",
        "required", List.Of("catalog"),
        "properties", Map.Of(
            "catalog", CatalogData,
            "userManagementData", UserManagementData));

    /// The request a caller sends to search the catalogue -- validated at the
    /// boundary, before anything trusts its shape.
    public static readonly DataMap SearchRequest = Map.Of(
        "type", "object",
        "required", List.Of("title"),
        "additionalProperties", false,
        "properties", Map.Of(
            "title", Map.Of("type", "string", "minLength", 1, "maxLength", 200),
            "fields", Map.Of(
                "type", "array",
                "minItems", 1,
                "uniqueItems", true,
                "items", Map.Of("enum", List.Of("title", "isbn", "authorNames")))));

    /// booksByIsbn and authorsById are keyed by id, so their shape cannot be stated
    /// with `properties`. Validating the values needs the collection walked.
    /// librarians and members are keyed by id, so like the catalogue their entries
    /// need the collection walked rather than named with `properties`.
    public static ValidationResult ValidateUserManagement(DataMap userManagementData)
    {
        var errors = new List<ValidationError>(
            Validation.Validate(UserManagementData, userManagementData).Errors());
        if (errors.Count > 0) return new Invalid(errors);

        foreach (var (collection, schema) in new[] { ("librarians", Librarian), ("members", Member) })
            if (userManagementData.ContainsKey(collection))
                CollectEntryErrors(userManagementData[collection].As<DataMap>(), schema, collection, errors);

        return errors.Count == 0 ? Valid.Instance : new Invalid(errors);
    }

    public static ValidationResult ValidateCatalog(DataMap catalogData)
    {
        var errors = new List<ValidationError>(Validation.Validate(CatalogData, catalogData).Errors());
        if (errors.Count > 0) return new Invalid(errors);

        CollectEntryErrors(_.Get<DataMap>(catalogData, "booksByIsbn"), Book, "booksByIsbn", errors);
        CollectEntryErrors(_.Get<DataMap>(catalogData, "authorsById"), Author, "authorsById", errors);

        return errors.Count == 0 ? Valid.Instance : new Invalid(errors);
    }

    private static void CollectEntryErrors(
        DataMap entries, DataMap schema, string name, List<ValidationError> errors)
    {
        foreach (var (key, value) in entries)
            foreach (var error in Validation.Validate(schema, value).Errors())
                errors.Add(error with { Path = DataPath.Of(name).Then(key).Then(error.Path) });
    }
}
