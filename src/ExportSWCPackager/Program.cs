using System.Diagnostics;
using System.IO.Compression;
using ExportSWCPackager;

//if (args.Length < 1)
//{
//    Console.Error.WriteLine("ERROR: Please specify a working directory where the input files can be found");
//    return -1;
//}

//var workingDirectory = args[0];

var allFound = CheckFile("ExportSWC.dll") &&
    CheckFile("README.md") &&
    CheckFile("LICENSE.txt");

if (!allFound)
{
    return -1;
}

var fdzFilename = "ExportSWC.fdz";
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
