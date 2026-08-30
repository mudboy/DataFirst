namespace DataFirst.Lodash;

public static partial class _
{
    public static IndexedList InsertAt(IndexedList list, int idx, object value)
    {
        if (list.Count < idx)
        {
            var diff = idx - list.Count;
            return list.InsertRange(list.Count, Enumerable.Repeat<object>(null!, diff)).Add(value);
        }

        return list.Insert(idx, value);
    }
}