using System.Collections.Immutable;

namespace DataFirst;

public static class Catalog
{
    public static ImmutableDictionary<string, object> SearchBook(ImmutableDictionary<string, dynamic> catalogData, ImmutableDictionary<string, dynamic> searchQuery) => 
        throw new NotImplementedException();

    public static ImmutableDictionary<string, object> GetBookLendings(ImmutableDictionary<string, object> CatalogData, string memberId) =>
        throw new NotImplementedException();

    public static ImmutableDictionary<string, object> AddBookItem(ImmutableDictionary<string, object> catalogData, ImmutableDictionary<string, object> bookItemInfo) =>
        throw new NotImplementedException();

    public static ImmutableList<string> AuthorNames(ImmutableDictionary<string, object> catalogData, ImmutableDictionary<string, object> book)
    {
        var authorIds = _.Get<ImmutableList<string>>(book, "authorIds");
        var names = _.Map(authorIds, authorId => 
            _.Get<string>(catalogData, List.Of("authorsById", authorId, "name")));
        return names;
    }

    public static ImmutableDictionary<string, object> bookInfo(ImmutableDictionary<string, object> catalogData, ImmutableDictionary<string, object> book) =>
        Map.Of("title", _.Get(book, "title"),
            "isbn", _.Get(book, "isbn"),
            "authorNames", AuthorNames(catalogData, book));

    public static ImmutableList<ImmutableDictionary<string, object>> SearchBooksByTitle(ImmutableDictionary<string, object> catalogData, string query)
    {
        var allBooks = _.Values<ImmutableDictionary<string, object>>(_.Get<ImmutableDictionary<string, object>>(catalogData, "booksByIsbn"));
        var matchingBooks = 
            _.Filter(allBooks, book => 
                _.Get<string>(book, "title").Contains(query));

        return _.Map(matchingBooks, book => bookInfo(catalogData, book));
    }
}