using System.Collections.Immutable;
using DataFirst.Testing;
using DataFirst.Database;
using DataFirst.Lodash;
using AwesomeAssertions;
using Xunit;

namespace DataFirst.Tests;

public sealed class WritingDataTests
{
    [Fact]
    public void Should_Write_Through_A_Path_That_Mixes_Keys_And_Indexes()
    {
        var data = Map.Of(
            ("a", List.Of(
                Map.Of(("b", List.Of("zero", "one"))))));

        var updated = _.Set(data, ["a", 0, "b", 1], "ONE");

        updated.ShouldEqual(Map.Of(
            ("a", List.Of(
                Map.Of(("b", List.Of("zero", "ONE")))))));
    }
    [Fact]
    public void Should_Update()
    {
        var input = Map.Of(("name", List.Of("one", "two", "one")));

        var result = _.Update(input, "name", 
            o => DataList.Create(o.As<DataList>().Distinct()));

        result.ShouldEqual(Map.Of(("name", List.Of("one", "two"))));
    }
    [Fact]
    public void Should_Replace_Not_Insert_When_Setting_An_Existing_Index()
    {
        var authorIds = List.Of("alan-moore", "dave-gibbons");

        var result = _.Set(authorIds, 1, "dave-chester-gibbons").As<DataList>();

        result.ShouldEqual(List.Of("alan-moore", "dave-chester-gibbons"));
    }
    [Fact]
    public void Should_Replace_Nested_List_Item_On_Set()
    {
        var books = Map.Of(
            ("978-1779501127", Map.Of(
                ("authorIds", List.Of("alan-moore", "dave-gibbons")))));

        var updated = _.Set(books, ["978-1779501127", "authorIds", 1], "dave-chester-gibbons");

        (_.Get<DataList>(updated, ["978-1779501127", "authorIds"])).ShouldEqual(List.Of("alan-moore", "dave-chester-gibbons"));
    }
    [Fact]
    public void Should_Pad_With_Nulls_When_Setting_Past_The_End()
    {
        var result = _.Set(List.Of("first"), 3, "fourth").As<DataList>();

        result.ShouldEqual(List.Of("first", DataNull.Instance, DataNull.Instance, "fourth"));
    }
    [Fact]
    public void Should_Insert_Rather_Than_Replace_With_InsertAt()
    {
        _.InsertAt(List.Of(1), 1, 4).ShouldEqual(List.Of(1, 4));

        _.InsertAt(List.Of("a", "c"), 1, "b").ShouldEqual(List.Of("a", "b", "c"));
    }
    [Fact]
    public void Should_Add_Non_Existent_Items_On_Set()
    {
        var map = Map.Of(("key", "value"));
        var updated = _.Set(map, "isVip", true);

        _.ContainsKey(updated, "isVip").Should().BeTrue();
        _.Get<bool>(updated, "isVip").Should().BeTrue();
    }
    [Fact]
    public void Should_Create_Missing_Containers_When_Setting_A_Path()
    {
        // The following step decides the container: a name makes a map, an index a list.
        (_.Set(Map.Of(), ["a", "b"], 1)).ShouldEqual(Map.Of(("a", Map.Of(("b", 1)))));

        (_.Set(Map.Of(), ["a", 0, "b"], 1)).ShouldEqual(Map.Of(("a", List.Of(Map.Of(("b", 1))))));

        // A null stands in for absent, so a diff can fill one in.
        (_.Set(Map.Of(("a", DataNull.Instance)), ["a", "b"], 1)).ShouldEqual(Map.Of(("a", Map.Of(("b", 1)))));

        // A leaf part-way along is still an error, not something to overwrite.
        var throughALeaf = () => _.Set(Map.Of(("a", "leaf")), ["a", "b"], 1);
        throughALeaf.Should().Throw<InvalidOperationException>();
    }
    [Fact]
    public void Should_Be_Immutable()
    {
        var books = Map.Of(
            ("978-1779501127", Map.Of(
                ("isbn", "978-1779501127"),
                ("title", "Watchmen"),
                ("publicationYear", 1987),
                ("authorIds", List.Of("alan-moore", "dave-gibbons"))
            )));

        var nextBooks = _.Set(books, ["978-1779501127", "publicationYear"], 1986);
        var beforeName = _.Get(nextBooks, ["978-1779501127", "authorIds", 1]);
        _.Set(nextBooks, ["978-1779501127", "authorIds", 1], "dave-chester-gibbons");
        var afterName = _.Get(nextBooks, ["978-1779501127", "authorIds", 1]);

        beforeName.Should().Be(afterName);
    }
    [Fact]
    public void Should_Preserve_Insertion_Order()
    {
        var map = Map.Of(("z", 1), ("a", 2), ("m", 3));

        map.Keys.Should().Equal("z", "a", "m");

        // Overwriting keeps position; a new key appends.
        _.Set(map, "a", 99).Keys.Should().Equal("z", "a", "m");
        _.Set(map, "b", 4).Keys.Should().Equal("z", "a", "m", "b");

        // Order is presentation only -- equality ignores it.
        Map.Of(("a", 1), ("b", 2)).ShouldEqual(Map.Of(("b", 2), ("a", 1)));
    }
}
