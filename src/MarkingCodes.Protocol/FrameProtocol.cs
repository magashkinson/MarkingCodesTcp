using System.Buffers.Binary;
using System.Text;

namespace MarkingCodes.Protocol;

// кадр: [длина, 4 байта big-endian][UTF-8]
public static class FrameProtocol
{
    public const int HeaderSize = sizeof(int);
    public const int MaxPayloadBytes = 64 * 1024;

    public static async Task WriteAsync(Stream stream, string message, CancellationToken ct)
    {
        var payloadLength = Encoding.UTF8.GetByteCount(message);
        if (payloadLength > MaxPayloadBytes)
            throw new ArgumentException($"Message is {payloadLength} bytes, max is {MaxPayloadBytes}.", nameof(message));

        var frame = new byte[HeaderSize + payloadLength];
        BinaryPrimitives.WriteInt32BigEndian(frame, payloadLength);
        Encoding.UTF8.GetBytes(message, frame.AsSpan(HeaderSize));
        await stream.WriteAsync(frame, ct);
    }

    // null - соединение закрыто между кадрами
    public static async Task<string?> ReadAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[HeaderSize];
        var read = await stream.ReadAtLeastAsync(header, HeaderSize, throwOnEndOfStream: false, ct);
        if (read == 0)
            return null;
        if (read < HeaderSize)
            throw new EndOfStreamException("Connection closed in the middle of a frame header.");

        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is < 0 or > MaxPayloadBytes)
            throw new InvalidDataException($"Invalid frame length {length}.");

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, ct);
        return Encoding.UTF8.GetString(payload);
    }
}
