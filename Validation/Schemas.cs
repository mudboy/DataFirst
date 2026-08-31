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
            "userManagementData", Map.Of("type", "object")));

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
