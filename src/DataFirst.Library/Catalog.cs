using DataFirst.Lodash;

namespace DataFirst;

/// Raised when a book item id is already in use on the book it is being added to.
public sealed class DuplicateBookItemException(string bookItemId)
    : Exception($"A book item already exists with id '{bookItemId}'");

/// The catalogue: books, their authors, and the physical items libraries hold.
///
/// Every function takes the catalogue data as an argument and returns a value.
/// Nothing here reaches for member data -- a lending belongs to a member, so
/// GetBookLendings is handed the records rather than looking them up.
public static class Catalog
{
    /// Books matching every criterion given. An empty query matches everything.
    ///
    /// Criteria are optional and combine with AND: title and author match as
    /// case-insensitive substrings, the year bounds are inclusive.
    public static DataList SearchBook(DataMap catalogData, DataMap searchQuery)
    {
        Validation.ValidateOrThrow(Schemas.SearchQuery, searchQuery);

        var allBooks = _.Values(_.Get<DataMap>(catalogData, "booksByIsbn"));
        var matching = _.Filter(allBooks, book => Matches(catalogData, book.As<DataMap>(), searchQuery));

        return _.Map(matching, book => BookInfo(catalogData, book.As<DataMap>()));
    }

    private static bool Matches(DataMap catalogData, DataMap book, DataMap query)
    {
        if (query.ContainsKey("title")
            && !Contains(_.Get<string>(book, "title"), query["title"].As<string>()))
            return false;

        if (query.ContainsKey("author"))
        {
            var wanted = query["author"].As<string>();
            if (!AuthorNames(catalogData, book).Any(name => Contains(name.As<string>(), wanted)))
                return false;
        }

        if (!book.ContainsKey("publicationYear"))
            return !query.ContainsKey("publishedAfter") && !query.ContainsKey("publishedBefore");

        var year = _.Get<long>(book, "publicationYear");

        if (query.ContainsKey("publishedAfter") && year < query["publishedAfter"].As<long>()) return false;
        if (query.ContainsKey("publishedBefore") && year > query["publishedBefore"].As<long>()) return false;

        return true;
    }

    /// Enriches a member's lending records with the book each one refers to.
    ///
    /// The records come from user management: a lending is something a member has,
    /// and the catalogue only knows how to describe the book.
    public static DataList GetBookLendings(DataMap catalogData, DataList lendings) =>
        _.Map(lendings, lending =>
        {
            var isbn = _.Get<string>(lending, "bookIsbn");
            var path = new StringOrInt[] { "booksByIsbn", isbn };

            if (!_.ContainsKey(catalogData, path))
                throw new KeyNotFoundException($"Lending refers to unknown book '{isbn}'");

            var book = _.Get<DataMap>(catalogData, path);

            return Map.Of(
                "bookItemId", _.Get(lending, "bookItemId"),
                "lendingDate", _.Get(lending, "lendingDate"),
                "title", _.Get(book, "title"),
                "isbn", isbn,
                "authorNames", AuthorNames(catalogData, book));
        });

    /// Adds a physical item to a book, returning the new catalogue.
    /// A new item starts out not lent.
    public static DataMap AddBookItem(DataMap catalogData, DataMap bookItemInfo)
    {
        Validation.ValidateOrThrow(Schemas.BookItemInfo, bookItemInfo);

        var isbn = _.Get<string>(bookItemInfo, "isbn");
        var bookPath = new StringOrInt[] { "booksByIsbn", isbn };

        if (!_.ContainsKey(catalogData, bookPath))
            throw new KeyNotFoundException($"No book with isbn '{isbn}'");

        return _.Set(catalogData, bookPath,
            AddItemToBook(_.Get<DataMap>(catalogData, bookPath), bookItemInfo));
    }

    /// The same operation scoped to a single book, which is all a caller holding that
    /// aggregate can offer. AddBookItem is this plus the lookup.
    ///
    /// Aggregate boundaries push back into function signatures like this: a client
    /// that only has the book cannot call something that wants the whole catalogue.
    public static DataMap AddItemToBook(DataMap book, DataMap bookItemInfo)
    {
        Validation.ValidateOrThrow(Schemas.BookItemInfo, bookItemInfo);

        var existing = book.ContainsKey("bookItems") ? _.Get<DataList>(book, "bookItems") : DataList.Empty;

        var id = _.Get<string>(bookItemInfo, "id");
        if (existing.Any(item => _.Get<string>(item, "id") == id))
            throw new DuplicateBookItemException(id);

        var item = Map.Of(
            "id", id,
            "libId", _.Get<string>(bookItemInfo, "libId"),
            "isLent", false);

        return book.SetItem("bookItems", existing.Add(item));
    }

    public static DataList AuthorNames(DataMap catalogData, DataMap book)
    {
        var authorIds = _.Get<DataList>(book, "authorIds");
        return _.Map(authorIds, authorId =>
            _.Get(catalogData, ["authorsById", authorId.As<string>(), "name"]));
    }

    public static DataMap BookInfo(DataMap catalogData, DataMap book) =>
        Map.Of(
            "title", _.Get(book, "title"),
            "isbn", _.Get(book, "isbn"),
            "authorNames", AuthorNames(catalogData, book));

    public static DataList SearchBooksByTitle(DataMap catalogData, string query)
    {
        var allBooks = _.Values(_.Get<DataMap>(catalogData, "booksByIsbn"));

        var matchingBooks = _.Filter(allBooks, book =>
            Contains(_.Get<string>(book, "title"), query));

        return _.Map(matchingBooks, book => BookInfo(catalogData, book.As<DataMap>()));
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
