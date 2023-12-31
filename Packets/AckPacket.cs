using System.Diagnostics.CodeAnalysis;

public readonly struct AckPacket(ushort blockID) : IPacket<AckPacket>
{
    public readonly ushort blockID = blockID;

    public static bool TryParse(ReadOnlySpan<byte> rawData, [NotNullWhen(true)] out AckPacket? packet) {
        packet = null;

        // opcode + 2 for block ID
        if (rawData.Length != 4)
            return false;

        var reader = new BufferReader<byte>(rawData);

        if (!reader.TryReadExactly(2, out var opcode))
            return false;

        if (!opcode.SequenceEqual<byte>([0x0, 0x4]))
            return false;

        if (!reader.TryReadExactly(2, out var blockIDBuffer))
            return false;

        var blockID = (ushort)((blockIDBuffer[0] << 8) | (blockIDBuffer[1]));

        packet = new AckPacket(blockID);
        return true;
    }

    public override string ToString()
        => $"ACK({blockID})";

    public byte[] ToBytes()
        => [
            0x0,
            0x4,
            (byte)(blockID >>> 8),
            (byte)blockID // = blockID % 256
        ];
}