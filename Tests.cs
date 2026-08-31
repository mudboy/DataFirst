using System.Collections.Immutable;
using DataFirst.Lodash;
using FluentAssertions;
using Xunit;

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

    private static readonly IndexedList authorsListMap = List.Of(
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
        _.Get<string>(searchResultsMap, ["978-1779501127", "title"])
            .Should().Be("Watchmen");
    }

    [Fact]
    public void Should_Check_By_Path()
    {
        _.ContainsKey(searchResultsMap, ["978-1779501127", "title"])
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
        var TITLE = Getter.Create<string>(["978-1779501127", "title"]);

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

        var result = _.AggregateField(rows7Habits, "author_name", "authorNames");
        result.Should().BeEquivalentTo(expectedResults);
    }

    [Fact]
    public void Should_Aggregate_Fields()
    {
        var expectedResult = List.Of(
            Map.Of("isbn", "978-1982137274",
                "title", "7 Habits of Highly Effective People",
                "authorNames", List.Of("Tom Jons", "Steven Clarey")),
            Map.Of("isbn", "978-1779501127",
                "title", "Watchmen",
                "authorNames", List.Of("Billy Gibson"))
        );
        var result = _.AggregateFields(authorsListMap, "isbn", "author_name", "authorNames");
        result.Should().BeEquivalentTo(expectedResult);
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
            o => ((IndexedList)o).Distinct().ToImmutableList());

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
        var book = _.Get<StringMap>(Library.LibraryData, ["catalog", "booksByIsbn", "978-1779501127"]);
        
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
    
    [Fact]
    public void Should_Not_Modify_Original_With_Set()
    {
        var oldData = Library.LibraryData;
        var newData = (StringMap)_.Set(oldData, 
            ["catalog", "booksByIsbn", "978-1779501127", "publicationYear"], 1986);

        newData.Should().NotBeEquivalentTo(oldData);
    }

    [Fact]
    public void Should_Be_Immutable()
    {
        var books = Map.Of(
            "978-1779501127", Map.Of(
                "isbn", "978-1779501127",
                "title", "Watchmen",
                "publicationYear", 1987,
                "authorIds", List.Of("alan-moore", "dave-gibbons")
            ));

        var nextBooks = _.Set(books, ["978-1779501127", "publicationYear"], 1986);
        var beforeName = _.Get(nextBooks, ["978-1779501127", "authorIds", 1]);
        _.Set(nextBooks, ["978-1779501127", "authorIds", 1], "dave-chester-gibbons");
        var afterName = _.Get(nextBooks, ["978-1779501127", "authorIds", 1]);

        beforeName.Should().Be(afterName);
    }

    [Fact]
    public void Should_Replace_Not_Insert_When_Setting_An_Existing_Index()
    {
        var authorIds = List.Of("alan-moore", "dave-gibbons");

        var result = (IndexedList)_.Set(authorIds, 1, "dave-chester-gibbons");

        result.Should().BeEquivalentTo(List.Of("alan-moore", "dave-chester-gibbons"),
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Should_Replace_Nested_List_Item_On_Set()
    {
        var books = Map.Of(
            "978-1779501127", Map.Of(
                "authorIds", List.Of("alan-moore", "dave-gibbons")));

        var updated = _.Set(books, ["978-1779501127", "authorIds", 1], "dave-chester-gibbons");

        _.Get<IndexedList>(updated, ["978-1779501127", "authorIds"])
            .Should().BeEquivalentTo(List.Of("alan-moore", "dave-chester-gibbons"),
                o => o.WithStrictOrdering());
    }

    [Fact]
    public void Should_Pad_With_Nulls_When_Setting_Past_The_End()
    {
        var result = (IndexedList)_.Set(List.Of("first"), 3, "fourth");

        result.Should().BeEquivalentTo(List.Of("first", null!, null!, "fourth"),
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Should_Insert_Rather_Than_Replace_With_InsertAt()
    {
        _.InsertAt(List.Of(1), 1, 4)
            .Should().BeEquivalentTo(List.Of(1, 4), o => o.WithStrictOrdering());

        _.InsertAt(List.Of("a", "c"), 1, "b")
            .Should().BeEquivalentTo(List.Of("a", "b", "c"), o => o.WithStrictOrdering());
    }

    [Fact]
    public void Should_Reduce_A_List_With_Increasing_Indexes()
    {
        var seenIndexes = new System.Collections.Generic.List<object>();

        var total = _.Reduce(List.Of(10, 20, 30), (acc, v, idx) =>
        {
            seenIndexes.Add(idx);
            return (int)acc + (int)v;
        }, 0);

        total.Should().Be(60);
        seenIndexes.Should().BeEquivalentTo(new object[] { 0, 1, 2 }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Should_Reduce_A_Map_With_Its_Keys()
    {
        var seenKeys = new System.Collections.Generic.List<object>();

        var total = _.Reduce(Map.Of("a", 1, "b", 2), (acc, v, key) =>
        {
            seenKeys.Add(key);
            return (int)acc + (int)v;
        }, 0);

        total.Should().Be(3);
        seenKeys.Should().BeEquivalentTo(new object[] { "a", "b" });
    }

    [Fact]
    public void Should_Add_Non_Existent_Items_On_Set()
    {
        var map = Map.Of("key", "value");
        var updated = _.Set(map, "isVip", true);

        _.ContainsKey(updated, "isVip").Should().BeTrue();
        _.Get<bool>(updated, "isVip").Should().BeTrue();
    }

    [Fact]
    public void Diffing()
    {
        var data1 = Map.Of(
            "a", Map.Of(
                "x", 1,
                "y", List.Of(2, 3),
                "z", 4
            ));
        
        var data2 = Map.Of(
            "a", Map.Of(
                "x", 2,
                "y", List.Of(2, 4),
                "z", 4
            ));

        var expected = Map.Of(
            "a", Map.Of(
                "x", 2,
                "y", List.Of(null!, 4)
            ));

        var diff = _.DiffObjects(data1, data2);
        diff.Should().BeEquivalentTo(expected);

        // var empty = List.Of(1);
        // var ins = _.InsertAt(empty, 1, 4);
        // ins.Should().BeEquivalentTo(List.Of(1, 4));

        var d1 = List.Of(1, 2);
        var d2 = List.Of(1, 4);
        
        var no = _.Diff(_.Get(d1, "0"), _.Get(d2, "0"));
        
        (no is NoDiff).Should().BeTrue();
        
        var res = _.DiffObjects(d1, d2);
        
        res.Should().BeEquivalentTo(List.Of(null, 4));

    }

    [Fact]
    public void Should_Not_Confuse_A_Literal_No_Diff_Value_With_An_Unchanged_Node()
    {
        // "no-diff" used to be the sentinel, so a real value of "no-diff" was
        // silently dropped from the diff as though nothing had changed.
        var before = Map.Of("status", "pending");
        var after = Map.Of("status", "no-diff");

        _.DiffObjects(before, after)
            .Should().BeEquivalentTo(Map.Of("status", "no-diff"));
    }

    [Fact]
    public void Should_Report_Changed_Leaves_And_Unchanged_Nodes()
    {
        var unchanged = _.Diff("Watchmen", "Watchmen");
        (unchanged is NoDiff).Should().BeTrue();

        // A union value's runtime type is the union itself, so pattern matching
        // rather than BeOfType is how you get at the case.
        var changed = _.Diff("Watchmen", "The Watchmen") switch
        {
            Changed(var value) => value,
            NoDiff => null
        };
        changed.Should().Be("The Watchmen");

        var equivalentMaps = _.Diff(
            Map.Of("title", "Watchmen"),
            Map.Of("title", "Watchmen"));
        (equivalentMaps is NoDiff).Should().BeTrue();
    }

    [Fact]
    public void DiffyLoop()
    {
        var watchmen = Map.Of(
            "isbn", "978-1779501127",
            "title", "Watchmen",
            "publicationYear", 1987,
            "authorIds", List.Of("alan-moore", "dave-gibbons"));
        var alan = Map.Of(
            "name", "Alan Moore",
            "bookIsbns", List.Of("978-1779501127"));
        var dave = Map.Of(
            "name", "Dave Gibbons",
            "bookIsbns", List.Of("978-1779501127"));
        
        var library = Map.Of(
            "catalog", Map.Of(
                "booksByIsbn", Map.Of("978-1779501127", watchmen),
                "authorsById", Map.Of("alan-moore", alan, "dave-gibbons", dave)));

        var previous = library;
        var next = _.Set(library, 
            ["catalog", "booksByIsbn", "978-1779501127", "publicationYear"], 1986);
        var libraryWithUpdatedTitle = _.Set(library,
            ["catalog", "booksByIsbn", "978-1779501127", "title"], "The Watchmen");
        var current = _.Set(libraryWithUpdatedTitle, 
            ["catalog", "authorsById", "dave-gibbons", "name"], "David Chester Gibbons");

        // next changes only the publication year...
        var diff1 = _.DiffObjects(previous, next);

        diff1.Should().BeEquivalentTo(
            Map.Of("catalog", Map.Of(
                "booksByIsbn", Map.Of(
                    "978-1779501127", Map.Of(
                        "publicationYear", 1986)))));

        // ...while current changes the title and one author's name.
        var diff2 = _.DiffObjects(previous, current);

        diff2.Should().BeEquivalentTo(
            Map.Of("catalog", Map.Of(
                "booksByIsbn", Map.Of(
                    "978-1779501127", Map.Of(
                        "title", "The Watchmen")),
                "authorsById", Map.Of(
                    "dave-gibbons", Map.Of(
                        "name", "David Chester Gibbons")))));
    }
}
