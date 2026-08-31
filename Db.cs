using System.Data.Common;
using System.Globalization;

namespace DataFirst;

public static class Db
{
    /// Reads a result set as a list of maps -- the generic representation, so the
    /// rest of the system never sees a reader or a row type.
    public static DataList ReadFrom(DbDataReader reader)
    {
        if (!reader.Read()) return DataList.Empty;

        var rows = new List<DataValue>();
        var columns = reader.GetColumnSchema();

        do
        {
            var row = DataMap.CreateBuilder();
            for (var i = 0; i < columns.Count; i++)
                row.Set(columns[i].ColumnName, ToDataValue(reader[i]));

            rows.Add(row.ToDataMap());
        } while (reader.Read());

        return DataList.Create(rows);
    }

    /// Narrows a driver's CLR value onto the union. Integers all land on long and
    /// floating point on double, so 1998 read from SQLite equals the literal 1998.
    private static DataValue ToDataValue(object? value) =>
        value switch
        {
            null or DBNull => DataNull.Instance,
            string s => s,
            bool b => b,
            long n => n,
            int n => (long)n,
            short n => (long)n,
            byte n => (long)n,
            double d => d,
            float f => (double)f,
            decimal m => (double)m,
            DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
            byte[] bytes => Convert.ToBase64String(bytes),
            _ => throw new NotSupportedException(
                $"No DataValue case for column type {value.GetType().Name}")
        };
}
