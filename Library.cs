using DataFirst.Lodash;

namespace DataFirst;


public static class Library
{

    /// Passwords are hashed once, when this data is first built.
    private static readonly DataMap UserManagementSeed =
        Map.Of(
            "librarians", Map.Of(
                "franck@gmail.com", Map.Of(
                    "email", "franck@gmail.com",
                    "password", Passwords.Hash("librarian-secret"))),
            "members", Map.Of(
                "samantha@gmail.com", Map.Of(
                    "email", "samantha@gmail.com",
                    "password", Passwords.Hash("member-secret"),
                    "isBlocked", false,
                    "isSuper", true,
                    "bookLendings", List.Of(
                        Map.Of(
                            "bookItemId", "book-item-1",
                            "bookIsbn", "978-1779501127",
                            "lendingDate", "2020-04-23"))),
                "vip@gmail.com", Map.Of(
                    "email", "vip@gmail.com",
                    "password", Passwords.Hash("vip-secret"),
                    "isVip", true)));

    public static DataMap LibraryData =
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
            ), "userManagementData", UserManagementSeed);

    public static DataMap SearchBook(DataMap libraryData, DataMap searchQuery) =>
        Catalog.SearchBook(_.Get<DataMap>(libraryData, "catalog"), searchQuery);

    public static DataMap GetBookLendings(DataMap libraryData, string userId, string memberId)
    {
        var userManagementData = _.Get<DataMap>(libraryData, "userManagementData");
        if (UserManagement.IsLibrarian(userManagementData, userId) ||
            UserManagement.IsSuperMember(userManagementData, userId))
            return Catalog.GetBookLendings(_.Get<DataMap>(libraryData, "catalog"), memberId);
        throw new Exception("Not allowed to get book lendings");
    }

    public static DataMap AddBookItem(DataMap libraryData, string userId, DataMap bookItemInfo)
    {
        var userManagementData = _.Get<DataMap>(libraryData, "userManagementData");
        if (UserManagement.IsLibrarian(userManagementData, userId) ||
            UserManagement.IsVipMember(userManagementData, userId))
            return Catalog.AddBookItem(_.Get<DataMap>(libraryData, "catalog"), bookItemInfo);
        throw new Exception("Not allowed to add book items");
    }

    public static string SearchBooksByTitleJson(DataMap libraryData, string query)
    {
        var results = Catalog.SearchBooksByTitle(_.Get<DataMap>(libraryData, "catalog"), query);
        return DataJson.Serialize(results);
    }

    /// The boundary: a request arrives as generic data and is checked against a
    /// schema before anything downstream trusts its shape. Inside the boundary the
    /// data stays generic -- validation buys confidence, not a type.
    public static string SearchBooksJson(DataMap libraryData, DataMap request)
    {
        Validation.ValidateOrThrow(Schemas.SearchRequest, request);

        var results = Catalog.SearchBooksByTitle(
            _.Get<DataMap>(libraryData, "catalog"),
            _.Get<string>(request, "title"));

        if (!request.ContainsKey("fields")) return DataJson.Serialize(results);

        var fields = _.Get<DataList>(request, "fields").Select(f => f.As<string>()).ToList();
        var projected = _.Map(results, book => (DataValue)Map.Of(
            [.. fields.SelectMany(f => new DataValue[] { f, _.Get(book, f) })]));

        return DataJson.Serialize(projected);
    }
}