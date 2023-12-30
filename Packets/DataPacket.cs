using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

public readonly struct DataPacket : IPacket<DataPacket>
{
    public readonly ushort blockID;
    public readonly ReadOnlyMemory<byte> data;

    public bool IsLastBlock => data.Length == 512;

    public DataPacket(ushort blockID, ReadOnlyMemory<byte> data) {
        this.blockID = blockID;
        this.data = data;
    }

    public static bool TryParse(ReadOnlySpan<byte> rawData, [NotNullWhen(true)] out DataPacket? packet) {
        packet = null;

        // opcode + 2 for block ID
        if (rawData.Length is < 4 or > 512)
            return false;

        var reader = new BufferReader<byte>(rawData);

        var opcode = reader.Read(2);

        if (!opcode.SequenceEqual(stackalloc byte[2] { 0x3, 0x0 }))
            return false;

        var blockIDBuffer = reader.Read(2);
        var blockID = BitConverter.ToUInt16(blockIDBuffer);

        var data = reader.Span.ToArray().AsMemory();

        packet = new DataPacket(blockID, data);
        return true;
    }

    public override string ToString()
        => $"DATA({blockID}, <{data.Length} bytes>)";

    public byte[] ToBytes() {
        var buffer = new byte[4 + data.Length];

        var writer = new BufferWriter<byte>(buffer);

        writer.WriteOne(0x3);
        writer.WriteOne(0x0);

        writer.WriteOne((byte)(blockID % 255));
        writer.WriteOne((byte)(blockID >> 8));

        writer.Write(data.Span);

        return buffer;
    }
}