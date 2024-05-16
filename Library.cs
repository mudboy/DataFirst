using System.Text.Json;

namespace DataFirst;


public static class Library
{

    public static StringMap LibraryData =
        Map.Of("catalog",
            Map.Of(
                "booksByIsbn", Map.Of(
                    "978-1779501127", Map.Of(
                        "isbn", "978-1779501127",
                        "title", "Watchmen",
                        "publicationYear", 1987,
                        "authorIds", List.Of("alan-moore", "dave-gibbons"),
                        "bookItems", List.Of(
                            Map.Of(
                                "id", "book-item-1",
                                "libId", "nyc-central-lib",
                                "isLent", true
                            ),
                            Map.Of(
                                "id", "book-item-2",
                                "libId", "nyc-central-lib",
                                "isLent", false
                            )
                        )
                    )
                ),
                "authorsById", Map.Of(
                    "alan-moore", Map.Of(
                        "name", "Alan Moore",
                        "bookIsbns", List.Of("978-1779501127")
                    ),
                    "dave-gibbons", Map.Of(
                        "name", "Dave Gibbons",
                        "bookIsbns", List.Of("978-1779501127")
                    )
                )
            ), "userManagementData", Map.Of());

    public static StringMap SearchBook(StringMap libraryData, StringMap searchQuery) =>
        Catalog.SearchBook(_.Get<StringMap>(libraryData, "catalog"), searchQuery);

    public static StringMap GetBookLendings(StringMap libraryData, string userId, string memberId)
    {
        var userManagementData = _.Get<StringMap>(libraryData, "userManagementData");
        if (UserManagement.IsLibrarian(userManagementData, userId) ||
            UserManagement.IsSuperMember(userManagementData, userId))
            return Catalog.GetBookLendings(_.Get<StringMap>(libraryData, "catalog"), memberId);
        throw new Exception("Not allowed to get book lendings");
    }

    public static StringMap AddBookItem(StringMap libraryData, string userId, StringMap bookItemInfo)
    {
        var userManagementData = _.Get<StringMap>(libraryData, "userManagementData");
        if (UserManagement.IsLibrarian(userManagementData, userId) ||
            UserManagement.IsVipMember(userManagementData, userId))
            return Catalog.AddBookItem(_.Get<StringMap>(libraryData, "catalog"), bookItemInfo);
        throw new Exception("Not allowed to add book items");
    }

    public static string SearchBooksByTitleJson(StringMap libraryData, string query)
    {
        var results = Catalog.SearchBooksByTitle(_.Get<StringMap>(libraryData, "catalog"), query);
        var resultsJson = JsonSerializer.Serialize(results);
        return resultsJson;
    }
}