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

    // Replaces the element at idx, or extends the list (padding with nulls) when idx is past the end.
    // Unlike InsertAt this never grows a list whose index already exists, which is what _.Set needs.
    public static IndexedList SetAt(IndexedList list, int idx, object value) =>
        idx < list.Count
            ? list.SetItem(idx, value)
            : InsertAt(list, idx, value);
}