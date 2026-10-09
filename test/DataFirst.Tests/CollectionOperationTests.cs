using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Database;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Tests;

public sealed class CollectionOperationTests
{
    private static readonly DataList authorsListMap = List.Of(
        Map.Of(("isbn", "978-1982137274"),
            ("title", "7 Habits of Highly Effective People"),
            ("author_name", "Steven Clarey")),
        Map.Of(("isbn", "978-1982137274"),
            ("title", "7 Habits of Highly Effective People"),
            ("author_name", "Tom Jons")),
        Map.Of(("isbn", "978-1779501127"),
            ("title", "Watchmen"),
            ("author_name", "Billy Gibson"))
    );
    [Fact]
    public void Table_As_List_Of_Maps()
    {
        var maps = With.Database(Db.ReadFrom);

        var expected = List.Of(
            Map.Of(("isbn", "978-1982137274"), 
                               ("title", "7 Habits of Highly Effective People"), 
                               ("publication_year", 1998)),
            Map.Of(("isbn", "978-0812981605"), 
                   ("title", "Watchmen"), 
                   ("publication_year", 1985)));

        maps.ShouldEqual(expected);
    }
    [Fact]
    public void Should_Aggregate_Authors()
    {
        var rows7Habits = List.Of(
            Map.Of(("author_name", "Sean Covey"),
                ("isbn", "978-1982137274"),
                ("title", "7 Habits of Highly Effective People")),
            Map.Of(("author_name", "Stephen Covey"),
                ("isbn", "978-1982137274"),
                ("title", "7 Habits of Highly Effective People"))
        );
        
        var expectedResults = Map.Of(
            ("isbn", "978-1982137274"),
            ("title", "7 Habits of Highly Effective People"),
            ("authorNames", List.Of("Sean Covey", "Stephen Covey"))
        );

        var result = _.AggregateField(rows7Habits, "author_name", "authorNames");
        result.ShouldEqual(expectedResults);
    }
    [Fact]
    public void Should_Aggregate_Fields()
    {
        var expectedResult = List.Of(
            Map.Of(("isbn", "978-1982137274"),
                ("title", "7 Habits of Highly Effective People"),
                ("authorNames", List.Of("Steven Clarey", "Tom Jons"))),
            Map.Of(("isbn", "978-1779501127"),
                ("title", "Watchmen"),
                ("authorNames", List.Of("Billy Gibson")))
        );
        var result = _.AggregateFields(authorsListMap, "isbn", "author_name", "authorNames");
        result.ShouldEqual(expectedResult);
    }
    [Fact]
    public void Should_KeyBy()
    {
        var books = List.Of(
            Map.Of(
                ("title", "7 Habits of Highly Effective People"),
                ("isbn", "978-1982137274"),
                ("available", true)
            ),
            Map.Of(
                ("title", "The Power of Habit"),
                ("isbn", "978-0812981605"),
                ("available", false)
            ));

        _.KeyBy(books, "isbn").ShouldEqual(Map.Of(
                ("978-0812981605", Map.Of(
                    ("available", false),
                    ("isbn", "978-0812981605"),
                    ("title", "The Power of Habit")
                )),
                ("978-1982137274", Map.Of(
                    ("available", true),
                    ("isbn", "978-1982137274"),
                    ("title", "7 Habits of Highly Effective People")
                ))
            ));
    }
    [Fact]
    public void Should_Unwind()
    {
        var customer = Map.Of(
            ("customer-id", "joe"),
            ("items", List.Of(
                Map.Of(
                    ("item", "phone"),
                    ("quantity", 1)
                ),
                Map.Of(
                    ("item", "pencil"),
                    ("quantity", 10)
                )
            )));

        var expectedRes = List.Of(
            Map.Of(
                ("customer-id", "joe"),
                ("items", Map.Of(
                    ("item", "phone"),
                    ("quantity", 1)
                ))
            ),
            Map.Of(
                ("customer-id", "joe"),
                ("items", Map.Of(
                    ("item", "pencil"),
                    ("quantity", 10)
                ))
            ));

        var result = _.Unwind(customer, "items");
        result.ShouldEqual(expectedRes);
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

        var total = _.Reduce(Map.Of(("a", 1), ("b", 2)), (int acc, DataValue v, StringOrInt key) =>
        {
            seenKeys.Add(key switch { string s => s, int i => i.ToString() });
            return acc + (int)v.As<long>();
        }, 0);

        total.Should().Be(3);
        seenKeys.Should().BeEquivalentTo("a", "b");
    }
}
