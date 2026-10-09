using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class CatalogTests
{
    private static readonly DataMap Catalog = DomainFixtures.Catalog;

    private static DataList Search(DataMap query) => DataFirst.Library.Catalog.SearchBook(Catalog, query);

    // Generated catalogues: years, titles and author sets vary, ids are fixed.

    private static readonly Gen<DataMap> RandomCatalog =
        from count in Gen.Choose(0, 8)
        from books in (from year in Gen.OneOf(Gen.Choose(1400, 2100).Select(y => (long?)y), Gen.Constant<long?>(null))
                       from title in Gen.Elements("Watchmen", "WATCH", "Dark", "Sandman", "watch the throne", "Maus")
                       from authors in Gen.Choose(1, 3)
                       select (year, title, authors)).ArrayOf(count)
        select Build(books);

    private static DataMap Build((long? year, string title, int authors)[] books)
    {
        var authorsById = Enumerable.Range(1, 3).Aggregate(DataMap.Empty, (m, i) =>
            m.SetItem($"a{i}", Map.Of(("name", $"Author {i}"), ("bookIsbns", DataList.Empty))));

        var booksByIsbn = books.Select((b, i) => (b, isbn: $"978-{i:D10}")).Aggregate(DataMap.Empty, (m, t) =>
        {
            var book = Map.Of(
                ("isbn", t.isbn), ("title", t.b.title),
                ("authorIds", DataList.Create(Enumerable.Range(1, t.b.authors).Select(a => (DataValue)$"a{a}").ToList())));
            return m.SetItem(t.isbn, t.b.year is { } y ? book.SetItem("publicationYear", y) : book);
        });

        return Map.Of(("booksByIsbn", booksByIsbn), ("authorsById", authorsById));
    }

    private static IEnumerable<DataMap> Books(DataMap catalog) =>
        _.Get<DataMap>(catalog, "booksByIsbn").Values.Select(b => b.As<DataMap>());

    [Property]
    public Property An_empty_query_matches_every_book_in_order() =>
        Prop.ForAll(RandomCatalog.ToArbitrary(), catalog =>
            DataFirst.Library.Catalog.SearchBook(catalog, DataMap.Empty).Select(r => _.Get<string>(r, "isbn"))
                .SequenceEqual(Books(catalog).Select(b => _.Get<string>(b, "isbn"))));

    [Property]
    public Property Year_bounds_select_exactly_the_books_inside_them_inclusively() =>
        Prop.ForAll(
            (from catalog in RandomCatalog from a in Gen.Choose(1400, 2100) from b in Gen.Choose(1400, 2100) select (catalog, a, b)).ToArbitrary(),
            t =>
            {
                var expected = Books(t.catalog)
                    .Where(b => b.ContainsKey("publicationYear")
                                && _.Get<long>(b, "publicationYear") >= t.a
                                && _.Get<long>(b, "publicationYear") <= t.b)
                    .Select(b => _.Get<string>(b, "isbn"));

                var actual = DataFirst.Library.Catalog.SearchBook(t.catalog, Map.Of(("publishedAfter", t.a), ("publishedBefore", t.b)))
                    .Select(r => _.Get<string>(r, "isbn"));

                return actual.SequenceEqual(expected);
            });

    [Property]
    public Property Title_search_is_a_case_insensitive_substring_match() =>
        Prop.ForAll(
            (from catalog in RandomCatalog
             from needle in Gen.Elements("watch", "WATCH", "Dark", "an", "zzz", "e", "A")
             select (catalog, needle)).ToArbitrary(),
            t =>
            {
                var expected = Books(t.catalog).Where(b => _.Get<string>(b, "title").Contains(t.needle, StringComparison.OrdinalIgnoreCase));
                var actual = DataFirst.Library.Catalog.SearchBook(t.catalog, Map.Of(("title", t.needle)));

                return actual.Select(r => _.Get<string>(r, "isbn")).SequenceEqual(expected.Select(b => _.Get<string>(b, "isbn")))
                       && DataFirst.Library.Catalog.SearchBooksByTitle(t.catalog, t.needle).Equals(actual);
            });

    [Property]
    public Property Criteria_combine_with_and() =>
        Prop.ForAll(
            (from catalog in RandomCatalog from a in Gen.Choose(1400, 2100) select (catalog, a)).ToArbitrary(),
            t =>
            {
                var both = DataFirst.Library.Catalog.SearchBook(t.catalog, Map.Of(("title", "watch"), ("publishedAfter", t.a)));
                var byTitle = DataFirst.Library.Catalog.SearchBook(t.catalog, Map.Of(("title", "watch")));
                var byYear = DataFirst.Library.Catalog.SearchBook(t.catalog, Map.Of(("publishedAfter", t.a)));

                return both.All(r => byTitle.Contains(r) && byYear.Contains(r))
                       && both.Count == byTitle.Count(r => byYear.Contains(r));
            });

    [Property]
    public Property Results_describe_each_book_by_title_isbn_and_resolved_author_names() =>
        Prop.ForAll(RandomCatalog.ToArbitrary(), catalog =>
            DataFirst.Library.Catalog.SearchBook(catalog, DataMap.Empty).All(r =>
            {
                var info = r.As<DataMap>();
                var names = _.Get<DataList>(info, "authorNames").Select(n => n.As<string>()).ToList();
                return info.Keys.SequenceEqual(["title", "isbn", "authorNames"])
                       && names.Count >= 1
                       && names.All(n => n.StartsWith("Author "));
            }));

    [Fact]
    public void Search_by_author_matches_any_of_a_books_authors_ignoring_case()
    {
        Titles(Search(Map.Of(("author", "moore")))).Should().Equal("Watchmen");
        Titles(Search(Map.Of(("author", "GIBBONS")))).Should().Equal("Watchmen");
        Titles(Search(Map.Of(("author", "miller")))).Should().Equal("The Dark Knight Returns", "Undated Pamphlet");
        Search(Map.Of(("author", "tolkien"))).Should().BeEmpty();
    }

    [Fact]
    public void A_book_without_a_year_is_excluded_by_any_year_bound_but_kept_otherwise()
    {
        Titles(Search(Map.Of(("publishedAfter", 1400)))).Should().NotContain("Undated Pamphlet");
        Titles(Search(Map.Of(("publishedBefore", 2100)))).Should().NotContain("Undated Pamphlet");
        Titles(Search(DataMap.Empty)).Should().Contain("Undated Pamphlet");
        Titles(Search(Map.Of(("author", "miller")))).Should().Contain("Undated Pamphlet");
    }

    [Fact]
    public void Year_bounds_are_inclusive_at_both_ends()
    {
        Titles(Search(Map.Of(("publishedAfter", 1987)))).Should().Equal("Watchmen");
        Titles(Search(Map.Of(("publishedBefore", 1986)))).Should().Equal("The Dark Knight Returns");
        Titles(Search(Map.Of(("publishedAfter", 1987), ("publishedBefore", 1986)))).Should().BeEmpty();
    }

    [Fact]
    public void An_invalid_query_is_rejected_before_searching()
    {
        new Action(() => Search(Map.Of(("publishedAfter", 1)))).Should().Throw<SchemaViolationException>();
        new Action(() => Search(Map.Of(("colour", "red")))).Should().Throw<SchemaViolationException>();
        new Action(() => Search(Map.Of(("title", "")))).Should().Throw<SchemaViolationException>();
    }

    [Fact]
    public void A_book_naming_an_unknown_author_cannot_be_described()
    {
        var broken = _.Set(Catalog, ["booksByIsbn", "978-0000000001", "authorIds"], List.Of("ghost"));
        new Action(() => DataFirst.Library.Catalog.SearchBook(broken, DataMap.Empty)).Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void An_empty_catalogue_has_nothing_to_find()
    {
        var empty = Map.Of(("booksByIsbn", DataMap.Empty), ("authorsById", DataMap.Empty));
        DataFirst.Library.Catalog.SearchBook(empty, DataMap.Empty).Should().BeEmpty();
        DataFirst.Library.Catalog.SearchBooksByTitle(empty, "x").Should().BeEmpty();
    }

    // Lendings

    [Fact]
    public void Lendings_are_enriched_in_order_with_the_book_and_its_authors()
    {
        var lendings = UserManagement.BookLendings(DomainFixtures.Users, "lent@x.co");

        var described = DataFirst.Library.Catalog.GetBookLendings(Catalog, lendings);

        described.ShouldEqual(List.Of(
            Map.Of(("bookItemId", "item-1"), ("lendingDate", "2021-01-02"), ("title", "Watchmen"),
                ("isbn", "978-0000000001"), ("authorNames", List.Of("Alan Moore", "Dave Gibbons"))),
            Map.Of(("bookItemId", "item-2"), ("lendingDate", "2021-02-03"), ("title", "The Dark Knight Returns"),
                ("isbn", "978-0000000002"), ("authorNames", List.Of("Frank Miller")))));
    }

    [Fact]
    public void No_lendings_describe_to_nothing_and_an_unknown_book_is_an_error()
    {
        DataFirst.Library.Catalog.GetBookLendings(Catalog, DataList.Empty).Should().BeEmpty();
        new Action(() => DataFirst.Library.Catalog.GetBookLendings(Catalog, List.Of(
                Map.Of(("bookItemId", "i"), ("bookIsbn", "978-9999999999"), ("lendingDate", "2020-01-01")))))
            .Should().Throw<KeyNotFoundException>().WithMessage("*978-9999999999*");
    }

    // Adding items

    private static readonly DataMap WatchmenIsbnInfo =
        Map.Of(("isbn", "978-0000000001"), ("id", "new-item"), ("libId", "lib-1"));

    [Property]
    public Property Adding_an_item_appends_a_not_lent_copy_and_changes_nothing_else() =>
        Prop.ForAll(Gen.Elements("item-a", "item-b", "x").ToArbitrary(), id =>
        {
            var book = _.Get<DataMap>(Catalog, ["booksByIsbn", "978-0000000001"]);
            var before = book.ToString();

            var updated = DataFirst.Library.Catalog.AddItemToBook(book, Map.Of(("isbn", "978-0000000001"), ("id", id), ("libId", "lib-1")));
            var items = _.Get<DataList>(updated, "bookItems");

            return items.Count == 1
                   && items[0].Equals((DataValue)Map.Of(("id", id), ("libId", "lib-1"), ("isLent", false)))
                   && updated.Remove("bookItems").Equals(book.Remove("bookItems"))
                   && book.ToString() == before;
        });

    [Fact]
    public void Items_accumulate_and_a_repeated_id_is_refused()
    {
        var book = _.Get<DataMap>(Catalog, ["booksByIsbn", "978-0000000001"]);
        var withOne = DataFirst.Library.Catalog.AddItemToBook(book, WatchmenIsbnInfo);
        var withTwo = DataFirst.Library.Catalog.AddItemToBook(withOne, WatchmenIsbnInfo.SetItem("id", "second"));

        _.Get<DataList>(withTwo, "bookItems").Select(i => _.Get<string>(i, "id")).Should().Equal("new-item", "second");
        new Action(() => DataFirst.Library.Catalog.AddItemToBook(withTwo, WatchmenIsbnInfo))
            .Should().Throw<DuplicateBookItemException>().WithMessage("*new-item*");
    }

    [Fact]
    public void The_same_item_id_may_exist_on_different_books()
    {
        var once = DataFirst.Library.Catalog.AddBookItem(Catalog, WatchmenIsbnInfo);
        var twice = DataFirst.Library.Catalog.AddBookItem(once, WatchmenIsbnInfo.SetItem("isbn", "978-0000000002"));

        _.Get<DataList>(twice, ["booksByIsbn", "978-0000000002", "bookItems"]).Should().HaveCount(1);
    }

    [Fact]
    public void Adding_to_the_catalogue_touches_only_the_named_book()
    {
        var updated = DataFirst.Library.Catalog.AddBookItem(Catalog, WatchmenIsbnInfo);

        _.DiffObjects(Catalog, updated).ChangedPathsAre(
            "booksByIsbn.978-0000000001.bookItems.[0].id",
            "booksByIsbn.978-0000000001.bookItems.[0].libId",
            "booksByIsbn.978-0000000001.bookItems.[0].isLent");
    }

    [Fact]
    public void Adding_an_item_to_an_unknown_book_or_with_a_bad_shape_is_refused()
    {
        new Action(() => DataFirst.Library.Catalog.AddBookItem(Catalog, WatchmenIsbnInfo.SetItem("isbn", "978-9999999999")))
            .Should().Throw<KeyNotFoundException>();
        new Action(() => DataFirst.Library.Catalog.AddBookItem(Catalog, WatchmenIsbnInfo.SetItem("isLent", true)))
            .Should().Throw<SchemaViolationException>();
        new Action(() => DataFirst.Library.Catalog.AddItemToBook(DataMap.Empty, Map.Of(("id", "i"))))
            .Should().Throw<SchemaViolationException>();
    }

    private static string[] Titles(DataList results) => DomainFixtures.Titles(results);
}
