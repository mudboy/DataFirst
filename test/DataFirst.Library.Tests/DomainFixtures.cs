using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

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
