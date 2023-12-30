public ref struct BufferWriter<T>
{
    public int Position;
    public readonly Span<T> Buffer;

    public BufferWriter(Span<T> span) {
        Buffer = span;
    }

    public Span<T> Available
        => Buffer.Length - Position <= 0
        ? Span<T>.Empty
        : Buffer.Slice(Position);

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
        var free = Available;

        if (!data.TryCopyTo(free))
            return false;

        Position += data.Length;

        return true;
    }
}