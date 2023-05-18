using System.IO.Compression;

namespace ExportSWCPackager;

public static class ZipArchiveExtensions
{
    public static async Task<ZipArchiveEntry> WriteEntryAsync(this ZipArchive zipArchive, string zipPath, string inputFilepath)
    {
        var entry = zipArchive.CreateEntry(zipPath);
        using (var entryStream = entry.Open())
        {

            using var inputStream = File.OpenRead(inputFilepath);
            await inputStream.CopyToAsync(entryStream);
        }

        return entry;
    }
}
