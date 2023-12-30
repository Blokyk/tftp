using System.Diagnostics.CodeAnalysis;

public readonly struct AckPacket : IPacket<AckPacket>
{
    public readonly ushort blockID;

    public AckPacket(ushort blockID) {
        this.blockID = blockID;
    }

    public static bool TryParse(ReadOnlySpan<byte> rawData, [NotNullWhen(true)] out AckPacket? packet) {
        packet = null;

        // opcode + 2 for block ID
        if (rawData.Length != 4)
            return false;

        var reader = new BufferReader<byte>(rawData);

        var opcode = reader.Read(2);

        if (!opcode.SequenceEqual(stackalloc byte[2] { 0x4, 0x0 }))
            return false;

        var blockIDBuffer = reader.Read(2);
        var blockID = BitConverter.ToUInt16(blockIDBuffer);

        packet = new AckPacket(blockID);
        return true;
    }

    public override string ToString()
        => $"ACK({blockID})";

    public byte[] ToBytes()
        => new byte[] {
            0x4,
            0x0,
            (byte)(blockID % 255),
            (byte)(blockID >> 8)
        };
}