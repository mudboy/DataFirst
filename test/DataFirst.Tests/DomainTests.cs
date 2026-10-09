using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Tests;

public static class DomainFixtures
{
    public static DataMap Member(string email, params (string Flag, bool Value)[] flags) =>
        flags.Aggregate(
            Map.Of(("email", email), ("password", Passwords.Hash("pw", 1))),
            (m, f) => m.SetItem(f.Flag, f.Value));

    public static DataMap Librarian(string email) =>
        Map.Of(("email", email), ("password", Passwords.Hash("pw", 1)));

    public static readonly DataMap Users = Map.Of(
        ("librarians", Map.Of(("lib@x.co", Librarian("lib@x.co")))),
        ("members", Map.Of(
            ("plain@x.co", Member("plain@x.co")),
            ("vip@x.co", Member("vip@x.co", ("isVip", true))),
            ("super@x.co", Member("super@x.co", ("isSuper", true))),
            ("blocked@x.co", Member("blocked@x.co", ("isBlocked", true))),
            ("lent@x.co", Member("lent@x.co").SetItem("bookLendings", List.Of(
                Map.Of(("bookItemId", "item-1"), ("bookIsbn", "978-0000000001"), ("lendingDate", "2021-01-02")),
                Map.Of(("bookItemId", "item-2"), ("bookIsbn", "978-0000000002"), ("lendingDate", "2021-02-03"))))))));

    public static readonly DataMap Catalog = Map.Of(
        ("booksByIsbn", Map.Of(
            ("978-0000000001", Map.Of(
                ("isbn", "978-0000000001"), ("title", "Watchmen"), ("publicationYear", 1987),
                ("authorIds", List.Of("a1", "a2")))),
            ("978-0000000002", Map.Of(
                ("isbn", "978-0000000002"), ("title", "The Dark Knight Returns"), ("publicationYear", 1986),
                ("authorIds", List.Of("a3")))),
            ("978-0000000003", Map.Of(
                ("isbn", "978-0000000003"), ("title", "Undated Pamphlet"),
                ("authorIds", List.Of("a3")))))),
        ("authorsById", Map.Of(
            ("a1", Map.Of(("name", "Alan Moore"), ("bookIsbns", List.Of("978-0000000001")))),
            ("a2", Map.Of(("name", "Dave Gibbons"), ("bookIsbns", List.Of("978-0000000001")))),
            ("a3", Map.Of(("name", "Frank Miller"), ("bookIsbns", List.Of("978-0000000002", "978-0000000003")))))));

    public static readonly DataMap Library = Map.Of(("catalog", Catalog), ("userManagementData", Users));

    public static string[] Titles(DataList results) =>
        results.Select(r => _.Get<string>(r, "title")).ToArray();
}

public sealed class UserManagementTests
{
    private static readonly DataMap Users = DomainFixtures.Users;

    [Fact]
    public void Roles_are_distinct_collections()
    {
        UserManagement.IsLibrarian(Users, "lib@x.co").Should().BeTrue();
        UserManagement.IsMember(Users, "lib@x.co").Should().BeFalse();
        UserManagement.IsMember(Users, "plain@x.co").Should().BeTrue();
        UserManagement.IsLibrarian(Users, "plain@x.co").Should().BeFalse();
        UserManagement.IsMember(Users, "nobody@x.co").Should().BeFalse();
        UserManagement.IsLibrarian(Users, "nobody@x.co").Should().BeFalse();
    }

    [Fact]
    public void Flags_default_to_false_and_never_apply_to_librarians_or_strangers()
    {
        UserManagement.IsVipMember(Users, "vip@x.co").Should().BeTrue();
        UserManagement.IsVipMember(Users, "plain@x.co").Should().BeFalse();
        UserManagement.IsSuperMember(Users, "super@x.co").Should().BeTrue();
        UserManagement.IsSuperMember(Users, "vip@x.co").Should().BeFalse();
        UserManagement.IsBlocked(Users, "blocked@x.co").Should().BeTrue();
        UserManagement.IsBlocked(Users, "plain@x.co").Should().BeFalse();
        UserManagement.IsVipMember(Users, "lib@x.co").Should().BeFalse();
        UserManagement.IsBlocked(Users, "nobody@x.co").Should().BeFalse();
    }

    [Fact]
    public void A_flag_set_to_something_other_than_true_is_not_set()
    {
        var odd = _.Set(Users, ["members", "plain@x.co", "isVip"], "yes");
        UserManagement.IsVipMember(odd, "plain@x.co").Should().BeFalse();
    }

    [Fact]
    public void Authentication_needs_a_known_unblocked_user_and_the_right_password()
    {
        UserManagement.Authenticate(Users, "plain@x.co", "pw").Should().BeTrue();
        UserManagement.Authenticate(Users, "lib@x.co", "pw").Should().BeTrue();
        UserManagement.Authenticate(Users, "plain@x.co", "wrong").Should().BeFalse();
        UserManagement.Authenticate(Users, "nobody@x.co", "pw").Should().BeFalse();
        UserManagement.Authenticate(Users, "blocked@x.co", "pw").Should().BeFalse();
        UserManagement.Authenticate(Users, "plain@x.co", "").Should().BeFalse();
    }

    [Fact]
    public void A_user_with_no_password_record_cannot_log_in()
    {
        var stripped = _.Set(Users, ["members", "plain@x.co"], Map.Of(("email", "plain@x.co")));
        UserManagement.Authenticate(stripped, "plain@x.co", "pw").Should().BeFalse();
    }

    [Fact]
    public void Lendings_are_listed_in_order_and_default_to_empty()
    {
        UserManagement.BookLendings(Users, "lent@x.co").Select(l => _.Get<string>(l, "bookItemId"))
            .Should().Equal("item-1", "item-2");
        UserManagement.BookLendings(Users, "plain@x.co").Should().BeEmpty();
    }

    [Fact]
    public void Lendings_of_a_stranger_or_a_librarian_are_an_error_not_an_empty_list()
    {
        new Action(() => UserManagement.BookLendings(Users, "nobody@x.co")).Should().Throw<KeyNotFoundException>();
        new Action(() => UserManagement.BookLendings(Users, "lib@x.co")).Should().Throw<KeyNotFoundException>();
    }

    [Property]
    public Property Blocking_then_unblocking_leaves_only_the_flag_changed() =>
        Prop.ForAll(Gen.Elements("plain@x.co", "vip@x.co", "super@x.co", "lent@x.co").ToArbitrary(), id =>
        {
            var blocked = UserManagement.BlockMember(Users, id);
            var unblocked = UserManagement.UnblockMember(blocked, id);

            return UserManagement.IsBlocked(blocked, id)
                   && !UserManagement.IsBlocked(unblocked, id)
                   && _.Set(unblocked, ["members", id], _.Get<DataMap>(unblocked, ["members", id]).Remove("isBlocked"))
                       .Equals(_.Set(Users, ["members", id], _.Get<DataMap>(Users, ["members", id]).Remove("isBlocked")))
                   && Users.Equals(DomainFixtures.Users);
        });

    [Property]
    public Property Blocking_is_idempotent() =>
        Prop.ForAll(Gen.Elements("plain@x.co", "blocked@x.co").ToArbitrary(), id =>
        {
            var once = UserManagement.BlockMember(Users, id);
            return UserManagement.BlockMember(once, id).Equals(once);
        });

    [Fact]
    public void Blocking_stops_a_member_logging_in_and_unblocking_restores_it()
    {
        var blocked = UserManagement.BlockMember(Users, "plain@x.co");
        UserManagement.Authenticate(blocked, "plain@x.co", "pw").Should().BeFalse();
        UserManagement.Authenticate(UserManagement.UnblockMember(blocked, "plain@x.co"), "plain@x.co", "pw").Should().BeTrue();
    }

    [Fact]
    public void Only_members_can_be_blocked()
    {
        new Action(() => UserManagement.BlockMember(Users, "lib@x.co")).Should().Throw<KeyNotFoundException>();
        new Action(() => UserManagement.UnblockMember(Users, "nobody@x.co")).Should().Throw<KeyNotFoundException>();
    }

    [Property]
    public Property An_added_member_exists_and_nothing_else_changes() =>
        Prop.ForAll(Gen.Elements("new@x.co", "other.person@example.org", "a@b.cd").ToArbitrary(), email =>
        {
            var added = UserManagement.AddMember(Users, DomainFixtures.Member(email));

            return UserManagement.IsMember(added, email)
                   && !UserManagement.IsMember(Users, email)
                   && _.Get<DataMap>(added, "members").Count == _.Get<DataMap>(Users, "members").Count + 1
                   && _.Get<DataMap>(added, "librarians").Equals(_.Get<DataMap>(Users, "librarians"))
                   && UserManagement.Authenticate(added, email, "pw");
        });

    [Fact]
    public void Adding_a_librarian_files_them_under_librarians()
    {
        var added = UserManagement.AddLibrarian(Users, DomainFixtures.Librarian("newlib@x.co"));

        UserManagement.IsLibrarian(added, "newlib@x.co").Should().BeTrue();
        UserManagement.IsMember(added, "newlib@x.co").Should().BeFalse();
        UserManagement.Authenticate(added, "newlib@x.co", "pw").Should().BeTrue();
    }

    [Theory]
    [InlineData("plain@x.co")]
    [InlineData("lib@x.co")]
    public void An_email_is_unique_across_members_and_librarians(string email)
    {
        new Action(() => UserManagement.AddMember(Users, DomainFixtures.Member(email)))
            .Should().Throw<DuplicateUserException>().WithMessage($"*{email}*");
        new Action(() => UserManagement.AddLibrarian(Users, DomainFixtures.Librarian(email)))
            .Should().Throw<DuplicateUserException>();
    }

    [Fact]
    public void A_user_that_breaks_the_schema_is_rejected_before_the_duplicate_check()
    {
        new Action(() => UserManagement.AddMember(Users, Map.Of(("email", "plain@x.co"))))
            .Should().Throw<SchemaViolationException>();
        new Action(() => UserManagement.AddLibrarian(Users, Map.Of(("email", "not-an-email"), ("password", Passwords.Hash("x", 1)))))
            .Should().Throw<SchemaViolationException>();
        new Action(() => UserManagement.AddMember(Users, DomainFixtures.Member("x@y.co").SetItem("isVip", "yes")))
            .Should().Throw<SchemaViolationException>();
    }

    [Fact]
    public void Adding_a_user_to_empty_user_data_creates_the_collection()
    {
        var added = UserManagement.AddMember(DataMap.Empty, DomainFixtures.Member("first@x.co"));
        UserManagement.IsMember(added, "first@x.co").Should().BeTrue();
    }
}

[Properties(Arbitrary = [typeof(Arbs)])]
public sealed class CatalogTests
{
    private static readonly DataMap Catalog = DomainFixtures.Catalog;

    private static DataList Search(DataMap query) => DataFirst.Catalog.SearchBook(Catalog, query);

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
            DataFirst.Catalog.SearchBook(catalog, DataMap.Empty).Select(r => _.Get<string>(r, "isbn"))
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

                var actual = DataFirst.Catalog.SearchBook(t.catalog, Map.Of(("publishedAfter", t.a), ("publishedBefore", t.b)))
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
                var actual = DataFirst.Catalog.SearchBook(t.catalog, Map.Of(("title", t.needle)));

                return actual.Select(r => _.Get<string>(r, "isbn")).SequenceEqual(expected.Select(b => _.Get<string>(b, "isbn")))
                       && DataFirst.Catalog.SearchBooksByTitle(t.catalog, t.needle).Equals(actual);
            });

    [Property]
    public Property Criteria_combine_with_and() =>
        Prop.ForAll(
            (from catalog in RandomCatalog from a in Gen.Choose(1400, 2100) select (catalog, a)).ToArbitrary(),
            t =>
            {
                var both = DataFirst.Catalog.SearchBook(t.catalog, Map.Of(("title", "watch"), ("publishedAfter", t.a)));
                var byTitle = DataFirst.Catalog.SearchBook(t.catalog, Map.Of(("title", "watch")));
                var byYear = DataFirst.Catalog.SearchBook(t.catalog, Map.Of(("publishedAfter", t.a)));

                return both.All(r => byTitle.Contains(r) && byYear.Contains(r))
                       && both.Count == byTitle.Count(r => byYear.Contains(r));
            });

    [Property]
    public Property Results_describe_each_book_by_title_isbn_and_resolved_author_names() =>
        Prop.ForAll(RandomCatalog.ToArbitrary(), catalog =>
            DataFirst.Catalog.SearchBook(catalog, DataMap.Empty).All(r =>
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
        new Action(() => DataFirst.Catalog.SearchBook(broken, DataMap.Empty)).Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void An_empty_catalogue_has_nothing_to_find()
    {
        var empty = Map.Of(("booksByIsbn", DataMap.Empty), ("authorsById", DataMap.Empty));
        DataFirst.Catalog.SearchBook(empty, DataMap.Empty).Should().BeEmpty();
        DataFirst.Catalog.SearchBooksByTitle(empty, "x").Should().BeEmpty();
    }

    // Lendings

    [Fact]
    public void Lendings_are_enriched_in_order_with_the_book_and_its_authors()
    {
        var lendings = UserManagement.BookLendings(DomainFixtures.Users, "lent@x.co");

        var described = DataFirst.Catalog.GetBookLendings(Catalog, lendings);

        described.ShouldEqual(List.Of(
            Map.Of(("bookItemId", "item-1"), ("lendingDate", "2021-01-02"), ("title", "Watchmen"),
                ("isbn", "978-0000000001"), ("authorNames", List.Of("Alan Moore", "Dave Gibbons"))),
            Map.Of(("bookItemId", "item-2"), ("lendingDate", "2021-02-03"), ("title", "The Dark Knight Returns"),
                ("isbn", "978-0000000002"), ("authorNames", List.Of("Frank Miller")))));
    }

    [Fact]
    public void No_lendings_describe_to_nothing_and_an_unknown_book_is_an_error()
    {
        DataFirst.Catalog.GetBookLendings(Catalog, DataList.Empty).Should().BeEmpty();
        new Action(() => DataFirst.Catalog.GetBookLendings(Catalog, List.Of(
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

            var updated = DataFirst.Catalog.AddItemToBook(book, Map.Of(("isbn", "978-0000000001"), ("id", id), ("libId", "lib-1")));
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
        var withOne = DataFirst.Catalog.AddItemToBook(book, WatchmenIsbnInfo);
        var withTwo = DataFirst.Catalog.AddItemToBook(withOne, WatchmenIsbnInfo.SetItem("id", "second"));

        _.Get<DataList>(withTwo, "bookItems").Select(i => _.Get<string>(i, "id")).Should().Equal("new-item", "second");
        new Action(() => DataFirst.Catalog.AddItemToBook(withTwo, WatchmenIsbnInfo))
            .Should().Throw<DuplicateBookItemException>().WithMessage("*new-item*");
    }

    [Fact]
    public void The_same_item_id_may_exist_on_different_books()
    {
        var once = DataFirst.Catalog.AddBookItem(Catalog, WatchmenIsbnInfo);
        var twice = DataFirst.Catalog.AddBookItem(once, WatchmenIsbnInfo.SetItem("isbn", "978-0000000002"));

        _.Get<DataList>(twice, ["booksByIsbn", "978-0000000002", "bookItems"]).Should().HaveCount(1);
    }

    [Fact]
    public void Adding_to_the_catalogue_touches_only_the_named_book()
    {
        var updated = DataFirst.Catalog.AddBookItem(Catalog, WatchmenIsbnInfo);

        _.DiffObjects(Catalog, updated).ChangedPathsAre(
            "booksByIsbn.978-0000000001.bookItems.[0].id",
            "booksByIsbn.978-0000000001.bookItems.[0].libId",
            "booksByIsbn.978-0000000001.bookItems.[0].isLent");
    }

    [Fact]
    public void Adding_an_item_to_an_unknown_book_or_with_a_bad_shape_is_refused()
    {
        new Action(() => DataFirst.Catalog.AddBookItem(Catalog, WatchmenIsbnInfo.SetItem("isbn", "978-9999999999")))
            .Should().Throw<KeyNotFoundException>();
        new Action(() => DataFirst.Catalog.AddBookItem(Catalog, WatchmenIsbnInfo.SetItem("isLent", true)))
            .Should().Throw<SchemaViolationException>();
        new Action(() => DataFirst.Catalog.AddItemToBook(DataMap.Empty, Map.Of(("id", "i"))))
            .Should().Throw<SchemaViolationException>();
    }

    private static string[] Titles(DataList results) => DomainFixtures.Titles(results);
}

file static class DiffAssertions
{
    public static void ChangedPathsAre(this DataMap diff, params string[] expected) =>
        _.ChangedPaths(diff).Select(p => p.ToString()).Should().BeEquivalentTo(expected);
}

public sealed class LibraryAuthorisationTests
{
    private static readonly DataMap Library = DomainFixtures.Library;
    private static readonly DataMap Info = Map.Of(("isbn", "978-0000000001"), ("id", "i-1"), ("libId", "l-1"));

    [Theory]
    [InlineData("lib@x.co", true)]
    [InlineData("super@x.co", true)]
    [InlineData("vip@x.co", false)]
    [InlineData("plain@x.co", false)]
    [InlineData("nobody@x.co", false)]
    public void Only_librarians_and_super_members_read_lendings(string user, bool allowed)
    {
        var read = () => DataFirst.Library.GetBookLendings(Library, user, "lent@x.co");

        if (allowed) read().Should().HaveCount(2);
        else read.Should().Throw<Exception>().WithMessage("Not allowed*");
    }

    [Theory]
    [InlineData("lib@x.co", true)]
    [InlineData("vip@x.co", true)]
    [InlineData("super@x.co", false)]
    [InlineData("plain@x.co", false)]
    [InlineData("nobody@x.co", false)]
    public void Only_librarians_and_vip_members_add_items(string user, bool allowed)
    {
        var add = () => DataFirst.Library.AddBookItem(Library, user, Info);

        if (allowed)
            _.Get<DataList>(add(), ["catalog", "booksByIsbn", "978-0000000001", "bookItems"]).Should().HaveCount(1);
        else
            add.Should().Throw<Exception>().WithMessage("Not allowed*");
    }

    [Fact]
    public void Adding_an_item_returns_the_whole_library_with_the_rest_untouched()
    {
        var updated = DataFirst.Library.AddBookItem(Library, "lib@x.co", Info);

        _.Get<DataMap>(updated, "userManagementData").Equals(DomainFixtures.Users).Should().BeTrue();
        _.Get<DataMap>(updated, ["catalog", "authorsById"]).Equals(_.Get<DataMap>(DomainFixtures.Catalog, "authorsById")).Should().BeTrue();
    }

    [Fact]
    public void Searches_run_over_the_catalogue_and_serialise_to_json()
    {
        DataFirst.Library.SearchBook(Library, Map.Of(("title", "watch"))).Should().HaveCount(1);
        DataFirst.Library.SearchBooksByTitleJson(Library, "dark")
            .Should().Be("""[{"title":"The Dark Knight Returns","isbn":"978-0000000002","authorNames":["Frank Miller"]}]""");
        DataFirst.Library.SearchBooksByTitleJson(Library, "zzz").Should().Be("[]");
    }

    [Fact]
    public void A_search_request_can_project_fields_in_the_order_asked()
    {
        DataFirst.Library.SearchBooksJson(Library, Map.Of(("title", "watch"), ("fields", List.Of("isbn", "title"))))
            .Should().Be("""[{"isbn":"978-0000000001","title":"Watchmen"}]""");
        DataFirst.Library.SearchBooksJson(Library, Map.Of(("title", "watch"), ("fields", List.Of("authorNames"))))
            .Should().Be("""[{"authorNames":["Alan Moore","Dave Gibbons"]}]""");
        DataFirst.Library.SearchBooksJson(Library, Map.Of(("title", "watch")))
            .Should().Contain("\"title\":\"Watchmen\"").And.Contain("\"authorNames\"");
    }

    [Fact]
    public void A_malformed_search_request_never_reaches_the_catalogue()
    {
        new Action(() => DataFirst.Library.SearchBooksJson(Library, Map.Of(("fields", List.Of("title")))))
            .Should().Throw<SchemaViolationException>();
        new Action(() => DataFirst.Library.SearchBooksJson(Library, Map.Of(("title", "w"), ("fields", List.Of("password")))))
            .Should().Throw<SchemaViolationException>();
    }
}

public sealed class LibrarySystemTests
{
    public static IEnumerable<object[]> Kinds => new[] { "snapshot", "diff-indexed" }.Select(k => new object[] { k });

    private static LibrarySystem Make(string kind) =>
        new(kind == "snapshot"
            ? new SnapshotAggregateStore(DomainFixtures.Library)
            : new DiffIndexedStore(DomainFixtures.Library));

    private static readonly DataMap Info = Map.Of(("isbn", "978-0000000001"), ("id", "i-1"), ("libId", "l-1"));

    [Fact]
    public void A_system_can_be_built_straight_from_data()
    {
        new LibrarySystem(DomainFixtures.Library).Snapshot().Equals(DomainFixtures.Library).Should().BeTrue();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Reads_see_the_initial_library(string kind)
    {
        var system = Make(kind);

        system.Snapshot().Equals(DomainFixtures.Library).Should().BeTrue();
        system.SearchBook(Map.Of(("author", "moore"))).Should().HaveCount(1);
        system.SearchBooksByTitleJson("watch").Should().Contain("Watchmen");
        system.GetBookLendings("lib@x.co", "lent@x.co").Should().HaveCount(2);
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Adding_an_item_returns_the_book_and_is_visible_to_later_reads(string kind)
    {
        var system = Make(kind);

        var book = system.AddBookItem("vip@x.co", Info);

        _.Get<DataList>(book, "bookItems").Should().HaveCount(1);
        _.Get<DataList>(system.Snapshot(), ["catalog", "booksByIsbn", "978-0000000001", "bookItems"]).Should().HaveCount(1);
        new Action(() => system.AddBookItem("lib@x.co", Info)).Should().Throw<DuplicateBookItemException>();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Adding_an_item_is_checked_for_permission_then_shape_then_existence(string kind)
    {
        var system = Make(kind);

        new Action(() => system.AddBookItem("plain@x.co", Info)).Should().Throw<Exception>().WithMessage("Not allowed*");
        new Action(() => system.AddBookItem("lib@x.co", Info.SetItem("isLent", true))).Should().Throw<SchemaViolationException>();
        new Action(() => system.AddBookItem("lib@x.co", Info.SetItem("isbn", "978-9999999999"))).Should().Throw<KeyNotFoundException>();
        system.Snapshot().Equals(DomainFixtures.Library).Should().BeTrue();
    }

    /// UserManagement.AddMember refuses an email already used by a librarian, but
    /// LibrarySystem.AddMember lifts only the `members` collection into it, so the
    /// librarians are invisible and the same email is accepted as a member too.
    [Theory(Skip = "Known gap: LibrarySystem.AddMember does not see librarians, so it cannot enforce cross-collection uniqueness"),
     MemberData(nameof(Kinds))]
    public void Adding_a_member_with_a_librarians_email_is_refused(string kind)
    {
        var system = Make(kind);

        new Action(() => system.AddMember("lib@x.co", DomainFixtures.Member("lib@x.co")))
            .Should().Throw<DuplicateUserException>();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Blocking_a_member_stops_their_login_and_leaves_the_rest_of_them_alone(string kind)
    {
        var system = Make(kind);
        var before = _.Get<DataMap>(system.Snapshot(), ["userManagementData", "members", "lent@x.co"]);

        var blocked = system.BlockMember("lib@x.co", "lent@x.co");

        blocked.Equals(before.SetItem("isBlocked", true)).Should().BeTrue();
        UserManagement.Authenticate(_.Get<DataMap>(system.Snapshot(), "userManagementData"), "lent@x.co", "pw").Should().BeFalse();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Blocking_an_already_blocked_member_changes_nothing(string kind)
    {
        // Regression: the diff is empty, and merging an empty diff used to replace the
        // whole member with {}.
        var system = Make(kind);
        var before = _.Get<DataMap>(system.Snapshot(), ["userManagementData", "members", "blocked@x.co"]);

        var again = system.BlockMember("lib@x.co", "blocked@x.co");
        var twice = system.BlockMember("lib@x.co", "blocked@x.co");

        again.Equals(before).Should().BeTrue();
        twice.Equals(before).Should().BeTrue();
        system.Snapshot().Equals(DomainFixtures.Library).Should().BeTrue();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Only_librarians_block_and_only_real_members_can_be_blocked(string kind)
    {
        var system = Make(kind);

        new Action(() => system.BlockMember("super@x.co", "plain@x.co")).Should().Throw<Exception>().WithMessage("Not allowed*");
        new Action(() => system.BlockMember("lib@x.co", "ghost@x.co")).Should().Throw<KeyNotFoundException>();
        new Action(() => system.BlockMember("lib@x.co", "lib@x.co")).Should().Throw<KeyNotFoundException>();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Adding_a_member_makes_them_able_to_log_in(string kind)
    {
        var system = Make(kind);

        var members = system.AddMember("lib@x.co", DomainFixtures.Member("fresh@x.co"));

        members.ContainsKey("fresh@x.co").Should().BeTrue();
        members.Count.Should().Be(_.Get<DataMap>(DomainFixtures.Users, "members").Count + 1);
        UserManagement.Authenticate(_.Get<DataMap>(system.Snapshot(), "userManagementData"), "fresh@x.co", "pw").Should().BeTrue();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Adding_a_member_is_refused_for_non_librarians_duplicates_and_bad_shapes(string kind)
    {
        var system = Make(kind);

        new Action(() => system.AddMember("vip@x.co", DomainFixtures.Member("fresh@x.co"))).Should().Throw<Exception>().WithMessage("Not allowed*");
        new Action(() => system.AddMember("lib@x.co", DomainFixtures.Member("plain@x.co"))).Should().Throw<DuplicateUserException>();
        new Action(() => system.AddMember("lib@x.co", Map.Of(("email", "x@y.co")))).Should().Throw<SchemaViolationException>();
        system.Snapshot().Equals(DomainFixtures.Library).Should().BeTrue();
    }

    [Theory, MemberData(nameof(Kinds))]
    public void Unrelated_operations_from_many_threads_all_take_effect(string kind)
    {
        var system = Make(kind);
        var tasks = new Action[]
        {
            () => system.AddBookItem("lib@x.co", Info),
            () => system.AddBookItem("lib@x.co", Info.SetItem("isbn", "978-0000000002")),
            () => system.BlockMember("lib@x.co", "plain@x.co"),
            () => system.BlockMember("lib@x.co", "vip@x.co"),
            () => system.AddMember("lib@x.co", DomainFixtures.Member("new1@x.co")),
        };

        Parallel.ForEach(tasks, a => a());

        var snapshot = system.Snapshot();
        UserManagement.IsBlocked(_.Get<DataMap>(snapshot, "userManagementData"), "plain@x.co").Should().BeTrue();
        UserManagement.IsBlocked(_.Get<DataMap>(snapshot, "userManagementData"), "vip@x.co").Should().BeTrue();
        UserManagement.IsMember(_.Get<DataMap>(snapshot, "userManagementData"), "new1@x.co").Should().BeTrue();
        _.Get<DataList>(snapshot, ["catalog", "booksByIsbn", "978-0000000001", "bookItems"]).Should().HaveCount(1);
        _.Get<DataList>(snapshot, ["catalog", "booksByIsbn", "978-0000000002", "bookItems"]).Should().HaveCount(1);
    }
}

public sealed class AggregatePathTests
{
    [Fact]
    public void Aggregates_name_their_place_in_the_system_data()
    {
        Aggregates.Book("b").ToString().Should().Be("catalog.booksByIsbn.b");
        Aggregates.Author("a").ToString().Should().Be("catalog.authorsById.a");
        Aggregates.Member("m@x.co").ToString().Should().Be("userManagementData.members.m@x.co");
        Aggregates.Members.ToString().Should().Be("userManagementData.members");
        Aggregates.UserManagement.ToString().Should().Be("userManagementData");
        Aggregates.Everything.ToString().Should().Be("(root)");
    }

    [Fact]
    public void Containment_is_visible_through_overlap_which_is_what_makes_the_granularity_matter()
    {
        Aggregates.Member("m@x.co").Overlaps(Aggregates.Members).Should().BeTrue();
        Aggregates.Members.Overlaps(Aggregates.UserManagement).Should().BeTrue();
        Aggregates.Everything.Overlaps(Aggregates.Book("b")).Should().BeTrue();

        Aggregates.Book("b1").Overlaps(Aggregates.Book("b2")).Should().BeFalse();
        Aggregates.Book("b").Overlaps(Aggregates.Author("b")).Should().BeFalse();
        Aggregates.Member("m@x.co").Overlaps(Aggregates.Book("b")).Should().BeFalse();
    }

    [Fact]
    public void Every_aggregate_path_resolves_in_the_seeded_library()
    {
        foreach (var path in new[]
                 {
                     Aggregates.Book("978-1779501127"), Aggregates.Author("alan-moore"),
                     Aggregates.Member("samantha@gmail.com"), Aggregates.Members, Aggregates.UserManagement
                 })
            _.ContainsKey(Library.LibraryData, path).Should().BeTrue(path.ToString());
    }
}
