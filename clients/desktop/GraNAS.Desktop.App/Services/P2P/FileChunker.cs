using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace GraNAS.Desktop.App.Services.P2P;

public static class FileChunker
{
    public const int ChunkSize = 64 * 1024; // 64 KB

    public static async IAsyncEnumerable<byte[]> ReadChunksAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(filePath);
        var buffer = new byte[ChunkSize];
        int read;
        while ((read = await fs.ReadAsync(buffer, ct)) > 0)
        {
            var chunk = new byte[read];
            Buffer.BlockCopy(buffer, 0, chunk, 0, read);
            yield return chunk;
        }
    }

    public static async Task<string> ComputeSha256HexAsync(string filePath, CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static long GetFileSize(string filePath) => new FileInfo(filePath).Length;
}
