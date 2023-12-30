using System.Diagnostics.CodeAnalysis;

public readonly struct WritePacket(string filename, DataMode mode) : IPacket<WritePacket>
{
    public readonly string filename = filename;
    public readonly DataMode mode = mode;

    public static bool TryParse(ReadOnlySpan<byte> rawData, [NotNullWhen(true)] out WritePacket? packet) {
        packet = null;

        // opcode + 2 terminating bytes for filename and mode strings
        if (rawData.Length is < 4 or > 512)
            return false;

        var reader = new BufferReader<byte>(rawData);

        var opcode = reader.Read(2);

        if (!opcode.SequenceEqual<byte>([0x2, 0x0]))
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

        packet = new WritePacket(filename, mode);
        return true;
    }

    public override string ToString()
        => $"WRQ({filename}, mode: {mode})";

    public byte[] ToBytes() {
        var modeStr = Utils.DataModeToString(mode);

        var filenameByteCount = Encoding.ASCII.GetByteCount(filename);
        var modeStrByteCount = Encoding.ASCII.GetByteCount(modeStr);

        var buffer = new byte[4 + filenameByteCount + modeStrByteCount];

        var writer = new BufferWriter<byte>(buffer);

        writer.WriteOne(0x2);
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