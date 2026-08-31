using DataFirst.Lodash;

namespace DataFirst;

public static class Catalog
{
    public static DataMap SearchBook(DataMap catalogData, DataMap searchQuery) =>
        throw new NotImplementedException();

    public static DataMap GetBookLendings(DataMap catalogData, string memberId) =>
        throw new NotImplementedException();

    public static DataMap AddBookItem(DataMap catalogData, DataMap bookItemInfo) =>
        throw new NotImplementedException();

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
            _.Get<string>(book, "title").Contains(query));

        return _.Map(matchingBooks, book => BookInfo(catalogData, book.As<DataMap>()));
    }
}
