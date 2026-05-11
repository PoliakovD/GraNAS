using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace GraNAS.Desktop.App.Services.P2P;

/// <summary>Утилиты чтения файла чанками и вычисления его SHA-256 хеша.</summary>
public static class FileChunker
{
    /// <summary>Размер одного чанка при передаче файла: 64 KB.</summary>
    public const int ChunkSize = 64 * 1024; // 64 KB

    /// <summary>
    /// Асинхронно читает файл и возвращает последовательность чанков по <see cref="ChunkSize"/> байт.
    /// Последний чанк может быть меньше.
    /// </summary>
    /// <param name="filePath">Путь к файлу на диске.</param>
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

    /// <summary>
    /// Вычисляет SHA-256 хеш файла и возвращает его в виде строки hex (нижний регистр).
    /// Значение включается в <c>file_header.sha256</c> для верификации на receiver'е.
    /// </summary>
    public static async Task<string> ComputeSha256HexAsync(string filePath, CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Возвращает размер файла в байтах. Используется при формировании <c>file_header.size</c>.</summary>
    public static long GetFileSize(string filePath) => new FileInfo(filePath).Length;
}
