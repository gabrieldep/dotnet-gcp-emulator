using GcpStorageEmulator.Storage;

namespace GcpStorageEmulator.Tests;

public class StorageNameEncoderTests
{
    [Theory]
    [InlineData("simple.txt")]
    [InlineData("images/logo.png")]
    [InlineData("a")]
    [InlineData("a/b")]
    [InlineData("deep/nested/path/to/file.json")]
    [InlineData("with space and +plus%percent")]
    [InlineData("unicode-ação-日本語")]
    [InlineData("trailing/")]
    [InlineData("./weird/../name")]
    public void Encode_RoundTrips(string name)
    {
        string encoded = StorageNameEncoder.Encode(name);
        Assert.Equal(name, StorageNameEncoder.Decode(encoded));
    }

    [Fact]
    public void Encode_ProducesSinglePathSegment()
    {
        string encoded = StorageNameEncoder.Encode("images/logo.png");
        Assert.DoesNotContain('/', encoded);
        Assert.DoesNotContain('\\', encoded);
        Assert.Equal("images%2Flogo.png", encoded);
    }

    [Fact]
    public void Encode_KeepsUnreservedCharacters()
    {
        Assert.Equal("abcABC012._-", StorageNameEncoder.Encode("abcABC012._-"));
    }
}
