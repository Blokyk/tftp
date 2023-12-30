using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

public enum DataMode { NetAscii, Octet, Mail }

public interface IPacket {
    byte[] ToBytes();
}

public interface IPacket<T> : IPacket where T : struct, IPacket<T> {
    static abstract bool TryParse(ReadOnlySpan<byte> rawData, [NotNullWhen(true)] out T? packet);
}