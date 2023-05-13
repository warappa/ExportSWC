using System.Collections.Generic;
using System.IO;

namespace ExportSWC.Utils
{
    internal static class PathUtils
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

        public static bool IsFileIgnored(string projectPath, string filepath, List<string> classExclusions)
        {
            var filePath = GetProjectItemFullPath(projectPath, filepath);

            if (classExclusions.Contains(filePath.ToLower()))
            {
                return true;
            }

            return false;
        }

        public static string GetProjectItemFullPath(string projectPath, string filepath)
        {
            if (Path.IsPathRooted(filepath))
            {
                return filepath;
            }

            return Path.GetFullPath(Path.Combine(projectPath, filepath));
        }
    }
}
