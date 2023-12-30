#pragma warning disable IDE0057

var addr = IPAddress.Loopback;

if (args.Length != 1 || Int32.TryParse(args[0], out var port)) {
    Console.Error.WriteLine("Usage: ./tftp <port>");
    return 1;
}

var server = new TcpListener(addr, port);

server.Start();
Console.WriteLine($"[INFO] Server started on {addr}:{port}.");

while (true) {
    using var client = server.AcceptTcpClient();
    Console.WriteLine("[INFO] Client connected!");

    while (client.Connected) {
        using var stream = client.GetStream();
        var packet = ReadSinglePacket(stream);

        switch (packet) {
            case ReadPacket readPacket:
                Console.WriteLine($"[INFO] Sending file '{readPacket.filename}' with mode = {readPacket.mode}");
                HandleReadPacket(readPacket, stream);
                Console.WriteLine("[INFO] Transfer complete!");
                break;
            case WritePacket writePacket:
                Console.WriteLine($"[INFO] Receiving file '{writePacket.filename}' with mode = {writePacket.mode}");
                HandleWritePacket(writePacket, stream);
                Console.WriteLine("[INFO] Transfer complete!");
                break;
            default:
                Console.WriteLine($"[INFO] Received packet {packet}, but didn't know how to handle it");
                break;
        }
    }

    Console.WriteLine("[INFO] Client disconnected!");
}

static IPacket? ReadSinglePacket(NetworkStream stream) {
    Console.WriteLine("[INFO] Waiting for data...");

    while (!stream.DataAvailable) {}

    Span<byte> buffer = stackalloc byte[512];

    var bytesRead = stream.ReadAtLeast(buffer, 2, false);

    if (bytesRead < 2)
        return null;

    // restrict span to number of bytes actually read
    buffer = buffer.Slice(0, bytesRead);

    if (buffer[1] != 0x0)
        return null;

    switch (buffer[0]) {
        case 0x1:
            if (!ReadPacket.TryParse(buffer, out var readPacket)) {
                Console.WriteLine("Couldn't parse RRQ packet");
                return null;
            }

            return readPacket;
        case 0x2:
            if (!WritePacket.TryParse(buffer, out var writePacket)) {
                Console.WriteLine("Couldn't parse WRQ packet");
                return null;
            }

            return writePacket;
        case 0x3:
            if (!DataPacket.TryParse(buffer, out var dataPacket)) {
                Console.WriteLine("Couldn't parse DATA packet");
                return null;
            }

            return dataPacket;
        case 0x4:
            if (!AckPacket.TryParse(buffer, out var ackPacket)) {
                Console.WriteLine("Couldn't parse ACK packet");
                return null;
            }

            return ackPacket;
        default:
            Console.WriteLine($"Couldn't parse unknown packet with opcode 0x{buffer[0]:X} 0x{buffer[1]:X}");
            return null;
    }
}

static void HandleReadPacket(ReadPacket packet, NetworkStream clientStream) {
    using var fileStream = new FileStream(packet.filename, FileMode.Open);

    if (fileStream.Length > ushort.MaxValue * 512) // maximum of blockID
        ReportError(clientStream, 4, $"File '{packet.filename}' must be {ushort.MaxValue * 512} bytes or less to transfer it with TFTP.");

    byte[] buffer = new byte[512];

    ushort blockID = 1;

    while (fileStream.Position < fileStream.Length) {
        var bytesRead = fileStream.Read(buffer);

        if (bytesRead == 0)
            return;

        var dataPacket = new DataPacket(blockID, buffer.AsMemory(0, bytesRead));
        var packetBytes = dataPacket.ToBytes();
        Console.WriteLine($"[INFO] Sending {dataPacket} as {packetBytes.Length} bytes...");
        clientStream.Write(packetBytes);

        Console.WriteLine("[INFO] Waiting for ACK packet...");

        var responsePacket = ReadSinglePacket(clientStream);

        if (responsePacket is not AckPacket ackPacket) {
            ReportError(clientStream, 4, $"Packet {responsePacket} is not an ACK packet");
            return;
        }

        if (ackPacket.blockID != blockID) {
            ReportError(clientStream, 5, $"Expected block {blockID}, but got ACK for block {ackPacket.blockID}");
            return;
        }

        Console.WriteLine($"[INFO] Received ACK for {blockID}");

        blockID++;
    }
}

static void HandleWritePacket(WritePacket packet, NetworkStream clientStream) {
    using var fileStream = new FileStream(packet.filename, FileMode.OpenOrCreate);

    if (fileStream.Length > ushort.MaxValue * 512) // maximum of blockID
        ReportError(clientStream, 4, $"File '{packet.filename}' must be {ushort.MaxValue * 512} bytes or less to transfer it with TFTP.");

    byte[] buffer = new byte[512];

    ushort blockID = 0;

    int bytesWritten = 0;

    do {
        var ackPacket = new AckPacket(blockID);

        Console.WriteLine($"[ERR] Sent {ackPacket}, waiting for DATA packet...");

        var responsePacket = ReadSinglePacket(clientStream);

        if (responsePacket is not DataPacket dataPacket) {
            ReportError(clientStream, 4, $"Packet {responsePacket} is not a DATA packet");
            return;
        }

        if (dataPacket.blockID != blockID) {
            ReportError(clientStream, 5, $"Expected block {blockID}, but got DATA for block {dataPacket.blockID}");
            return;
        }

        Console.WriteLine($"[INFO] Received {dataPacket}, writing data to {packet.filename}");

        bytesWritten = dataPacket.data.Length;
        fileStream.Write(dataPacket.data.Span);

        blockID++;
    } while (bytesWritten == 512);
}

static void ReportError(NetworkStream stream, int errCode, string message) {
    Console.Error.WriteLine($"[ERR] (errCode={errCode}) {message}");
    throw new Exception(message);
}