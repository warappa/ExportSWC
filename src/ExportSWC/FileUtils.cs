using System.IO;

namespace ExportSWC
{
    internal static class FileUtils
    {
        public static FileInfo GetExeOrBatPath(string filepath)
        {
            var fileInfo = new FileInfo(filepath);
            if (!fileInfo.Exists)
            {
                var batFilepath = Path.ChangeExtension(filepath, ".bat");
                fileInfo = new FileInfo(batFilepath);
                if (!fileInfo.Exists)
                {
                    throw new FileNotFoundException($"{filepath} not found", fileInfo.FullName);
                }
            }

            return fileInfo;
        }
    }
}
