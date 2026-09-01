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

        // A list diff is keyed by index: only slot 1 changed, and there is no
        // padding to confuse with a real null.
        var expected = Map.Of(
            "a", Map.Of(
                "x", 2,
                "y", Map.Of("1", 4)
            ));

        var diff = _.DiffObjects(data1, data2);
        ShouldEqual(diff, expected);

        // var empty = List.Of(1);
        // var ins = _.InsertAt(empty, 1, 4);
        // ins.Should().BeEquivalentTo(List.Of(1, 4));

        var d1 = List.Of(1, 2);
        var d2 = List.Of(1, 4);
        
        var no = _.Diff(_.Get(d1, "0"), _.Get(d2, "0"));
        
        (no is NoDiff).Should().BeTrue();
        
        var res = _.DiffObjects(d1, d2);
        
        ShouldEqual(res, Map.Of("1", 4));

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
    public void Should_List_Information_Paths()
    {
        var data = Map.Of(
            "a", Map.Of("x", 1, "y", List.Of("p", "q")),
            "b", true);

        _.InformationPaths(data).Select(p => p.ToString())
            .Should().BeEquivalentTo("a.x", "a.y.[0]", "a.y.[1]", "b");
    }

    [Fact]
    public void Should_Merge_A_Value_Genuinely_Changed_To_Null()
    {
        // This is why a list diff is keyed by index. With positional padding the
        // null at slot 0 (meaning "unchanged") would be indistinguishable from
        // slot 1's real change to null, and merge would have to guess.
        var previous = Map.Of("xs", List.Of(1, 2));
        var next = Map.Of("xs", List.Of(1, DataNull.Instance));

        var diff = _.DiffObjects(previous, next);
        ShouldEqual(diff, Map.Of("xs", Map.Of("1", DataNull.Instance)));

        ShouldEqual(_.Merge(previous, diff), next);
    }

    [Fact]
    public void Should_Merge_A_Diff_Back_Onto_Its_Source()
    {
        var previous = Map.Of("a", Map.Of("x", 1, "y", List.Of(2, 3)));
        var next = Map.Of("a", Map.Of("x", 9, "y", List.Of(2, 30)));

        ShouldEqual(_.Merge(previous, _.DiffObjects(previous, next)), next);
    }

    [Fact]
    public void Should_Fast_Forward_When_Nothing_Was_Committed_In_Between()
    {
        var previous = Map.Of("a", 1);
        var next = Map.Of("a", 2);

        ShouldEqual(SystemConsistency.Reconcile(previous, previous, next), next);
    }

    [Fact]
    public void Should_Merge_Concurrent_Changes_To_Different_Places()
    {
        var state = new SystemState(Map.Of("catalog", Map.Of("a", 1, "b", 2)));
        var start = state.Get();

        // Two mutations calculated from the same version, touching different keys.
        var next1 = _.Set(start, ["catalog", "a"], 10);
        var next2 = _.Set(start, ["catalog", "b"], 20);

        state.Commit(start, next1);
        state.Commit(start, next2); // reconciled against the first

        ShouldEqual(state.Get(), Map.Of("catalog", Map.Of("a", 10, "b", 20)));
    }

    [Fact]
    public void Should_Reject_Concurrent_Changes_To_The_Same_Place()
    {
        var state = new SystemState(Map.Of("catalog", Map.Of("a", 1)));
        var start = state.Get();

        state.Commit(start, _.Set(start, ["catalog", "a"], 10));

        var secondCommit = () => state.Commit(start, _.Set(start, ["catalog", "a"], 20));

        secondCommit.Should().Throw<ConcurrentModificationException>()
            .Which.ConflictingPaths.Single().ToString().Should().Be("catalog.a");

        // The rejected commit left the state alone.
        ShouldEqual(state.Get(), Map.Of("catalog", Map.Of("a", 10)));
    }

    [Fact]
    public void Should_Reconcile_The_Chapter_Five_Scenario()
    {
        // The DiffyLoop scenario: next changed the publication year while current
        // changed the title and an author name. Different places, so both survive.
        var previous = Map.Of(
            "booksByIsbn", Map.Of("978-1779501127", Map.Of("title", "Watchmen", "publicationYear", 1987)),
            "authorsById", Map.Of("dave-gibbons", Map.Of("name", "Dave Gibbons")));

        var next = _.Set(previous, ["booksByIsbn", "978-1779501127", "publicationYear"], 1986);

        var current = _.Set(
            _.Set(previous, ["booksByIsbn", "978-1779501127", "title"], "The Watchmen"),
            ["authorsById", "dave-gibbons", "name"], "David Chester Gibbons");

        ShouldEqual(SystemConsistency.Reconcile(current, previous, next), Map.Of(
            "booksByIsbn", Map.Of("978-1779501127", Map.Of("title", "The Watchmen", "publicationYear", 1986)),
            "authorsById", Map.Of("dave-gibbons", Map.Of("name", "David Chester Gibbons"))));
    }

    [Fact]
    public void Should_Not_Lose_Updates_Under_Parallel_Commits()
    {
        const int workers = 16;
        var state = new SystemState(Map.Of("counters", Map.Of()));

        Parallel.For(0, workers, i =>
            state.Update(current => _.Set(current, ["counters", $"w{i}"], i)));

        var counters = _.Get<DataMap>(state.Get(), "counters");

        counters.Count.Should().Be(workers);
        for (var i = 0; i < workers; i++)
            _.Get<long>(counters, $"w{i}").Should().Be(i);
    }

    [Fact]
    public void Should_Accept_The_Real_Library_Data()
    {
        Validation.Validate(Schemas.LibraryData, Library.LibraryData).Errors()
            .Should().BeEmpty();

        Schemas.ValidateCatalog(_.Get<DataMap>(Library.LibraryData, "catalog")).Errors()
            .Should().BeEmpty();
    }

    [Fact]
    public void Should_Report_Every_Error_With_Its_Path()
    {
        var book = Map.Of(
            "isbn", "nope",
            "title", "",
            "publicationYear", 3000,
            "authorIds", List.Of());

        var errors = Validation.Validate(Schemas.Book, book).Errors()
            .Select(e => e.ToString()).ToList();

        // Every problem, not just the first.
        errors.Should().HaveCount(4);
        errors.Should().Contain(e => e.StartsWith("isbn:") && e.Contains("must match"));
        errors.Should().Contain(e => e.StartsWith("title:") && e.Contains("at least 1 characters"));
        errors.Should().Contain(e => e.StartsWith("publicationYear:") && e.Contains("at most 2100"));
        errors.Should().Contain(e => e.StartsWith("authorIds:") && e.Contains("at least 1 items"));
    }

    [Fact]
    public void Should_Report_A_Missing_Required_Field()
    {
        var errors = Validation.Validate(Schemas.Book, Map.Of("title", "Watchmen")).Errors();

        errors.Select(e => e.ToString()).Should().BeEquivalentTo(
            "isbn: is required but missing",
            "authorIds: is required but missing");
    }

    [Fact]
    public void Should_Path_Errors_Through_Nested_Structures()
    {
        var book = Map.Of(
            "isbn", "978-1779501127",
            "title", "Watchmen",
            "authorIds", List.Of("alan-moore"),
            "bookItems", List.Of(
                Map.Of("id", "book-item-1", "libId", "nyc", "isLent", false),
                Map.Of("id", "book-item-2", "libId", "nyc", "isLent", "no")));

        Validation.Validate(Schemas.Book, book).Errors().Single().ToString()
            .Should().Be("bookItems.[1].isLent: expected boolean, but found string");
    }

    [Fact]
    public void Should_Path_Errors_Through_Id_Keyed_Collections()
    {
        var catalog = Map.Of(
            "booksByIsbn", Map.Of(
                "978-1779501127", Map.Of(
                    "isbn", "978-1779501127",
                    "title", "Watchmen",
                    "authorIds", List.Of("alan-moore", "alan-moore"))),
            "authorsById", Map.Of());

        Schemas.ValidateCatalog(catalog).Errors().Single().ToString()
            .Should().Be("booksByIsbn.978-1779501127.authorIds: must not contain duplicates");
    }

    [Fact]
    public void Should_Reject_Properties_The_Schema_Does_Not_Name()
    {
        var author = Map.Of(
            "name", "Alan Moore",
            "bookIsbns", List.Of("978-1779501127"),
            "favouriteColour", "black");

        Schemas.ValidateCatalog(Map.Of(
                "booksByIsbn", Map.Of(),
                "authorsById", Map.Of("alan-moore", author)))
            .Errors().Single().ToString()
            .Should().Be("authorsById.alan-moore.favouriteColour: is not a permitted property");
    }

    [Fact]
    public void Should_Ignore_Keywords_That_Do_Not_Apply_To_The_Value()
    {
        // minimum says nothing about a string, as in JSON Schema.
        var schema = Map.Of("minimum", 10, "minLength", 2);

        Validation.Validate(schema, "ab").IsValid().Should().BeTrue();
        Validation.Validate(schema, 20).IsValid().Should().BeTrue();
        Validation.Validate(schema, 5).IsValid().Should().BeFalse();
        Validation.Validate(schema, "a").IsValid().Should().BeFalse();
    }

    [Fact]
    public void Should_Support_Unions_Of_Types_And_Schemas()
    {
        var nullableIsbn = Map.Of("type", List.Of("string", "null"));

        Validation.Validate(nullableIsbn, "978-1779501127").IsValid().Should().BeTrue();
        Validation.Validate(nullableIsbn, DataNull.Instance).IsValid().Should().BeTrue();
        Validation.Validate(nullableIsbn, 1987).IsValid().Should().BeFalse();

        var stringOrCount = Map.Of("anyOf", List.Of(
            Map.Of("type", "string"),
            Map.Of("type", "integer", "minimum", 0)));

        Validation.Validate(stringOrCount, "x").IsValid().Should().BeTrue();
        Validation.Validate(stringOrCount, 3).IsValid().Should().BeTrue();
        Validation.Validate(stringOrCount, -1).IsValid().Should().BeFalse();
    }

    [Fact]
    public void Should_Validate_A_Request_At_The_Boundary()
    {
        var request = Map.Of("title", "Watchmen", "fields", List.Of("title", "isbn"));

        Library.SearchBooksJson(Library.LibraryData, request)
            .Should().Be("""[{"title":"Watchmen","isbn":"978-1779501127"}]""");
    }

    [Fact]
    public void Should_Reject_A_Malformed_Request_At_The_Boundary()
    {
        var badRequest = Map.Of("title", "", "fields", List.Of("title", "publisher"));

        var search = () => Library.SearchBooksJson(Library.LibraryData, badRequest);

        search.Should().Throw<SchemaViolationException>()
            .Which.Errors.Select(e => e.ToString()).Should().BeEquivalentTo(
                "title: must be at least 1 characters, but was 0",
                "fields.[1]: must be one of [\"title\",\"isbn\",\"authorNames\"], but was \"publisher\"");
    }

    [Fact]
    public void Should_Treat_A_Schema_As_Data()
    {
        // The point of principle 4: a schema is a value. It can be built from parts,
        // diffed, and changed without touching a type.
        var strict = _.Set(Schemas.Book, ["properties", "title", "minLength"], 5).As<DataMap>();

        Validation.Validate(Schemas.Book, Map.Of(
            "isbn", "978-1779501127", "title", "Wat", "authorIds", List.Of("a"))).IsValid()
            .Should().BeTrue();

        Validation.Validate(strict, Map.Of(
            "isbn", "978-1779501127", "title", "Wat", "authorIds", List.Of("a"))).IsValid()
            .Should().BeFalse();

        ShouldEqual(
            _.DiffObjects(Schemas.Book, strict),
            Map.Of("properties", Map.Of("title", Map.Of("minLength", 5))));
    }

    private static DataMap Users => _.Get<DataMap>(Library.LibraryData, "userManagementData");

    [Fact]
    public void Should_Identify_Roles()
    {
        UserManagement.IsLibrarian(Users, "franck@gmail.com").Should().BeTrue();
        UserManagement.IsLibrarian(Users, "samantha@gmail.com").Should().BeFalse();
        UserManagement.IsLibrarian(Users, "nobody@gmail.com").Should().BeFalse();

        UserManagement.IsMember(Users, "samantha@gmail.com").Should().BeTrue();
        UserManagement.IsMember(Users, "franck@gmail.com").Should().BeFalse();

        UserManagement.IsSuperMember(Users, "samantha@gmail.com").Should().BeTrue();
        UserManagement.IsSuperMember(Users, "vip@gmail.com").Should().BeFalse();

        UserManagement.IsVipMember(Users, "vip@gmail.com").Should().BeTrue();
        UserManagement.IsVipMember(Users, "samantha@gmail.com").Should().BeFalse();
    }

    [Fact]
    public void Should_Treat_An_Absent_Flag_As_False()
    {
        // vip@gmail.com carries no isSuper or isBlocked at all.
        UserManagement.IsSuperMember(Users, "vip@gmail.com").Should().BeFalse();
        UserManagement.IsBlocked(Users, "vip@gmail.com").Should().BeFalse();

        // Neither does a user who does not exist.
        UserManagement.IsVipMember(Users, "nobody@gmail.com").Should().BeFalse();
        UserManagement.IsBlocked(Users, "nobody@gmail.com").Should().BeFalse();
    }

    [Fact]
    public void Should_Authenticate_Against_A_Hashed_Password()
    {
        UserManagement.Authenticate(Users, "samantha@gmail.com", "member-secret").Should().BeTrue();
        UserManagement.Authenticate(Users, "franck@gmail.com", "librarian-secret").Should().BeTrue();

        UserManagement.Authenticate(Users, "samantha@gmail.com", "wrong").Should().BeFalse();
        UserManagement.Authenticate(Users, "nobody@gmail.com", "member-secret").Should().BeFalse();
        UserManagement.Authenticate(Users, "samantha@gmail.com", "").Should().BeFalse();
    }

    [Fact]
    public void Should_Not_Store_The_Password_Itself()
    {
        var stored = _.Get<DataMap>(Users, ["members", "samantha@gmail.com", "password"]);

        DataJson.Serialize(stored).Should().NotContain("member-secret");
        stored.Keys.Should().BeEquivalentTo("salt", "hash", "iterations");

        // Two users with the same password get different hashes, because the salts differ.
        var one = Passwords.Hash("same-password", 1000);
        var two = Passwords.Hash("same-password", 1000);

        _.Get<string>(one, "hash").Should().NotBe(_.Get<string>(two, "hash"));
        Passwords.Verify(one, "same-password").Should().BeTrue();
        Passwords.Verify(two, "same-password").Should().BeTrue();
        Passwords.Verify(one, "different").Should().BeFalse();
    }

    [Fact]
    public void Should_Refuse_A_Blocked_Member()
    {
        var blocked = UserManagement.BlockMember(Users, "samantha@gmail.com");

        UserManagement.IsBlocked(blocked, "samantha@gmail.com").Should().BeTrue();
        UserManagement.Authenticate(blocked, "samantha@gmail.com", "member-secret").Should().BeFalse();

        var unblocked = UserManagement.UnblockMember(blocked, "samantha@gmail.com");
        UserManagement.Authenticate(unblocked, "samantha@gmail.com", "member-secret").Should().BeTrue();

        // The original data is untouched.
        UserManagement.IsBlocked(Users, "samantha@gmail.com").Should().BeFalse();
    }

    [Fact]
    public void Should_Add_A_Member()
    {
        var member = Map.Of(
            "email", "new@gmail.com",
            "password", Passwords.Hash("new-secret", 1000));

        var updated = UserManagement.AddMember(Users, member);

        UserManagement.IsMember(updated, "new@gmail.com").Should().BeTrue();
        UserManagement.Authenticate(updated, "new@gmail.com", "new-secret").Should().BeTrue();

        // The argument is untouched.
        UserManagement.IsMember(Users, "new@gmail.com").Should().BeFalse();
    }

    [Fact]
    public void Should_Reject_A_Member_That_Does_Not_Match_The_Schema()
    {
        var noPassword = Map.Of("email", "new@gmail.com");
        var badEmail = Map.Of("email", "not-an-email", "password", Passwords.Hash("x", 1000));

        var addNoPassword = () => UserManagement.AddMember(Users, noPassword);
        addNoPassword.Should().Throw<SchemaViolationException>()
            .Which.Errors.Single().ToString().Should().Be("password: is required but missing");

        var addBadEmail = () => UserManagement.AddMember(Users, badEmail);
        addBadEmail.Should().Throw<SchemaViolationException>()
            .Which.Errors.Single().Path.ToString().Should().Be("email");
    }

    [Fact]
    public void Should_Reject_A_Duplicate_User()
    {
        var existing = Map.Of(
            "email", "samantha@gmail.com",
            "password", Passwords.Hash("another", 1000));

        var addAgain = () => UserManagement.AddMember(Users, existing);
        addAgain.Should().Throw<DuplicateUserException>();

        // Across collections too: an id taken by a librarian is not free for a member.
        var asMember = Map.Of(
            "email", "franck@gmail.com",
            "password", Passwords.Hash("another", 1000));

        var addLibrarianAsMember = () => UserManagement.AddMember(Users, asMember);
        addLibrarianAsMember.Should().Throw<DuplicateUserException>();
    }

    [Fact]
    public void Should_Read_Book_Lendings()
    {
        ShouldEqual(
            UserManagement.BookLendings(Users, "samantha@gmail.com"),
            List.Of(Map.Of(
                "bookItemId", "book-item-1",
                "bookIsbn", "978-1779501127",
                "lendingDate", "2020-04-23")));

        // A member with no lendings recorded gets an empty list, not an error.
        UserManagement.BookLendings(Users, "vip@gmail.com").Should().BeEmpty();

        var unknown = () => UserManagement.BookLendings(Users, "nobody@gmail.com");
        unknown.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Should_Accept_The_Seeded_User_Management_Data()
    {
        Schemas.ValidateUserManagement(Users).Errors().Should().BeEmpty();
    }

    [Fact]
    public void Should_Refuse_Library_Operations_To_Unauthorised_Users()
    {
        // vip@gmail.com is neither a librarian nor a super member.
        var getLendings = () => Library.GetBookLendings(Library.LibraryData, "vip@gmail.com", "samantha@gmail.com");
        getLendings.Should().Throw<Exception>().WithMessage("Not allowed to get book lendings");

        // samantha is a super member, but not a VIP.
        var addItem = () => Library.AddBookItem(Library.LibraryData, "samantha@gmail.com", Map.Of());
        addItem.Should().Throw<Exception>().WithMessage("Not allowed to add book items");

        var unknownUser = () => Library.AddBookItem(Library.LibraryData, "nobody@gmail.com", Map.Of());
        unknownUser.Should().Throw<Exception>().WithMessage("Not allowed to add book items");
    }

    private static DataMap CatalogFixture => Map.Of(
        "booksByIsbn", Map.Of(
            "978-1779501127", Map.Of(
                "isbn", "978-1779501127",
                "title", "Watchmen",
                "publicationYear", 1987,
                "authorIds", List.Of("alan-moore")),
            "978-1982137274", Map.Of(
                "isbn", "978-1982137274",
                "title", "7 Habits of Highly Effective People",
                "publicationYear", 2020,
                "authorIds", List.Of("stephen-covey"))),
        "authorsById", Map.Of(
            "alan-moore", Map.Of("name", "Alan Moore", "bookIsbns", List.Of("978-1779501127")),
            "stephen-covey", Map.Of("name", "Stephen Covey", "bookIsbns", List.Of("978-1982137274"))));

    private static IEnumerable<string> TitlesOf(DataList results) =>
        results.Select(r => _.Get<string>(r, "title"));

    [Fact]
    public void Should_Search_On_Combined_Criteria()
    {
        // An empty query matches everything.
        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of()))
            .Should().BeEquivalentTo("Watchmen", "7 Habits of Highly Effective People");

        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of("author", "Moore")))
            .Should().Equal("Watchmen");

        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of("publishedAfter", 2000)))
            .Should().Equal("7 Habits of Highly Effective People");

        // Criteria combine with AND.
        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of("title", "Habits", "publishedBefore", 2000)))
            .Should().BeEmpty();

        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of("title", "Habits", "publishedAfter", 2000)))
            .Should().Equal("7 Habits of Highly Effective People");

        // Bounds are inclusive.
        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of("publishedAfter", 1987, "publishedBefore", 1987)))
            .Should().Equal("Watchmen");
    }

    [Fact]
    public void Should_Search_Case_Insensitively()
    {
        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of("title", "watchMEN")))
            .Should().Equal("Watchmen");

        TitlesOf(Catalog.SearchBook(CatalogFixture, Map.Of("author", "moore")))
            .Should().Equal("Watchmen");

        TitlesOf(Catalog.SearchBooksByTitle(CatalogFixture, "WATCH"))
            .Should().Equal("Watchmen");
    }

    [Fact]
    public void Should_Reject_A_Search_Criterion_The_Schema_Does_Not_Name()
    {
        var search = () => Catalog.SearchBook(CatalogFixture, Map.Of("publisher", "DC"));

        search.Should().Throw<SchemaViolationException>()
            .Which.Errors.Single().ToString().Should().Be("publisher: is not a permitted property");
    }

    [Fact]
    public void Should_Describe_A_Members_Lendings()
    {
        var lendings = UserManagement.BookLendings(Users, "samantha@gmail.com");
        var catalog = _.Get<DataMap>(Library.LibraryData, "catalog");

        ShouldEqual(
            Catalog.GetBookLendings(catalog, lendings),
            List.Of(Map.Of(
                "bookItemId", "book-item-1",
                "lendingDate", "2020-04-23",
                "title", "Watchmen",
                "isbn", "978-1779501127",
                "authorNames", List.Of("Alan Moore", "Dave Gibbons"))));
    }

    [Fact]
    public void Should_Reject_A_Lending_For_A_Book_Not_In_The_Catalogue()
    {
        var orphan = List.Of(Map.Of(
            "bookItemId", "book-item-9",
            "bookIsbn", "978-0000000000",
            "lendingDate", "2020-04-23"));

        var describe = () => Catalog.GetBookLendings(CatalogFixture, orphan);

        describe.Should().Throw<KeyNotFoundException>()
            .WithMessage("*978-0000000000*");
    }

    [Fact]
    public void Should_Add_A_Book_Item()
    {
        var info = Map.Of("isbn", "978-1779501127", "id", "book-item-3", "libId", "brooklyn-lib");

        var updated = Catalog.AddBookItem(CatalogFixture, info);

        ShouldEqual(
            _.Get<DataList>(updated, ["booksByIsbn", "978-1779501127", "bookItems"]),
            List.Of(Map.Of("id", "book-item-3", "libId", "brooklyn-lib", "isLent", false)));

        // A new item is not lent, and isLent cannot be supplied.
        var withIsLent = Map.Of(
            "isbn", "978-1779501127", "id", "book-item-4", "libId", "brooklyn-lib", "isLent", true);

        var addWithIsLent = () => Catalog.AddBookItem(CatalogFixture, withIsLent);
        addWithIsLent.Should().Throw<SchemaViolationException>()
            .Which.Errors.Single().ToString().Should().Be("isLent: is not a permitted property");

        // The argument is untouched.
        _.ContainsKey(CatalogFixture, ["booksByIsbn", "978-1779501127", "bookItems"]).Should().BeFalse();
    }

    [Fact]
    public void Should_Reject_A_Duplicate_Book_Item()
    {
        var catalog = _.Get<DataMap>(Library.LibraryData, "catalog");
        var taken = Map.Of("isbn", "978-1779501127", "id", "book-item-1", "libId", "nyc-central-lib");

        var add = () => Catalog.AddBookItem(catalog, taken);
        add.Should().Throw<DuplicateBookItemException>();
    }

    [Fact]
    public void Should_Reject_A_Book_Item_For_An_Unknown_Book()
    {
        var info = Map.Of("isbn", "978-0000000000", "id", "book-item-3", "libId", "brooklyn-lib");

        var add = () => Catalog.AddBookItem(CatalogFixture, info);
        add.Should().Throw<KeyNotFoundException>().WithMessage("*978-0000000000*");
    }

    [Fact]
    public void Should_Get_Lendings_End_To_End()
    {
        // A librarian may read another member's lendings.
        TitlesOf(Library.GetBookLendings(Library.LibraryData, "franck@gmail.com", "samantha@gmail.com"))
            .Should().Equal("Watchmen");

        // So may a super member.
        TitlesOf(Library.GetBookLendings(Library.LibraryData, "samantha@gmail.com", "samantha@gmail.com"))
            .Should().Equal("Watchmen");

        // A member with no lendings gets an empty list.
        Library.GetBookLendings(Library.LibraryData, "franck@gmail.com", "vip@gmail.com")
            .Should().BeEmpty();
    }

    [Fact]
    public void Should_Add_A_Book_Item_End_To_End()
    {
        var info = Map.Of("isbn", "978-1779501127", "id", "book-item-3", "libId", "brooklyn-lib");

        var updated = Library.AddBookItem(Library.LibraryData, "vip@gmail.com", info);

        var items = _.Get<DataList>(updated, ["catalog", "booksByIsbn", "978-1779501127", "bookItems"]);
        items.Select(i => _.Get<string>(i, "id"))
            .Should().Equal("book-item-1", "book-item-2", "book-item-3");

        // The whole library data comes back, so user management survives the change.
        UserManagement.IsLibrarian(_.Get<DataMap>(updated, "userManagementData"), "franck@gmail.com")
            .Should().BeTrue();

        // The only difference is the new item.
        ShouldEqual(
            _.DiffObjects(Library.LibraryData, updated),
            Map.Of("catalog", Map.Of("booksByIsbn", Map.Of("978-1779501127", Map.Of(
                "bookItems", Map.Of("2", Map.Of(
                    "id", "book-item-3", "libId", "brooklyn-lib", "isLent", false)))))));
    }

    private static DataMap TwoBookLibrary => _.Set(
        Library.LibraryData,
        ["catalog", "booksByIsbn", "978-1982137274"],
        Map.Of(
            "isbn", "978-1982137274",
            "title", "7 Habits of Highly Effective People",
            "publicationYear", 2020,
            "authorIds", List.Of("alan-moore")));

    [Fact]
    public void Should_Not_Contend_Across_Aggregates()
    {
        var state = new SystemState(TwoBookLibrary);
        var start = state.Get();

        var watchmen = Aggregates.Book("978-1779501127");
        var habits = Aggregates.Book("978-1982137274");

        // Two changes calculated from the same version, in different aggregates.
        var newWatchmen = _.Set(_.Get(start, watchmen), "publicationYear", 1986);
        var newHabits = _.Set(_.Get(start, habits), "publicationYear", 2021);

        state.Commit(watchmen, _.Get(start, watchmen), newWatchmen);
        state.Commit(habits, _.Get(start, habits), newHabits);

        var after = state.Get();
        _.Get<long>(after, [.. watchmen, "publicationYear"]).Should().Be(1986);
        _.Get<long>(after, [.. habits, "publicationYear"]).Should().Be(2021);
    }

    [Fact]
    public void Should_Merge_Disjoint_Changes_Within_One_Aggregate()
    {
        var state = new SystemState(Library.LibraryData);
        var book = Aggregates.Book("978-1779501127");
        var start = _.Get(state.Get(), book);

        state.Commit(book, start, _.Set(start, "publicationYear", 1986));
        state.Commit(book, start, _.Set(start, "title", "The Watchmen"));

        var after = _.Get(state.Get(), book);
        _.Get<long>(after, "publicationYear").Should().Be(1986);
        _.Get<string>(after, "title").Should().Be("The Watchmen");
    }

    [Fact]
    public void Should_Still_Conflict_Within_One_Aggregate()
    {
        var state = new SystemState(Library.LibraryData);
        var book = Aggregates.Book("978-1779501127");
        var start = _.Get(state.Get(), book);

        state.Commit(book, start, _.Set(start, "title", "The Watchmen"));

        var second = () => state.Commit(book, start, _.Set(start, "title", "Watchmen!"));

        second.Should().Throw<ConcurrentModificationException>()
            .Which.ConflictingPaths.Single().ToString().Should().Be("title");
    }

    [Fact]
    public void Should_Commit_An_Aggregate_That_Does_Not_Exist_Yet()
    {
        var state = new SystemState(Library.LibraryData);
        var newBook = Aggregates.Book("978-0000000001");

        // Nothing there yet, so the previous value is null rather than an error.
        (state.Read(newBook) is DataNull).Should().BeTrue();

        state.Commit(newBook, DataNull.Instance, Map.Of(
            "isbn", "978-0000000001", "title", "New", "authorIds", List.Of("alan-moore")));

        _.Get<string>(state.Get(), [.. newBook, "title"]).Should().Be("New");
    }

    [Fact]
    public void Should_Read_Only_The_Aggregate_A_Caller_Needs()
    {
        var state = new SystemState(Library.LibraryData);

        // A caller changing a book never has to hold the whole system -- which is
        // the property that makes this work over a network.
        var book = state.Read(Aggregates.Book("978-1779501127")).As<DataMap>();

        book.Keys.Should().Contain("title");
        book.ContainsKey("userManagementData").Should().BeFalse();

        state.Commit(Aggregates.Book("978-1779501127"), book, _.Set(book, "publicationYear", 1986));

        _.Get<long>(state.Get(), ["catalog", "booksByIsbn", "978-1779501127", "publicationYear"])
            .Should().Be(1986);
    }

    [Fact]
    public void Should_Scope_Library_Writes_To_One_Aggregate()
    {
        var system = new LibrarySystem(Library.LibraryData);

        // The caller gets back the aggregate it changed, not the system.
        var book = system.AddBookItem("franck@gmail.com", Map.Of(
            "isbn", "978-1779501127", "id", "book-item-3", "libId", "brooklyn-lib"));

        _.Get<DataList>(book, "bookItems").Select(i => _.Get<string>(i, "id"))
            .Should().Equal("book-item-1", "book-item-2", "book-item-3");

        // And nothing else in the system moved.
        ShouldEqual(
            _.DiffObjects(Library.LibraryData, system.Snapshot()),
            Map.Of("catalog", Map.Of("booksByIsbn", Map.Of("978-1779501127", Map.Of(
                "bookItems", Map.Of("2", Map.Of(
                    "id", "book-item-3", "libId", "brooklyn-lib", "isLent", false)))))));
    }

    // ---- IAggregateStore: the two implementations must agree ----

    public static TheoryData<string> StoreKinds => new() { "snapshot", "diff-indexed" };

    private static IAggregateStore StoreOf(string kind, DataMap initial) =>
        kind switch
        {
            "snapshot" => new SnapshotAggregateStore(initial),
            "diff-indexed" => new DiffIndexedStore(initial),
            _ => throw new ArgumentException(kind)
        };

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Read_An_Aggregate_With_Its_Version(string kind)
    {
        var store = StoreOf(kind, Library.LibraryData);
        var book = Aggregates.Book("978-1779501127");

        var (value, version) = store.Read(book);

        version.Should().Be(0);
        _.Get<string>(value, "title").Should().Be("Watchmen");

        // An aggregate that is not there reads as null at version 0, so creating one
        // needs no separate code path.
        var (missing, missingVersion) = store.Read(Aggregates.Book("978-0000000000"));
        (missing is DataNull).Should().BeTrue();
        missingVersion.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Apply_A_Diff_And_Advance_The_Version(string kind)
    {
        var store = StoreOf(kind, Library.LibraryData);
        var book = Aggregates.Book("978-1779501127");

        var (before, version) = store.Read(book);
        var after = _.Set(before, "publicationYear", 1986);

        var committed = store.Commit(book, version, _.DiffObjects(before, after));

        committed.Version.Should().Be(1);
        _.Get<long>(committed.Value, "publicationYear").Should().Be(1986);
        store.Read(book).Version.Should().Be(1);

        // The title was never in the diff, so it survived.
        _.Get<string>(store.Read(book).Value, "title").Should().Be("Watchmen");
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Merge_Two_Writers_On_Different_Paths(string kind)
    {
        var store = StoreOf(kind, Library.LibraryData);
        var book = Aggregates.Book("978-1779501127");

        // Both read the same version, then write.
        var (start, version) = store.Read(book);

        store.Commit(book, version, _.DiffObjects(start, _.Set(start, "publicationYear", 1986)));
        var second = store.Commit(book, version, _.DiffObjects(start, _.Set(start, "title", "The Watchmen")));

        second.Version.Should().Be(2);
        _.Get<long>(second.Value, "publicationYear").Should().Be(1986);
        _.Get<string>(second.Value, "title").Should().Be("The Watchmen");
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Reject_Two_Writers_On_The_Same_Path(string kind)
    {
        var store = StoreOf(kind, Library.LibraryData);
        var book = Aggregates.Book("978-1779501127");
        var (start, version) = store.Read(book);

        store.Commit(book, version, _.DiffObjects(start, _.Set(start, "title", "The Watchmen")));

        var second = () => store.Commit(book, version, _.DiffObjects(start, _.Set(start, "title", "Watchmen!")));

        second.Should().Throw<ConcurrentModificationException>()
            .Which.ConflictingPaths.Single().ToString().Should().Be("title");

        // A rejected commit changes nothing.
        store.Read(book).Version.Should().Be(1);
        _.Get<string>(store.Read(book).Value, "title").Should().Be("The Watchmen");
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Keep_Aggregates_Independent(string kind)
    {
        var store = StoreOf(kind, Library.LibraryData);
        var book = Aggregates.Book("978-1779501127");
        var member = Aggregates.Member("samantha@gmail.com");

        var (bookValue, bookVersion) = store.Read(book);
        var (memberValue, memberVersion) = store.Read(member);

        store.Commit(book, bookVersion, _.DiffObjects(bookValue, _.Set(bookValue, "publicationYear", 1986)));

        // The member is untouched, so its version has not moved and a write against
        // the version read earlier still applies.
        store.Read(member).Version.Should().Be(memberVersion);
        store.Commit(member, memberVersion, _.DiffObjects(memberValue, _.Set(memberValue, "isBlocked", true)));

        _.Get<bool>(store.Read(member).Value, "isBlocked").Should().BeTrue();
        _.Get<long>(store.Read(book).Value, "publicationYear").Should().Be(1986);
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Create_An_Aggregate_That_Is_Not_There_Yet(string kind)
    {
        var store = StoreOf(kind, Library.LibraryData);
        var fresh = Aggregates.Book("978-0000000001");

        var (missing, version) = store.Read(fresh);
        var book = Map.Of("isbn", "978-0000000001", "title", "New", "authorIds", List.Of("alan-moore"));

        var created = store.Commit(fresh, version, _.DiffObjects(missing, book));

        created.Version.Should().Be(1);
        _.Get<string>(created.Value, "title").Should().Be("New");
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Run_LibrarySystem_Unchanged(string kind)
    {
        var system = new LibrarySystem(StoreOf(kind, Library.LibraryData));

        TitlesOf(system.GetBookLendings("franck@gmail.com", "samantha@gmail.com"))
            .Should().Equal("Watchmen");

        var book = system.AddBookItem("vip@gmail.com", Map.Of(
            "isbn", "978-1779501127", "id", "book-item-3", "libId", "brooklyn-lib"));
        _.Get<DataList>(book, "bookItems").Count.Should().Be(3);

        system.BlockMember("franck@gmail.com", "samantha@gmail.com");
        UserManagement.IsBlocked(
            _.Get<DataMap>(system.Snapshot(), "userManagementData"), "samantha@gmail.com")
            .Should().BeTrue();

        system.AddMember("franck@gmail.com", Map.Of(
            "email", "new@gmail.com", "password", Passwords.Hash("new-secret", 1000)));
        UserManagement.IsMember(
            _.Get<DataMap>(system.Snapshot(), "userManagementData"), "new@gmail.com")
            .Should().BeTrue();

        var notALibrarian = () => system.BlockMember("vip@gmail.com", "samantha@gmail.com");
        notALibrarian.Should().Throw<Exception>().WithMessage("Not allowed to block members");
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Survive_Parallel_Writes_To_Different_Aggregates(string kind)
    {
        const int books = 12;
        var seed = Enumerable.Range(0, books).Aggregate(Library.LibraryData,
            (data, i) => _.Set(data, ["catalog", "booksByIsbn", $"978-000000000{i}"], Map.Of(
                "isbn", $"978-000000000{i}", "title", $"Book {i}", "authorIds", List.Of("alan-moore"))));

        var system = new LibrarySystem(StoreOf(kind, seed));

        Parallel.For(0, books, i =>
            system.AddBookItem("franck@gmail.com", Map.Of(
                "isbn", $"978-000000000{i}", "id", $"item-{i}", "libId", "brooklyn-lib")));

        var final = system.Snapshot();
        for (var i = 0; i < books; i++)
            _.Get<DataList>(final, ["catalog", "booksByIsbn", $"978-000000000{i}", "bookItems"])
                .Select(item => _.Get<string>(item, "id")).Should().Equal($"item-{i}");
    }

    // ---- where the two implementations legitimately differ ----

    [Fact]
    public void Should_Reject_A_Client_Older_Than_The_Retained_Tail()
    {
        // Room for two changes only.
        var store = new DiffIndexedStore(Library.LibraryData, retainedChangesPerAggregate: 2);
        var book = Aggregates.Book("978-1779501127");

        var (start, ancientVersion) = store.Read(book);

        // Three unrelated writes push the client version off the end of the tail.
        var running = start;
        for (var i = 0; i < 3; i++)
        {
            var (value, version) = store.Read(book);
            running = _.Set(value, "publicationYear", 1900 + i);
            store.Commit(book, version, _.DiffObjects(value, running));
        }

        // The paths do not collide, but the store can no longer tell that.
        var late = () => store.Commit(book, ancientVersion, _.DiffObjects(start, _.Set(start, "title", "Late")));

        late.Should().Throw<StaleVersionException>()
            .Which.ClientVersion.Should().Be(ancientVersion);
    }

    [Fact]
    public void Should_Answer_An_Old_Client_When_Every_Version_Is_Retained()
    {
        // The same sequence against the store that keeps values: it can still work out
        // what moved, so a non-colliding late write is accepted rather than refused.
        var store = new SnapshotAggregateStore(Library.LibraryData);
        var book = Aggregates.Book("978-1779501127");

        var (start, ancientVersion) = store.Read(book);

        for (var i = 0; i < 3; i++)
        {
            var (value, version) = store.Read(book);
            store.Commit(book, version, _.DiffObjects(value, _.Set(value, "publicationYear", 1900 + i)));
        }

        var late = store.Commit(book, ancientVersion, _.DiffObjects(start, _.Set(start, "title", "Late")));

        _.Get<string>(late.Value, "title").Should().Be("Late");
        _.Get<long>(late.Value, "publicationYear").Should().Be(1902);
    }

    [Fact]
    public void Should_Keep_Reads_And_Writes_Consistent_Through_The_System_Layer()
    {
        var system = new LibrarySystem(Library.LibraryData);

        TitlesOf(system.GetBookLendings("franck@gmail.com", "samantha@gmail.com"))
            .Should().Equal("Watchmen");

        system.BlockMember("franck@gmail.com", "samantha@gmail.com");

        var users = _.Get<DataMap>(system.Snapshot(), "userManagementData");
        UserManagement.IsBlocked(users, "samantha@gmail.com").Should().BeTrue();

        // Permission checks still apply at the system layer.
        var notALibrarian = () => system.BlockMember("vip@gmail.com", "samantha@gmail.com");
        notALibrarian.Should().Throw<Exception>().WithMessage("Not allowed to block members");
    }

    [Fact]
    public void Should_Add_A_Member_Through_The_System_Layer()
    {
        var system = new LibrarySystem(Library.LibraryData);

        system.AddMember("franck@gmail.com", Map.Of(
            "email", "new@gmail.com",
            "password", Passwords.Hash("new-secret", 1000)));

        var users = _.Get<DataMap>(system.Snapshot(), "userManagementData");
        UserManagement.IsMember(users, "new@gmail.com").Should().BeTrue();

        // The seed value is untouched: the store holds the new version, not the old one.
        UserManagement.IsMember(
            _.Get<DataMap>(Library.LibraryData, "userManagementData"), "new@gmail.com")
            .Should().BeFalse();
    }

    [Fact]
    public void Should_Survive_Parallel_Writes_To_Different_Books()
    {
        const int books = 12;

        var seed = Enumerable.Range(0, books).Aggregate(
            Library.LibraryData,
            (data, i) => _.Set(data, ["catalog", "booksByIsbn", $"978-000000000{i}"], Map.Of(
                "isbn", $"978-000000000{i}",
                "title", $"Book {i}",
                "authorIds", List.Of("alan-moore"))));

        var system = new LibrarySystem(seed);

        Parallel.For(0, books, i =>
            system.AddBookItem("franck@gmail.com", Map.Of(
                "isbn", $"978-000000000{i}",
                "id", $"item-{i}",
                "libId", "brooklyn-lib")));

        var final = system.Snapshot();
        for (var i = 0; i < books; i++)
        {
            var items = _.Get<DataList>(final,
                ["catalog", "booksByIsbn", $"978-000000000{i}", "bookItems"]);
            items.Select(item => _.Get<string>(item, "id")).Should().Equal($"item-{i}");
        }
    }

    [Fact]
    public void Should_Merge_A_Diff_That_Introduces_A_Path()
    {
        // Reachable whenever two people edit one book and only one of them adds the
        // first copy: the paths are disjoint, the merge is valid, and the descent
        // walks through a key the target does not have.
        var previous = Map.Of("book", Map.Of("title", "Watchmen"));
        var next = Map.Of("book", Map.Of("title", "Watchmen", "items", List.Of(Map.Of("id", "a"))));

        ShouldEqual(_.Merge(previous, _.DiffObjects(previous, next)), next);
    }

    [Fact]
    public void Should_Create_Missing_Containers_When_Setting_A_Path()
    {
        // The following step decides the container: a name makes a map, an index a list.
        ShouldEqual(
            _.Set(Map.Of(), ["a", "b"], 1),
            Map.Of("a", Map.Of("b", 1)));

        ShouldEqual(
            _.Set(Map.Of(), ["a", 0, "b"], 1),
            Map.Of("a", List.Of(Map.Of("b", 1))));

        // A null stands in for absent, so a diff can fill one in.
        ShouldEqual(
            _.Set(Map.Of("a", DataNull.Instance), ["a", "b"], 1),
            Map.Of("a", Map.Of("b", 1)));

        // A leaf part-way along is still an error, not something to overwrite.
        var throughALeaf = () => _.Set(Map.Of("a", "leaf"), ["a", "b"], 1);
        throughALeaf.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_Diff_From_Nothing_And_Merge_Into_Nothing()
    {
        // Creating a value is the same operation as changing one, so a store needs no
        // separate path for an aggregate that does not exist yet.
        var book = Map.Of("isbn", "978-1779501127", "title", "Watchmen");

        var born = _.DiffObjects(DataNull.Instance, book);
        ShouldEqual(born, Map.Of("isbn", "978-1779501127", "title", "Watchmen"));
        ShouldEqual(_.Merge(DataNull.Instance, born).As<DataMap>(), book);

        // And the reverse reads as every key removed.
        ShouldEqual(
            _.DiffObjects(book, DataNull.Instance),
            Map.Of("isbn", DataNull.Instance, "title", DataNull.Instance));

        // A list cannot be rebuilt from its diff alone. Diffs are always maps, with
        // indices as string keys, so the root container type is gone by then --
        // creating a list-rooted aggregate needs the value, not the difference.
        var list = List.Of("a", "b");
        ShouldEqual(
            _.Merge(DataNull.Instance, _.DiffObjects(DataNull.Instance, list)).As<DataMap>(),
            Map.Of("0", "a", "1", "b"));
    }

    [Fact]
    public void Should_Treat_A_Path_And_Its_Ancestor_As_Overlapping()
    {
        DataPath.Of("items").Overlaps(DataPath.Of("items", 1)).Should().BeTrue();
        DataPath.Of("items", 1).Overlaps(DataPath.Of("items")).Should().BeTrue();
        DataPath.Of("items", 1).Overlaps(DataPath.Of("items", 1)).Should().BeTrue();

        // Siblings are independent, and so are unrelated branches.
        DataPath.Of("items", 0).Overlaps(DataPath.Of("items", 1)).Should().BeFalse();
        DataPath.Of("items").Overlaps(DataPath.Of("title")).Should().BeFalse();

        // The root contains everything.
        DataPath.Root.Overlaps(DataPath.Of("items", 1)).Should().BeTrue();
    }

    [Fact]
    public void Should_Conflict_When_One_Writer_Replaces_What_Another_Reaches_Into()
    {
        var start = Map.Of("items", List.Of("a", "b"));

        // One writer drops the whole list, the other edits one element. The paths are
        // not equal, but they are not independent: exact intersection let both
        // through and the merge then produced a map where a list should be.
        var wholesale = _.DiffObjects(start, Map.Of("items", DataNull.Instance));
        var element = _.DiffObjects(start, Map.Of("items", List.Of("a", "c")));

        // Rendered "items.1", not "items.[1]": inside a diff an index is a string key.
        SystemConsistency.CommonPaths(wholesale, element)
            .Select(p => p.ToString()).Should().Equal("items.1");

        SystemConsistency.CommonPaths(element, wholesale)
            .Select(p => p.ToString()).Should().Equal("items");
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Reject_A_Write_Into_Something_Another_Writer_Replaced(string kind)
    {
        var store = StoreOf(kind, Map.Of("book", Map.Of("items", List.Of("a", "b"))));
        var book = DataPath.Of("book");

        var (start, version) = store.Read(book);

        // Writer 1 drops the list entirely.
        store.Commit(book, version, _.DiffObjects(start, _.Set(start, "items", DataNull.Instance)));

        // Writer 2 still holds the old version and edits inside that list.
        var edit = () => store.Commit(book, version,
            _.DiffObjects(start, _.Set(start, ["items", 1], "c")));

        edit.Should().Throw<ConcurrentModificationException>();

        // The list stayed dropped rather than being resurrected as a map.
        (_.Get(store.Read(book).Value, "items") is DataNull).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Should_Still_Merge_Writers_On_Sibling_Elements(string kind)
    {
        // The prefix rule must not make independent edits collide.
        var store = StoreOf(kind, Map.Of("book", Map.Of("items", List.Of("a", "b"))));
        var book = DataPath.Of("book");

        var (start, version) = store.Read(book);

        store.Commit(book, version, _.DiffObjects(start, _.Set(start, ["items", 0], "A")));
        var second = store.Commit(book, version, _.DiffObjects(start, _.Set(start, ["items", 1], "B")));

        ShouldEqual(_.Get(second.Value, "items").As<DataList>(), List.Of("A", "B"));
    }

    [Fact]
    public void Should_Treat_An_Empty_Diff_As_Touching_Nothing()
    {
        // InformationPaths reports the root of an empty map, because setting a field
        // to {} really is a change. A diff is different: empty means nothing moved,
        // and the root would otherwise be a prefix of every concurrent write.
        _.InformationPaths(Map.Of()).Select(p => p.ToString()).Should().Equal("(root)");
        _.ChangedPaths(Map.Of()).Should().BeEmpty();

        var busy = _.DiffObjects(Map.Of("a", 1), Map.Of("a", 2));
        SystemConsistency.CommonPaths(Map.Of(), busy).Should().BeEmpty();
        SystemConsistency.CommonPaths(busy, Map.Of()).Should().BeEmpty();
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
