namespace GcpStorageEmulator.Storage;

/// <summary>
/// CRC32C (Castagnoli, polynomial 0x1EDC6F41) used by Google Cloud Storage for the
/// <c>crc32c</c> object field. GCS reports it as the big-endian 4-byte value, base64 encoded.
/// </summary>
public static class Crc32C
{
    private const uint Polynomial = 0x82F63B78; // reflected 0x1EDC6F41
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ Polynomial : crc >> 1;
            }

            table[i] = crc;
        }

        return table;
    }

    /// <summary>Continues a running CRC32C over a buffer. Seed the first call with 0.</summary>
    public static uint Append(uint crc, ReadOnlySpan<byte> data)
    {
        crc = ~crc;
        foreach (byte b in data)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    /// <summary>Computes the CRC32C of a full buffer.</summary>
    public static uint Compute(ReadOnlySpan<byte> data) => Append(0u, data);

    /// <summary>Returns the CRC32C as GCS encodes it: 4 big-endian bytes, base64.</summary>
    public static string ToBase64(uint crc)
    {
        Span<byte> bytes = stackalloc byte[4];
        bytes[0] = (byte)(crc >> 24);
        bytes[1] = (byte)(crc >> 16);
        bytes[2] = (byte)(crc >> 8);
        bytes[3] = (byte)crc;
        return Convert.ToBase64String(bytes);
    }
}
