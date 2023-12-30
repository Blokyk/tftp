using System.Diagnostics.CodeAnalysis;

public readonly struct ReadPacket : IPacket<ReadPacket>
{
    public readonly string filename;
    public readonly DataMode mode;

    public ReadPacket(string filename, DataMode mode) {
        this.filename = filename;
        this.mode = mode;
    }

    public static bool TryParse(ReadOnlySpan<byte> rawData, [NotNullWhen(true)] out ReadPacket? packet) {
        packet = null;

        // opcode + 2 terminating bytes for filename and mode strings
        if (rawData.Length is < 4 or > 512)
            return false;

        var reader = new BufferReader<byte>(rawData);

        var opcode = reader.Read(2);

        if (!opcode.SequenceEqual(stackalloc byte[2] { 0x1, 0x0 }))
            return false;

        var filename = Utils.CreateSpanFromNullTerminatedBuffer(reader.Span);
        reader.Skip(filename.Length + 1);

        var modeStr = Utils.CreateSpanFromNullTerminatedBuffer(reader.Span);
        reader.Skip(modeStr.Length + 1);

        if (reader.Available != 0)
            return false;

        DataMode mode;

        if (modeStr.Equals("netascii", StringComparison.InvariantCultureIgnoreCase))
            mode = DataMode.NetAscii;
        else if (modeStr.Equals("octet", StringComparison.InvariantCultureIgnoreCase))
            mode = DataMode.Octet;
        else if (modeStr.Equals("mail", StringComparison.InvariantCultureIgnoreCase))
            mode = DataMode.Mail;
        else
            return false;

        packet = new ReadPacket(filename, mode);
        return true;
    }

    public override string ToString()
        => $"RRQ({filename}, mode: {mode})";

    public byte[] ToBytes() {
        var modeStr = Utils.DataModeToString(mode);

        var filenameByteCount = Encoding.ASCII.GetByteCount(filename);
        var modeStrByteCount = Encoding.ASCII.GetByteCount(modeStr);

        var buffer = new byte[4 + filenameByteCount + modeStrByteCount];

        var writer = new BufferWriter<byte>(buffer);

        writer.WriteOne(0x1);
        writer.WriteOne(0x0);

        Encoding.ASCII.GetBytes(filename, writer.Available);
        writer.Skip(filenameByteCount);
        writer.WriteOne(0x0);

        Encoding.ASCII.GetBytes(modeStr, writer.Available);
        writer.Skip(modeStrByteCount);
        writer.WriteOne(0x0);

        return buffer;
    }
}