using System.Collections.Immutable;

namespace DataFirst;

public static class Catalog
{
    public static StringMap SearchBook(StringMap catalogData, StringMap searchQuery) => 
        throw new NotImplementedException();

    public static StringMap GetBookLendings(StringMap CatalogData, string memberId) =>
        throw new NotImplementedException();

    public static StringMap AddBookItem(StringMap catalogData, StringMap bookItemInfo) =>
        throw new NotImplementedException();

    public static ImmutableList<string> AuthorNames(StringMap catalogData, StringMap book)
    {
        var authorIds = _.Get<ImmutableList<string>>(book, "authorIds");
        var names = _.Map(authorIds, authorId => 
            _.Get<string>(catalogData, List.Of("authorsById", authorId, "name")));
        return names;
    }

    public static StringMap bookInfo(StringMap catalogData, StringMap book) =>
        Map.Of("title", _.Get(book, "title"),
            "isbn", _.Get(book, "isbn"),
            "authorNames", AuthorNames(catalogData, book));

    public static ImmutableList<StringMap> SearchBooksByTitle(StringMap catalogData, string query)
    {
        var allBooks = _.Values<StringMap>(_.Get<StringMap>(catalogData, "booksByIsbn"));
        var matchingBooks = 
            _.Filter(allBooks, book => 
                _.Get<string>(book, "title").Contains(query));

        return _.Map(matchingBooks, book => bookInfo(catalogData, book));
    }
}