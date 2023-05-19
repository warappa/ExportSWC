using System.Diagnostics;
using System.IO.Compression;
using ExportSWCPackager;

if (args.Length < 1)
{
    Console.Error.WriteLine("ERROR: Please specify a compatability level (5.3.3, development)");
    return -1;
}

var compatabilityLevel = args[0];

Console.WriteLine($"FlashDevelop compatibility: {compatabilityLevel}");

var allFound = CheckFile("ExportSWC.dll") &&
    CheckFile("README.md") &&
    CheckFile("LICENSE.txt");

if (!allFound)
{
    return -1;
}

var fdzFilename = $"ExportSWC-{compatabilityLevel}.fdz";
if (File.Exists(fdzFilename))
{
    File.Delete(fdzFilename);
}
using (var fs = File.OpenWrite(fdzFilename))
{
    fs.SetLength(0);

    using var zipArchive = new ZipArchive(fs, ZipArchiveMode.Create);

    await zipArchive.WriteEntryAsync("$(BaseDir)/Plugins/ExportSWC.dll", "ExportSWC.dll");

    await zipArchive.WriteEntryAsync("$(BaseDir)/Data/ExportSWC/README.md", "README.md");

    await zipArchive.WriteEntryAsync("$(BaseDir)/Data/ExportSWC/LICENSE.txt", "LICENSE.txt");
}

return 0;

static bool CheckFile(string filename)
{
    if (!File.Exists(filename))
    {
        Console.Error.WriteLine($"ERROR: File '{filename}' not found");
        return false;
    }

    return true;
}
