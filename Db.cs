using System.Collections.Immutable;
using System.Data.Common;

namespace DataFirst;

public static class Db
{
    public static ImmutableList<StringMap> ReadFrom(DbDataReader reader)
    {
        if (!reader.Read()) return ImmutableList<StringMap>.Empty;

        var builder = ImmutableList.CreateBuilder<StringMap>();
        var columns = reader.GetColumnSchema();

        do
        {
            var map = ImmutableDictionary.CreateBuilder<string, Object>();
            for (int i = 0; i < columns.Count; i++)
            {
                var label = columns[i].ColumnName;
                var value = reader[i];
                map.Add(label, value);
            }

            builder.Add(map.ToImmutable());
        } while (reader.Read());

        return builder.ToImmutable();
    }
}