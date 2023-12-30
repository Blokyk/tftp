internal static class Utils
{
    public static string CreateSpanFromNullTerminatedBuffer(ReadOnlySpan<byte> buffer) {
        unsafe {
            fixed (byte* ptr = buffer) {
                return new string((sbyte*)ptr);
            }
        }
    }

    public static void PrintByteArray(IEnumerable<byte> buffer, int bytesPerLine = 8) {
        static char getCharForByte(byte b) {
            return !Char.IsControl((char)b) ? Convert.ToChar(b) : '.';
        }

        static void printRawBytes(ReadOnlySpan<byte> bytes, int size) {
            for (int i = 0; i < Math.Min(bytes.Length, size); i++) {
                Console.Write(" " + bytes[i].ToString("x2"));
            }

            for (int j = bytes.Length; j < size; j++) {
                Console.Write("   ");
            }
        }

        static void printDecodedBytes(ReadOnlySpan<byte> bytes, int size) {
            for (int i = 0; i < Math.Min(bytes.Length, size); i++) {
                Console.Write(getCharForByte(bytes[i]));
            }

            for (int j = bytes.Length; j < size; j++) {
                Console.Write(".");
            }
        }

        foreach (var bytes in buffer.Chunk(bytesPerLine)) {
            int bytesPerHalfLine = bytesPerLine / 2;
            printRawBytes(bytes.AsSpan(), bytesPerHalfLine);

            Console.Write(" ");

            if (bytes.Length <= bytesPerHalfLine)
                printRawBytes(ReadOnlySpan<byte>.Empty, bytesPerHalfLine);
            else
                printRawBytes(bytes.AsSpan(bytesPerHalfLine), bytesPerHalfLine);

            Console.Write(" |");
            printDecodedBytes(bytes.AsSpan(), bytesPerHalfLine);

            Console.Write(" ");

            if (bytes.Length <= bytesPerHalfLine)
                printDecodedBytes(ReadOnlySpan<byte>.Empty, bytesPerHalfLine);
            else
                printDecodedBytes(bytes.AsSpan(bytesPerHalfLine), bytesPerHalfLine);

            Console.WriteLine("|");
        }
    }

    public static string DataModeToString(DataMode mode)
        => mode switch {
                DataMode.NetAscii => "netascii",
                DataMode.Octet => "octet",
                DataMode.Mail => "mail",
                _ => ""
        };
}