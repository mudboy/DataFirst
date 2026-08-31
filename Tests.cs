using System.Collections.Immutable;
using DataFirst.Lodash;
using FluentAssertions;
using Xunit;

namespace DataFirst;

public sealed class Tests
{
    /// Asserts through DataMap/DataList's own structural equality. Both implement
    /// IEnumerable, so a plain Should().Be() would route to FluentAssertions'
    /// collection assertions and walk members instead. Failure messages print the
    /// values as JSON.
    private static void ShouldEqual(object actual, object expected) =>
        actual.Equals(expected).Should().BeTrue($"of\n  expected: {expected}\n  actual:   {actual}");

    private static void ShouldNotEqual(object actual, object expected) =>
        actual.Should().NotBe(expected);

    private static readonly DataMap watchmenMap = Map.Of(
        "isbn", "978-1779501127",
        "title", "Watchmen",
        "publicationYear", 1987
    );

    private static readonly DataMap sevenHabitsMap = Map.Of(
        "isbn", "978-1982137274",
        "title", "7 Habits of Highly Effective People",
        "publicationYear", 2020
    );

    private readonly DataMap searchResultsMap = Map.Of(
        "978-1779501127", watchmenMap,
        "978-1982137274", sevenHabitsMap
    );

    private static readonly DataList authorsListMap = List.Of(
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

        ShouldEqual(maps, expected);
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
        ShouldEqual(result, expectedResults);
    }

    [Fact]
    public void Should_Aggregate_Fields()
    {
        var expectedResult = List.Of(
            Map.Of("isbn", "978-1982137274",
                "title", "7 Habits of Highly Effective People",
                "authorNames", List.Of("Steven Clarey", "Tom Jons")),
            Map.Of("isbn", "978-1779501127",
                "title", "Watchmen",
                "authorNames", List.Of("Billy Gibson"))
        );
        var result = _.AggregateFields(authorsListMap, "isbn", "author_name", "authorNames");
        ShouldEqual(result, expectedResult);
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

        ShouldEqual(_.KeyBy(books, "isbn"),
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
            o => DataList.Create(o.As<DataList>().Distinct()));

        ShouldEqual(result, Map.Of("name", List.Of("one", "two")));
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
        ShouldEqual(result, expectedRes);
    }

    [Fact]
    public void Should_Get_AuthorNames()
    {
        var catalogData = _.Get<DataMap>(Library.LibraryData, "catalog");
        var book = _.Get<DataMap>(Library.LibraryData, ["catalog", "booksByIsbn", "978-1779501127"]);
        
        var names = Catalog.AuthorNames(catalogData, book);

        ShouldEqual(names, List.Of("Alan Moore", "Dave Gibbons"));
    }

    [Fact]
    public void Should_Search_Books_By_Title()
    {   
        var catalogData = _.Get<DataMap>(Library.LibraryData, "catalog");

        var result = Catalog.SearchBooksByTitle(catalogData, "Wat");

        ShouldEqual(result, List.Of(
            Map.Of("authorNames", List.Of("Alan Moore", "Dave Gibbons"),
                "isbn", "978-1779501127",
                "title", "Watchmen")));
    }

    [Fact]
    public void Should_Search_Library_Books_By_Title_Json()
    {
        var result = Library.SearchBooksByTitleJson(Library.LibraryData, "Watchmen");

        result.Should().Be(
            """[{"title":"Watchmen","isbn":"978-1779501127","authorNames":["Alan Moore","Dave Gibbons"]}]""");
    }    
    
    [Fact]
    public void Should_Walk_A_Path_That_Mixes_Keys_And_Indexes()
    {
        var data = Map.Of(
            "a", List.Of(
                Map.Of("x", "wrong"),
                Map.Of("b", List.Of("zero", "one", "two"))));

        _.Get<string>(data, ["a", 1, "b", 2]).Should().Be("two");
    }

    [Fact]
    public void Should_Write_Through_A_Path_That_Mixes_Keys_And_Indexes()
    {
        var data = Map.Of(
            "a", List.Of(
                Map.Of("b", List.Of("zero", "one"))));

        var updated = _.Set(data, ["a", 0, "b", 1], "ONE");

        ShouldEqual(updated, Map.Of(
            "a", List.Of(
                Map.Of("b", List.Of("zero", "ONE")))));
    }

    [Fact]
    public void Should_Check_A_Path_Through_Lists()
    {
        var data = Map.Of("a", List.Of(Map.Of("b", "value")));

        _.ContainsKey(data, ["a", 0, "b"]).Should().BeTrue();
        _.ContainsKey(data, ["a", 0, "missing"]).Should().BeFalse();
        _.ContainsKey(data, ["a", 9, "b"]).Should().BeFalse();

        // A leaf part-way along the path is absence, not an exception.
        _.ContainsKey(data, ["a", 0, "b", "deeper"]).Should().BeFalse();
    }

    [Fact]
    public void Should_Switch_Exhaustively_Over_Every_Case()
    {
        // No default arm: the compiler checks this covers the union.
        static string Name(DataValue v) => v switch
        {
            DataNull => "null",
            string => "string",
            long => "long",
            double => "double",
            bool => "bool",
            DataMap => "map",
            DataList => "list"
        };

        Name(DataNull.Instance).Should().Be("null");
        Name("x").Should().Be("string");
        Name(1987).Should().Be("long");
        Name(1.5).Should().Be("double");
        Name(true).Should().Be("bool");
        Name(Map.Of()).Should().Be("map");
        Name(List.Of()).Should().Be("list");
    }

    [Fact]
    public void Should_Preserve_Insertion_Order()
    {
        var map = Map.Of("z", 1, "a", 2, "m", 3);

        map.Keys.Should().Equal("z", "a", "m");

        // Overwriting keeps position; a new key appends.
        _.Set(map, "a", 99).Keys.Should().Equal("z", "a", "m");
        _.Set(map, "b", 4).Keys.Should().Equal("z", "a", "m", "b");

        // Order is presentation only -- equality ignores it.
        ShouldEqual(Map.Of("a", 1, "b", 2), Map.Of("b", 2, "a", 1));
    }

    [Fact]
    public void Should_Not_Modify_Original_With_Set()
    {
        var oldData = Library.LibraryData;
        var newData = _.Set(oldData, 
            ["catalog", "booksByIsbn", "978-1779501127", "publicationYear"], 1986);

        ShouldNotEqual(newData, oldData);
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

        var result = _.Set(authorIds, 1, "dave-chester-gibbons").As<DataList>();

        ShouldEqual(result, List.Of("alan-moore", "dave-chester-gibbons"));
    }

    [Fact]
    public void Should_Replace_Nested_List_Item_On_Set()
    {
        var books = Map.Of(
            "978-1779501127", Map.Of(
                "authorIds", List.Of("alan-moore", "dave-gibbons")));

        var updated = _.Set(books, ["978-1779501127", "authorIds", 1], "dave-chester-gibbons");

        ShouldEqual(
            _.Get<DataList>(updated, ["978-1779501127", "authorIds"]),
            List.Of("alan-moore", "dave-chester-gibbons"));
    }

    [Fact]
    public void Should_Pad_With_Nulls_When_Setting_Past_The_End()
    {
        var result = _.Set(List.Of("first"), 3, "fourth").As<DataList>();

        ShouldEqual(result, List.Of("first", DataNull.Instance, DataNull.Instance, "fourth"));
    }

    [Fact]
    public void Should_Insert_Rather_Than_Replace_With_InsertAt()
    {
        ShouldEqual(_.InsertAt(List.Of(1), 1, 4), List.Of(1, 4));

        ShouldEqual(_.InsertAt(List.Of("a", "c"), 1, "b"), List.Of("a", "b", "c"));
    }

    [Fact]
    public void Should_Reduce_A_List_With_Increasing_Indexes()
    {
        var seenIndexes = new System.Collections.Generic.List<int>();

        var total = _.Reduce(List.Of(10, 20, 30), (int acc, DataValue v, StringOrInt idx) =>
        {
            seenIndexes.Add(idx switch { int i => i, string s => int.Parse(s) });
            return acc + (int)v.As<long>();
        }, 0);

        total.Should().Be(60);
        seenIndexes.Should().Equal(0, 1, 2);
    }

    [Fact]
    public void Should_Reduce_A_Map_With_Its_Keys()
    {
        var seenKeys = new System.Collections.Generic.List<string>();

        var total = _.Reduce(Map.Of("a", 1, "b", 2), (int acc, DataValue v, StringOrInt key) =>
        {
            seenKeys.Add(key switch { string s => s, int i => i.ToString() });
            return acc + (int)v.As<long>();
        }, 0);

        total.Should().Be(3);
        seenKeys.Should().BeEquivalentTo("a", "b");
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
                "y", List.Of(DataNull.Instance, 4)
            ));

        var diff = _.DiffObjects(data1, data2);
        ShouldEqual(diff.As<DataMap>(), expected);

        // var empty = List.Of(1);
        // var ins = _.InsertAt(empty, 1, 4);
        // ins.Should().BeEquivalentTo(List.Of(1, 4));

        var d1 = List.Of(1, 2);
        var d2 = List.Of(1, 4);
        
        var no = _.Diff(_.Get(d1, "0"), _.Get(d2, "0"));
        
        (no is NoDiff).Should().BeTrue();
        
        var res = _.DiffObjects(d1, d2);
        
        ShouldEqual(res.As<DataList>(), List.Of(DataNull.Instance, 4));

    }

    [Fact]
    public void Should_Not_Confuse_A_Literal_No_Diff_Value_With_An_Unchanged_Node()
    {
        // "no-diff" used to be the sentinel, so a real value of "no-diff" was
        // silently dropped from the diff as though nothing had changed.
        var before = Map.Of("status", "pending");
        var after = Map.Of("status", "no-diff");

        ShouldEqual(
            _.DiffObjects(before, after).As<DataMap>(),
            Map.Of("status", "no-diff"));
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
            Changed(var value) => value.As<string>(),
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

        ShouldEqual(diff1.As<DataMap>(),
            Map.Of("catalog", Map.Of(
                "booksByIsbn", Map.Of(
                    "978-1779501127", Map.Of(
                        "publicationYear", 1986)))));

        // ...while current changes the title and one author's name.
        var diff2 = _.DiffObjects(previous, current);

        ShouldEqual(diff2.As<DataMap>(),
            Map.Of("catalog", Map.Of(
                "booksByIsbn", Map.Of(
                    "978-1779501127", Map.Of(
                        "title", "The Watchmen")),
                "authorsById", Map.Of(
                    "dave-gibbons", Map.Of(
                        "name", "David Chester Gibbons")))));
    }
}
