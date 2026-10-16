using System.Text;
using GcpStorageEmulator.Storage;

namespace GcpStorageEmulator.Tests;

public class Crc32CTests
{
    [Fact]
    public void Compute_MatchesKnownCheckVector()
    {
        // The canonical CRC-32C check value for "123456789".
        uint crc = Crc32C.Compute(Encoding.ASCII.GetBytes("123456789"));
        Assert.Equal(0xE3069283u, crc);
        Assert.Equal("4waSgw==", Crc32C.ToBase64(crc));
    }

    [Fact]
    public void ToBase64_OfEmpty_IsFourZeroBytes()
    {
        Assert.Equal("AAAAAA==", Crc32C.ToBase64(Crc32C.Compute(ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void Append_IsEquivalentToSinglePass()
    {
        byte[] data = Encoding.ASCII.GetBytes("the quick brown fox");
        uint whole = Crc32C.Compute(data);

        uint chunked = Crc32C.Append(0u, data.AsSpan(0, 5));
        chunked = Crc32C.Append(chunked, data.AsSpan(5));

        Assert.Equal(whole, chunked);
    }
}
