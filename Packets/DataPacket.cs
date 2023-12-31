using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

public readonly struct DataPacket(ushort blockID, ReadOnlyMemory<byte> data) : IPacket<DataPacket>
{
    public readonly ushort blockID = blockID;
    public readonly ReadOnlyMemory<byte> data = data;

    public bool IsLastBlock => data.Length == 512;

    public static bool TryParse(ReadOnlySpan<byte> rawData, [NotNullWhen(true)] out DataPacket? packet) {
        packet = null;

        // opcode + 2 for block ID
        if (rawData.Length is < 4 or > 516)
            return false;

        var reader = new BufferReader<byte>(rawData);

        if (!reader.TryReadExactly(2, out var opcode))
            return false;

        if (!opcode.SequenceEqual<byte>([0x0, 0x3]))
            return false;

        if (!reader.TryReadExactly(2, out var blockIDBuffer))
            return false;

        var blockID = (ushort)((blockIDBuffer[0] << 8) | (blockIDBuffer[1]));

        var data = reader.Span.ToArray().AsMemory();

        packet = new DataPacket(blockID, data);
        return true;
    }

    public override string ToString()
        => $"DATA({blockID}, <{data.Length} bytes>)";

    public byte[] ToBytes() {
        var buffer = new byte[4 + data.Length];

        var writer = new BufferWriter<byte>(buffer);

        writer.WriteOne(0x0);
        writer.WriteOne(0x3);

        writer.WriteOne((byte)(blockID >>> 8));
        writer.WriteOne((byte)blockID); // = blockID % 256

        writer.Write(data.Span);

        return buffer;
    }
}