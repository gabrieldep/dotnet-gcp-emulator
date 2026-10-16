using System.Text;

namespace GcpStorageEmulator.Storage;

/// <summary>
/// Encodes GCS object names into a single, reversible, filesystem-safe path segment.
/// <para>
/// GCS object names are arbitrary UTF-8 strings and frequently contain '/', which does
/// NOT imply a directory in GCS: an object named "a" and an object named "a/b" can coexist.
/// A real filesystem cannot represent both (a cannot be a file and a directory at once), so
/// we never mirror the name as nested directories. Instead each object name is flattened to
/// one safe segment and stored as a pair of sibling files.
/// </para>
/// </summary>
public static class StorageNameEncoder
{
    /// <summary>
    /// Encodes <paramref name="name"/> keeping the unreserved set [A-Za-z0-9._-] verbatim and
    /// percent-encoding every other byte of its UTF-8 representation. The result is a single
    /// path segment (contains no '/') and round-trips through <see cref="Decode"/>.
    /// </summary>
    public static string Encode(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var sb = new StringBuilder(name.Length + 8);
        foreach (byte b in Encoding.UTF8.GetBytes(name))
        {
            if (IsUnreserved(b))
            {
                sb.Append((char)b);
            }
            else
            {
                sb.Append('%').Append(b.ToString("X2"));
            }
        }

        return sb.ToString();
    }

    /// <summary>Reverses <see cref="Encode"/>.</summary>
    public static string Decode(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        var bytes = new List<byte>(encoded.Length);
        for (int i = 0; i < encoded.Length; i++)
        {
            char c = encoded[i];
            if (c == '%' && i + 2 < encoded.Length
                && TryHex(encoded[i + 1], out int hi) && TryHex(encoded[i + 2], out int lo))
            {
                bytes.Add((byte)((hi << 4) | lo));
                i += 2;
            }
            else
            {
                bytes.Add((byte)c);
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static bool IsUnreserved(byte b) =>
        b is (>= (byte)'A' and <= (byte)'Z')
          or (>= (byte)'a' and <= (byte)'z')
          or (>= (byte)'0' and <= (byte)'9')
          or (byte)'.' or (byte)'-' or (byte)'_';

    private static bool TryHex(char c, out int value)
    {
        value = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1,
        };
        return value >= 0;
    }
}
