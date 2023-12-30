public ref struct BufferWriter<T>(Span<T> span)
{
    public int Position;
    public readonly Span<T> Buffer = span;

    public readonly Span<T> Available
        => Buffer.Length - Position <= 0 ? [] : Buffer[Position..];

    public void Skip(int offset) {
        if (offset < 0)
            return;

        Position += offset;
    }

    public bool WriteOne(T item) {
        if (Position >= Buffer.Length)
            return false;

        Buffer[Position++] = item;

        return true;
    }

    public bool Write(ReadOnlySpan<T> data) {
        if (!data.TryCopyTo(Available))
            return false;

        Position += data.Length;

        return true;
    }
}