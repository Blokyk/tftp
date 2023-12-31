#pragma warning disable IDE0057

var addr = IPAddress.Loopback;

if (args.Length != 1 || !Int32.TryParse(args[0], out var port)) {
    Console.Error.WriteLine("Usage: ./tftp <port>");
    return 1;
}

var server = new TcpListener(addr, port);

server.Start();
Console.WriteLine($"[INFO] Server started on {addr}:{port}.");

while (true) {
    using var client = server.AcceptTcpClient();
    Console.WriteLine("[INFO] Client connected!");

    // note: we have to declare it outside because disposing it also closes the connection
    using var stream = client.GetStream();
    while (client.Connected) {
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
            case null:
                // in case an error occurred, no need to print another error message
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

    // wait until there's data available OR we're disconnected
    SpinWait.SpinUntil(() => {
        stream.Socket.Receive((Span<byte>)[]); // need to call receive to update `Socket.Connected`
        return stream.Socket is { Available: > 0 } or { Connected: false };
    });

    if (!stream.Socket.Connected) {
        Console.Error.WriteLine("[ERROR] Client disconnected prematurely!");
        return null;
    }

    Span<byte> buffer = stackalloc byte[512];
    var bytesRead = stream.ReadAtLeast(buffer, 2, false);

    if (bytesRead < 2) {
        ReportError(stream, 0, "Packet was too small");
        return null;
    }

    // restrict span to number of bytes actually read
    buffer = buffer.Slice(0, bytesRead);

    if (buffer[0] != 0x0) {
        ReportError(stream, 4, $"Unknown packet opcode 0x{buffer[0]:X2}{buffer[1]:X2}");
        return null;
    }

    switch (buffer[1]) {
        case 0x1:
            if (!ReadPacket.TryParse(buffer, out var readPacket)) {
                ReportError(stream, 4, "Error parsing RRQ packet");
                return null;
            }

            return readPacket;
        case 0x2:
            if (!WritePacket.TryParse(buffer, out var writePacket)) {
                ReportError(stream, 4, "Error parsing WRQ packet");
                return null;
            }

            return writePacket;
        case 0x3:
            if (!DataPacket.TryParse(buffer, out var dataPacket)) {
                ReportError(stream, 4, "Error parsing DATA packet");
                return null;
            }

            return dataPacket;
        case 0x4:
            if (!AckPacket.TryParse(buffer, out var ackPacket)) {
                ReportError(stream, 4, "Error parsing ACK packet");
                return null;
            }

            return ackPacket;
        case 0x5:
            // todo: implement error packets
        default:
            ReportError(stream, 0, $"Unknown packet opcode 0x{buffer[0]:X2}{buffer[1]:X2}");
            return null;
    }
}

static void HandleReadPacket(ReadPacket packet, NetworkStream clientStream) {
    using var fileStream = new FileStream(packet.filename, FileMode.Open);

    if (fileStream.Length > UInt16.MaxValue * 512) { // maximum of blockID
        ReportError(clientStream, 4, $"File '{packet.filename}' must be {UInt16.MaxValue * 512} bytes or less to transfer it with TFTP.");
        return;
    }

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

        if (responsePacket is null)
            return;

        if (responsePacket is not AckPacket ackPacket) {
            // todo: add error packet
            ReportError(clientStream, 4, $"Expected an ACK packet, but got {responsePacket}");
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

    if (fileStream.Length > UInt16.MaxValue * 512) { // maximum of blockID
        ReportError(clientStream, 4, $"File '{packet.filename}' must be {UInt16.MaxValue * 512} bytes or less to transfer it with TFTP.");
        return;
    }

    int bytesWritten = 0;

    ushort blockID = 0;
    bool isLastBlock = false;
    while (!isLastBlock) {
        var ackPacket = new AckPacket(blockID);
        clientStream.Write(ackPacket.ToBytes());

        Console.WriteLine($"[INFO] Sent {ackPacket}, waiting for DATA packet...");

        var responsePacket = ReadSinglePacket(clientStream);

        if (responsePacket is null)
            return;

        if (responsePacket is not DataPacket dataPacket) {
            // todo: add error packet
            ReportError(clientStream, 4, $"Expected a DATA packet, but got {responsePacket}");
            return;
        }

        if (dataPacket.blockID != blockID) {
            ReportError(clientStream, 5, $"Expected block {blockID}, but got DATA for block {dataPacket.blockID}");
            return;
        }

        Console.WriteLine($"[INFO] Received {dataPacket}, writing data to {packet.filename}");

        bytesWritten = dataPacket.data.Length;
        fileStream.Write(dataPacket.data.Span);

        isLastBlock = dataPacket.IsLastBlock;
        blockID++;
    }
}

static void ReportError(NetworkStream stream, int errCode, string message) {
    Console.Error.WriteLine($"[ERR] (errCode={errCode}) {message}");
    throw new Exception(message);
}