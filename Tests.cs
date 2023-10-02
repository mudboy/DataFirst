using System.Collections.Immutable;
using FluentAssertions;
using Xunit;

using StringMap = System.Collections.Immutable.ImmutableDictionary<string, object>;

namespace DataFirst;

public sealed class Tests
{
    private static readonly StringMap watchmenMap = Map.Of(
        "isbn", "978-1779501127",
        "title", "Watchmen",
        "publicationYear", 1987
    );

    private static readonly StringMap sevenHabitsMap = Map.Of(
        "isbn", "978-1982137274",
        "title", "7 Habits of Highly Effective People",
        "publicationYear", 2020
    );

    private readonly StringMap searchResultsMap = Map.Of(
        "978-1779501127", watchmenMap,
        "978-1982137274", sevenHabitsMap
    );

    private static readonly ImmutableList<StringMap> authorsListMap = List.Of(
        Map.Of("isbn", "978-1982137274",
            "title", "7 Habits of Highly Effective People",
            "author_name", "Steven Clarey"),
        Map.Of("isbn", "978-1982137274",
            "title", "7 Habits of Highly Effective People",
            "author_name", "Tom Jons"),
        Map.Of("isbn", "978-1779501127",
            "title", "Watchmen",
            "author_name", "Billy Gibson")
    );

    [Fact]
    public void Should_Get_Key()
    {
        _.Get<string>(watchmenMap, "title").ToUpper()
            .Should().Be("WATCHMEN");
    }

    [Fact]
    public void Should_Get_By_Path()
    {
        _.Get<string>(searchResultsMap, List.Of("978-1779501127", "title"))
            .Should().Be("Watchmen");
    }

    [Fact]
    public void Should_Check_By_Path()
    {
        _.ContainsKey(searchResultsMap, List.Of("978-1779501127", "title"))
            .Should().BeTrue();
    }

    [Fact]
    public void Should_Be_Able_To_Use_Key_Getters()
    {
        var TITLE = Getter.Create<string>("title");

        TITLE.Get(watchmenMap).ToUpper().Should().Be("WATCHMEN");
    }

    [Fact]
    public void Should_Be_Able_To_Use_Path_Getters()
    {
        var TITLE = Getter.Create<string>(List.Of("978-1779501127", "title"));

        TITLE.Get(searchResultsMap).ToUpper().Should().Be("WATCHMEN");
    }

    [Fact]
    public void Table_As_List_Of_Maps()
    {
        var maps = With.Database(Db.ReadFrom);

        var expected = List.Of(
            Map.Of("isbn", "978-1982137274", 
                               "title", "7 Habits of Highly Effective People", 
                               "publication_year", 1998),
            Map.Of("isbn", "978-0812981605", 
                   "title", "Watchmen", 
                   "publication_year", 1985));

        maps.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Should_Aggregate_Authors()
    {
        var rows7Habits = List.Of(
            Map.Of("author_name", "Sean Covey",
                "isbn", "978-1982137274",
                "title", "7 Habits of Highly Effective People"),
            Map.Of("author_name", "Stephen Covey",
                "isbn", "978-1982137274",
                "title", "7 Habits of Highly Effective People")
        );
        
        var expectedResults = Map.Of(
            "isbn", "978-1982137274",
            "title", "7 Habits of Highly Effective People",
            "authorNames", List.Of("Sean Covey", "Stephen Covey")
        );

        AggregateField(rows7Habits, "author_name", "authorNames")
            .Should().BeEquivalentTo(expectedResults);
    }

    [Fact]
    public void Should_Aggregate_Fields()
    {
        AggregateFields(authorsListMap, "isbn", "author_name", "authorNames")
            .Should().BeEquivalentTo(List.Of(
                Map.Of("isbn", "978-1982137274",
                    "title", "7 Habits of Highly Effective People",
                    "authorNames", List.Of("Tom Jons", "Steven Clarey")),
                Map.Of("isbn", "978-1779501127",
                    "title", "Watchmen",
                    "authorNames", List.Of("Billy Gibson"))
                ));
    }

    public static ImmutableList<StringMap> AggregateFields(ImmutableList<StringMap> rows, string idFieldName, string fieldName,
        string aggregateFieldName)
    {
        var rowsByIsbn = _.GroupBy(rows, idFieldName);
        var groupedRows = _.Values(rowsByIsbn);
        return _.Map(groupedRows, x => AggregateField(x, fieldName, aggregateFieldName));
    }
    
    public static StringMap AggregateField(ImmutableList<StringMap> rows, string fieldName, string newName)
    {
        var aggregatedValues = _.Map(rows, x => _.Get(x, fieldName));
        var firstRow = rows[0];
        var firstRowWithAggregatedValues = _.Set(firstRow, newName, aggregatedValues);
        return firstRowWithAggregatedValues.Remove(fieldName);
    }

    [Fact]
    public void Should_KeyBy()
    {
        var books = List.Of(
            Map.Of(
                "title", "7 Habits of Highly Effective People",
                "isbn", "978-1982137274",
                "available", true
            ),
            Map.Of(
                "title", "The Power of Habit",
                "isbn", "978-0812981605",
                "available", false
            ));

        _.KeyBy(books, "isbn").Should().BeEquivalentTo(
            Map.Of(
                "978-0812981605", Map.Of(
                    "available", false,
                    "isbn", "978-0812981605",
                    "title", "The Power of Habit"
                ),
                "978-1982137274", Map.Of(
                    "available", true,
                    "isbn", "978-1982137274",
                    "title", "7 Habits of Highly Effective People"
                )
            ));
    }

    [Fact]
    public void Should_Update()
    {
        var input = Map.Of("name", List.Of("one", "two", "one"));

        var result = _.Update(input, "name", 
            o => ((ImmutableList<string>)o).Distinct().ToImmutableList());

        result.Should().BeEquivalentTo(Map.Of("name", List.Of("one", "two")));
    }

    [Fact]
    public void Should_Unwind()
    {
        var customer = Map.Of(
            "customer-id", "joe",
            "items", List.Of(
                Map.Of(
                    "item", "phone",
                    "quantity", 1
                ),
                Map.Of(
                    "item", "pencil",
                    "quantity", 10
                )
            ));

        var expectedRes = List.Of(
            Map.Of(
                "customer-id", "joe",
                "items", Map.Of(
                    "item", "phone",
                    "quantity", 1
                )
            ),
            Map.Of(
                "customer-id", "joe",
                "items", Map.Of(
                    "item", "pencil",
                    "quantity", 10
                )
            ));

        var result = _.Unwind(customer, "items");
        result.Should().BeEquivalentTo(expectedRes);
    }

    [Fact]
    public void Should_Get_AuthorNames()
    {
        var catalogData = _.Get<StringMap>(Library.LibraryData, "catalog");
        var book = _.Get<StringMap>(Library.LibraryData, List.Of("catalog", "booksByIsbn", "978-1779501127"));
        
        var names = Catalog.AuthorNames(catalogData, book);

        names.Should().BeEquivalentTo(List.Of("Alan Moore", "Dave Gibbons"));
    }

    [Fact]
    public void Should_Search_Books_By_Title()
    {   
        var catalogData = _.Get<StringMap>(Library.LibraryData, "catalog");

        var result = Catalog.SearchBooksByTitle(catalogData, "Wat");

        result.Should().BeEquivalentTo(List.Of(
            Map.Of("authorNames", List.Of("Alan Moore", "Dave Gibbons"),
                "isbn", "978-1779501127",
                "title", "Watchmen")));
    }

    [Fact]
    public void Should_Search_Library_Books_By_Title_Json()
    {
        var result = Library.SearchBooksByTitleJson(Library.LibraryData, "Watchmen");

        result.Should().NotBeEmpty();
    }
}
