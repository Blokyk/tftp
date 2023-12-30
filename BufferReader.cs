public ref struct BufferReader<T> {
    public int Position;
    public readonly ReadOnlySpan<T> Buffer;

    public int Available => Buffer.Length - Position;

    public BufferReader(ReadOnlySpan<T> span) {
        Buffer = span;
    }

    public ReadOnlySpan<T> Span
        => Available <= 0
        ?  ReadOnlySpan<T>.Empty
        :  Buffer.Slice(Position);

    public void Skip(int offset) {
        if (offset < 0)
            return;

        Position += offset;
    }

    public int Read(Span<T> output, int maxCount) {
        if (maxCount <= 0)
            return 0;

        if (maxCount > output.Length)
            return 0;

        var count = Math.Min(Buffer.Length - Position, maxCount);

        if (Position + count > Buffer.Length)
            return 0;

        bool succeeded = Buffer.Slice(Position, count).TryCopyTo(output);

        Position += count;

        return succeeded ? count : 0;
    }

    public ReadOnlySpan<T> Read(int maxCount) {
        if (maxCount <= 0)
            return ReadOnlySpan<T>.Empty;

        var count = Math.Min(Buffer.Length - Position, maxCount);

        if (Position + count > Buffer.Length)
            return ReadOnlySpan<T>.Empty;

        var newSpan = Buffer.Slice(Position, count);

        Position += count;

        return newSpan;
    }

    public bool TryRead(int maxCount, out ReadOnlySpan<T> output) {
        output = ReadOnlySpan<T>.Empty;
        if (maxCount <= 0)
            return false;

        var count = Math.Min(Buffer.Length - Position, maxCount);

        if (Position + count > Buffer.Length)
            return false;

        output = Buffer.Slice(Position, count);

        Position += count;

        return Position < Buffer.Length;
    }

    public bool TryReadExactly(int count, out ReadOnlySpan<T> output) {
        output = ReadOnlySpan<T>.Empty;

        if (count <= 0)
            return false;

        if (Position + count > Buffer.Length)
            return false;

        output = Buffer.Slice(Position, count);

        Position += count;

        return true;
    }
}