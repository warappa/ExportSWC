using System.Collections.Generic;
using System.IO;

namespace ExportSWC
{
    public static class PathUtils
    {
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
