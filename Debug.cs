using System.Text.Json;

namespace DataFirst;

public static class Debug
{
    public static void DumbContext<T1>(string context, T1 value)
    {
        File.WriteAllText(Pathx(context), JsonSerializer.Serialize(value));
    }

    public static void DumpContext<T1, T2>(string context, (T1 v1, T2 v2) values)
    {
        DumbContext(context, values);
    }
    
    public static void DumpContext<T1, T2, T3>(string context, (T1 v1, T2 v2, T3 v3) values)
    {
        DumbContext(context, values);
    }    
    
    public static void DumpContext<T1, T2, T3, T4>(string context, (T1 v1, T2 v2, T3 v3, T4 v4) values)
    {
        DumbContext(context, values);
    }

    private static string Pathx(string context)
    {
        return Path.Combine("test-data", context);
    }
}