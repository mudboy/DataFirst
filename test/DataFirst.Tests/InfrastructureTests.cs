using System.Data;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DataFirst.Tests;

public sealed class PasswordTests
{
    private static readonly Gen<string> Password =
        Gen.Choose(1, 12).SelectMany(n => Gen.Elements("abcXYZ019 !é☃".ToCharArray()).ArrayOf(n).Select(c => new string(c)));

    [Property(MaxTest = 25)]
    public Property A_password_verifies_against_its_own_hash() =>
        Prop.ForAll(Password.ToArbitrary(), p => Passwords.Verify(Passwords.Hash(p, 1), p));

    [Property(MaxTest = 25)]
    public Property A_different_password_does_not_verify() =>
        Prop.ForAll(Password.Zip(Password).Where(t => t.Item1 != t.Item2).ToArbitrary(), t =>
            !Passwords.Verify(Passwords.Hash(t.Item1, 1), t.Item2));

    [Property(MaxTest = 25)]
    public Property Hashing_the_same_password_twice_gives_different_salts_and_hashes() =>
        Prop.ForAll(Password.ToArbitrary(), p =>
        {
            var (one, two) = (Passwords.Hash(p, 1), Passwords.Hash(p, 1));
            return _.Get<string>(one, "salt") != _.Get<string>(two, "salt")
                   && _.Get<string>(one, "hash") != _.Get<string>(two, "hash");
        });

    [Property(MaxTest = 25)]
    public Property The_record_never_contains_the_password() =>
        Prop.ForAll(Password.Where(p => p.Length >= 4).ToArbitrary(), p =>
            !Passwords.Hash(p, 1).ToString().Contains(p) && !Passwords.Hash(p, 1).ToString().Contains(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(p))));

    [Property(MaxTest = 25)]
    public Property The_iteration_count_travels_with_the_hash_and_is_honoured(PositiveInt seed) =>
        Prop.ForAll(Gen.Choose(1, 50).ToArbitrary(), iterations =>
        {
            var record = Passwords.Hash("secret", iterations);
            var altered = record.SetItem("iterations", (long)iterations + 1);
            return _.Get<long>(record, "iterations") == iterations
                   && Passwords.Verify(record, "secret")
                   && !Passwords.Verify(altered, "secret");
        });

    [Fact]
    public void The_hash_is_a_map_with_salt_hash_and_iterations_that_fits_the_member_schema()
    {
        var record = Passwords.Hash("secret", 1);

        record.Keys.Should().Equal("salt", "hash", "iterations");
        Convert.FromBase64String(_.Get<string>(record, "salt")).Should().HaveCount(16);
        Convert.FromBase64String(_.Get<string>(record, "hash")).Should().HaveCount(32);
        Validation.Validate(Schemas.Member, Map.Of("email", "a@b.co", "password", record)).IsValid().Should().BeTrue();
    }

    [Fact]
    public void The_default_work_factor_is_high()
    {
        _.Get<long>(Passwords.Hash("secret"), "iterations").Should().BeGreaterThanOrEqualTo(210_000);
    }

    [Fact]
    public void An_empty_password_cannot_be_hashed_or_verified()
    {
        new Action(() => Passwords.Hash("", 1)).Should().Throw<ArgumentException>();
        new Action(() => Passwords.Hash(null!, 1)).Should().Throw<ArgumentException>();
        Passwords.Verify(Passwords.Hash("x", 1), "").Should().BeFalse();
        Passwords.Verify(Passwords.Hash("x", 1), null!).Should().BeFalse();
    }

    [Fact]
    public void Passwords_are_case_and_whitespace_sensitive()
    {
        var record = Passwords.Hash("Secret", 1);

        Passwords.Verify(record, "secret").Should().BeFalse();
        Passwords.Verify(record, "Secret ").Should().BeFalse();
        Passwords.Verify(record, "Secret").Should().BeTrue();
    }

    [Fact]
    public void A_credential_that_is_not_a_map_is_a_failed_login()
    {
        Passwords.Verify(DataNull.Instance, "x").Should().BeFalse();
        Passwords.Verify("plaintext", "plaintext").Should().BeFalse();
        Passwords.Verify(DataList.Empty, "x").Should().BeFalse();
    }

    [Fact]
    public void A_malformed_credential_record_is_a_failed_login_not_a_crash()
    {
        var good = Passwords.Hash("x", 1);

        Passwords.Verify(DataMap.Empty, "x").Should().BeFalse();
        Passwords.Verify(good.Remove("salt"), "x").Should().BeFalse();
        Passwords.Verify(good.Remove("hash"), "x").Should().BeFalse();
        Passwords.Verify(good.Remove("iterations"), "x").Should().BeFalse();
        Passwords.Verify(good.SetItem("salt", "***not base64***"), "x").Should().BeFalse();
        Passwords.Verify(good.SetItem("hash", "***not base64***"), "x").Should().BeFalse();
        Passwords.Verify(good.SetItem("salt", 5), "x").Should().BeFalse();
        Passwords.Verify(good.SetItem("iterations", "many"), "x").Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_record_claiming_no_iterations_is_a_failed_login_not_a_crash(int iterations)
    {
        var record = Passwords.Hash("x", 1).SetItem("iterations", (long)iterations);
        Passwords.Verify(record, "x").Should().BeFalse();
    }

    [Fact]
    public void A_truncated_stored_hash_never_matches()
    {
        var good = Passwords.Hash("x", 1);
        var hash = Convert.FromBase64String(_.Get<string>(good, "hash"));

        Passwords.Verify(good.SetItem("hash", Convert.ToBase64String(hash[..16])), "x").Should().BeFalse();
        Passwords.Verify(good.SetItem("hash", ""), "x").Should().BeFalse();
    }
}

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
            "i", 42,
            "r", 1.5,
            "s", "hello",
            "n", DataNull.Instance,
            "b", Convert.ToBase64String([0xDE, 0xAD, 0xBE, 0xEF]))));
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
                    "i", (long)r.i, "s", r.isNull ? DataNull.Instance : (DataValue)r.s)).ToList());

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
        var path = Debug.Dump("spec-dump", Map.Of("a", List.Of(1, 2)));
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
