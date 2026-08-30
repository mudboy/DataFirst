using DataFirst.Lodash;

namespace DataFirst;

public static class Catalog
{
    public static StringMap SearchBook(StringMap catalogData, StringMap searchQuery) => 
        throw new NotImplementedException();

    public static StringMap GetBookLendings(StringMap CatalogData, string memberId) =>
        throw new NotImplementedException();

    public static StringMap AddBookItem(StringMap catalogData, StringMap bookItemInfo) =>
        throw new NotImplementedException();

    public static IndexedList AuthorNames(StringMap catalogData, StringMap book)
    {
        var authorIds = _.Get<IndexedList>(book, "authorIds");
        var names = _.Map<string>(authorIds, authorId => 
            _.Get<string>(catalogData, ["authorsById", authorId, "name"]));
        return names;
    }

    public static StringMap bookInfo(StringMap catalogData, StringMap book) =>
        Map.Of("title", _.Get(book, "title"),
            "isbn", _.Get(book, "isbn"),
            "authorNames", AuthorNames(catalogData, book));

    public static IndexedList SearchBooksByTitle(StringMap catalogData, string query)
    {
        var allBooks = _.Values(_.Get<StringMap>(catalogData, "booksByIsbn"));
        var matchingBooks = 
            _.Filter(allBooks, book => 
                ((string)_.Get(book, "title")).Contains(query));

        return _.Map<StringMap>(matchingBooks, book => bookInfo(catalogData, book));
    }
}