using System.Buffers.Binary;
using MarkingCodes.Protocol;

namespace MarkingCodes.Tests;

public class FrameProtocolTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task SeveralFramesInOneBuffer_AreSplitCorrectly()
    {
        var stream = new MemoryStream();
        string[] messages = ["first", "", "третий \u001D code"];
        foreach (var m in messages)
            await FrameProtocol.WriteAsync(stream, m, Ct);
        stream.Position = 0;

        foreach (var expected in messages)
            Assert.Equal(expected, await FrameProtocol.ReadAsync(stream, Ct));
        Assert.Null(await FrameProtocol.ReadAsync(stream, Ct));
    }

    [Fact]
    public async Task FrameDeliveredOneByteAtATime_IsReassembled()
    {
        var buffer = new MemoryStream();
        await FrameProtocol.WriteAsync(buffer, "0104600000000001", Ct);
        await FrameProtocol.WriteAsync(buffer, "next", Ct);
        var stream = new OneByteReadStream(buffer.ToArray());

        Assert.Equal("0104600000000001", await FrameProtocol.ReadAsync(stream, Ct));
        Assert.Equal("next", await FrameProtocol.ReadAsync(stream, Ct));
    }

    [Fact]
    public async Task ConnectionClosedMidFrame_Throws()
    {
        var buffer = new MemoryStream();
        await FrameProtocol.WriteAsync(buffer, "truncated", Ct);
        var truncated = new MemoryStream(buffer.ToArray()[..^3]);

        await Assert.ThrowsAsync<EndOfStreamException>(() => FrameProtocol.ReadAsync(truncated, Ct));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(FrameProtocol.MaxPayloadBytes + 1)]
    public async Task InvalidLength_Throws(int length)
    {
        var header = new byte[FrameProtocol.HeaderSize];
        BinaryPrimitives.WriteInt32BigEndian(header, length);

        await Assert.ThrowsAsync<InvalidDataException>(() => FrameProtocol.ReadAsync(new MemoryStream(header), Ct));
    }

    // отдает по одному байту, как будто TCP порезал пакет
    private sealed class OneByteReadStream(byte[] data) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], ct);
    }
}
