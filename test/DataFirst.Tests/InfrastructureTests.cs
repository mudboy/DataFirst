using DataFirst.Testing;
using System.Data;
using DataFirst.Database;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DataFirst.Tests;

public sealed class DbReadTests
{
    private static DataList Query(string create, string insert, string select, Action<SqliteCommand>? bind = null)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        foreach (var sql in new[] { create, insert })
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            bind?.Invoke(command);
            command.ExecuteNonQuery();
        }

        using var query = connection.CreateCommand();
        query.CommandText = select;
        using var reader = query.ExecuteReader();
        return Db.ReadFrom(reader);
    }

    [Fact]
    public void No_rows_is_the_empty_list()
    {
        Query("CREATE TABLE t (a INTEGER)", "SELECT 1 WHERE 0", "SELECT a FROM t").ShouldEqual(DataList.Empty);
    }

    [Fact]
    public void Each_sqlite_storage_class_lands_on_its_union_case()
    {
        var rows = Query(
            "CREATE TABLE t (i INTEGER, r REAL, s TEXT, n TEXT, b BLOB)",
            "INSERT INTO t VALUES (42, 1.5, 'hello', NULL, x'DEADBEEF')",
            "SELECT i, r, s, n, b FROM t");

        rows.ShouldEqual(List.Of(Map.Of(
            ("i", 42),
            ("r", 1.5),
            ("s", "hello"),
            ("n", DataNull.Instance),
            ("b", Convert.ToBase64String([0xDE, 0xAD, 0xBE, 0xEF])))));
    }

    [Fact]
    public void Integers_are_longs_so_they_equal_the_literal()
    {
        var rows = Query("CREATE TABLE t (year INTEGER)", "INSERT INTO t VALUES (1998)", "SELECT year FROM t");

        rows[0].As<DataMap>()["year"].Unwrap().Should().BeOfType<long>();
        rows[0].As<DataMap>()["year"].Equals((DataValue)1998).Should().BeTrue();
    }

    [Fact]
    public void Columns_keep_the_order_the_query_asked_for_and_aliases_are_honoured()
    {
        var rows = Query("CREATE TABLE t (a INTEGER, b INTEGER)", "INSERT INTO t VALUES (1, 2)", "SELECT b AS second, a AS first FROM t");

        rows[0].As<DataMap>().Keys.Should().Equal("second", "first");
    }

    [Fact]
    public void Rows_come_back_in_the_order_of_the_result_set()
    {
        var rows = Query(
            "CREATE TABLE t (n INTEGER)",
            "INSERT INTO t VALUES (3), (1), (2)",
            "SELECT n FROM t ORDER BY n DESC");

        rows.Select(r => r.As<DataMap>()["n"].As<long>()).Should().Equal(3, 2, 1);
    }

    [Property(MaxTest = 30)]
    public Property Whatever_is_stored_is_read_back_unchanged() =>
        Prop.ForAll(
            (from n in Gen.Choose(1, 6)
             from rows in (from i in Gen.Choose(-1000, 1000)
                           from s in Gens.Word
                           from isNull in Gen.Elements(true, false)
                           select (i, s, isNull)).ArrayOf(n)
             select rows).ToArbitrary(),
            rows =>
            {
                using var connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();
                Exec(connection, "CREATE TABLE t (pos INTEGER, i INTEGER, s TEXT)");

                for (var pos = 0; pos < rows.Length; pos++)
                {
                    using var insert = connection.CreateCommand();
                    insert.CommandText = "INSERT INTO t VALUES ($pos, $i, $s)";
                    insert.Parameters.AddWithValue("$pos", pos);
                    insert.Parameters.AddWithValue("$i", rows[pos].i);
                    insert.Parameters.AddWithValue("$s", rows[pos].isNull ? DBNull.Value : rows[pos].s);
                    insert.ExecuteNonQuery();
                }

                using var select = connection.CreateCommand();
                select.CommandText = "SELECT i, s FROM t ORDER BY pos";
                using var reader = select.ExecuteReader();

                var expected = DataList.Create(rows.Select(r => (DataValue)Map.Of(
                    ("i", (long)r.i), ("s", r.isNull ? DataNull.Instance : (DataValue)r.s))).ToList());

                return Db.ReadFrom(reader).Equals(expected);
            });

    [Fact]
    public void Every_clr_type_a_driver_might_hand_back_is_narrowed_onto_the_union()
    {
        var table = new DataTable();
        foreach (var (name, type) in new (string, Type)[]
                 {
                     ("long", typeof(long)), ("int", typeof(int)), ("short", typeof(short)), ("byte", typeof(byte)),
                     ("double", typeof(double)), ("float", typeof(float)), ("decimal", typeof(decimal)),
                     ("bool", typeof(bool)), ("string", typeof(string)), ("date", typeof(DateTime)),
                     ("bytes", typeof(byte[]))
                 })
            table.Columns.Add(name, type);

        table.Rows.Add(1L, 2, (short)3, (byte)4, 1.5, 2.5f, 3.25m, true, "s",
            new DateTime(2020, 4, 23, 10, 30, 0, DateTimeKind.Utc), new byte[] { 1, 2 });

        var row = Db.ReadFrom(table.CreateDataReader())[0].As<DataMap>();

        row["long"].Equals((DataValue)1L).Should().BeTrue();
        row["int"].Equals((DataValue)2L).Should().BeTrue();
        row["short"].Equals((DataValue)3L).Should().BeTrue();
        row["byte"].Equals((DataValue)4L).Should().BeTrue();
        row["double"].Equals((DataValue)1.5).Should().BeTrue();
        row["float"].Equals((DataValue)2.5).Should().BeTrue();
        row["decimal"].Equals((DataValue)3.25).Should().BeTrue();
        row["bool"].Equals((DataValue)true).Should().BeTrue();
        row["string"].Equals((DataValue)"s").Should().BeTrue();
        row["date"].As<string>().Should().StartWith("2020-04-23T10:30:00");
        row["bytes"].Equals((DataValue)Convert.ToBase64String([1, 2])).Should().BeTrue();
    }

    [Fact]
    public void A_column_type_with_no_union_case_is_refused_by_name()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(Guid));
        table.Rows.Add(Guid.NewGuid());

        var act = () => Db.ReadFrom(table.CreateDataReader());

        act.Should().Throw<NotSupportedException>().WithMessage("*Guid*");
    }

    [Fact]
    public void A_null_column_is_the_explicit_null_case()
    {
        var table = new DataTable();
        table.Columns.Add("x", typeof(string));
        table.Rows.Add(DBNull.Value);

        Db.ReadFrom(table.CreateDataReader())[0].As<DataMap>()["x"].Equals((DataValue)DataNull.Instance).Should().BeTrue();
    }

    [Fact]
    public void With_Database_hands_over_the_seeded_books_and_closes_afterwards()
    {
        var rows = With.Database(Db.ReadFrom);

        rows.Count.Should().Be(2);
        rows.Select(r => r.As<DataMap>()["isbn"].As<string>()).Should().BeEquivalentTo("978-1982137274", "978-0812981605");
        rows[0].As<DataMap>().Keys.Should().Equal("title", "isbn", "publication_year");
    }

    [Fact]
    public void With_Database_gives_each_caller_a_fresh_database()
    {
        With.Database(Db.ReadFrom).ShouldEqual(With.Database(Db.ReadFrom));
        new Action(() => With.Database<int>(null!)).Should().Throw<ArgumentNullException>();
    }

    private static void Exec(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}

public sealed class DebugDumpTests : IDisposable
{
    private readonly List<string> written = [];

    public void Dispose()
    {
        foreach (var path in written) File.Delete(path);
    }

    [Fact]
    public void Dump_writes_the_json_and_returns_where()
    {
        var path = Debug.Dump("spec-dump", Map.Of(("a", List.Of(1, 2))));
        written.Add(path);

        File.ReadAllText(path).Should().Be("""{"a":[1,2]}""");
        Path.GetFileName(path).Should().Be("spec-dump.json");
    }

    [Fact]
    public void Dump_overwrites_an_earlier_dump()
    {
        written.Add(Debug.Dump("spec-overwrite", 1L));
        var path = Debug.Dump("spec-overwrite", 2L);
        written.Add(path);

        File.ReadAllText(path).Should().Be("2");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Dump_needs_a_name(string context)
    {
        new Action(() => Debug.Dump(context, 1L)).Should().Throw<ArgumentException>();
    }
}
