using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace DataFirst;

public static class With
{
    public static T Database<T>(Func<DbDataReader, T> f)
    {
        using var con = new SqliteConnection("Data Source=:memory:");
        try
        {
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"Create table books (isbn TEXT PRIMARY KEY , title TEXT, publication_year NUM);";
            cmd.ExecuteNonQuery();

            var inst = con.CreateCommand();
            inst.CommandText =
                """
                INSERT INTO books (isbn, title, publication_year) values ("978-1982137274", "7 Habits of Highly Effective People", 1998),
                ("978-0812981605", "Watchmen", 1985);
                """;
            inst.ExecuteNonQuery();

            var sel = con.CreateCommand();
            sel.CommandText = """
                              SELECT title, isbn, publication_year
                              FROM books
                              """;

            var rs = sel.ExecuteReader();

            var maps = f(rs);
            return maps;
        }
        finally
        {
            con.Close();
        }
    }
}