using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace DataFirst;

/// A throwaway database holding a couple of books, so the reading code has something
/// real to read from.
public static class With
{
    private const string Schema =
        """
        CREATE TABLE books (
            isbn             TEXT PRIMARY KEY,
            title            TEXT,
            publication_year INTEGER
        );
        """;

    // Single quotes: SQLite accepts double-quoted string literals only as a
    // compatibility quirk, and reads them as identifiers wherever one would fit.
    private const string Seed =
        """
        INSERT INTO books (isbn, title, publication_year) VALUES
            ('978-1982137274', '7 Habits of Highly Effective People', 1998),
            ('978-0812981605', 'Watchmen', 1985);
        """;

    private const string SelectAll =
        """
        SELECT title, isbn, publication_year
        FROM books
        """;

    /// Opens the database, hands the reader to f, and closes everything after.
    /// f must finish with the reader before it returns.
    public static T Database<T>(Func<DbDataReader, T> f)
    {
        ArgumentNullException.ThrowIfNull(f);

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        Execute(connection, Schema);
        Execute(connection, Seed);

        using var select = connection.CreateCommand();
        select.CommandText = SelectAll;

        using var reader = select.ExecuteReader();
        return f(reader);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
