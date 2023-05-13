using System.Collections.Generic;
using System.IO;
using ProjectManager.Projects.AS3;

namespace ExportSWC
{
    public partial class PluginMain
    {
        protected string GetRelativePath(string rootPath, string targetPath)
        {
            int i, k, j, count;
            rootPath = GetProjectItemFullPath(rootPath).ToLower();
            targetPath = GetProjectItemFullPath(targetPath).ToLower();

            var strsRoot = rootPath.Split(new char[] { '\\' });
            var strsTarget = targetPath.Split(new char[] { '\\' });

            for (i = strsRoot.Length; i > 0; i--)
            {
                var tmpPath = "";
                for (j = 0; j < i; j++)
                {
                    tmpPath += strsRoot[j] + "\\";
                }

                if ((targetPath + "\\").Contains(tmpPath))
                {
                    tmpPath = tmpPath.Substring(0, tmpPath.Length - 1);

                    tmpPath += "\\";
                    count = 0;

                    for (k = i, count = 0; k < strsRoot.Length; k++, count++)
                    {
                        if (tmpPath == rootPath)
                        {
                            break;
                        }

                        tmpPath += strsRoot[k];
                    }

                    tmpPath = "";
                    for (k = 0; k < count; k++)
                    {
                        tmpPath += "..\\";
                    }

                    for (k = i; k < strsTarget.Length; k++)
                    {
                        tmpPath += strsTarget[k] + '\\';
                    }

                    tmpPath = tmpPath.Substring(0, tmpPath.Length - 1);

                    return tmpPath;
                }
            }

            return null;

        }

        protected bool IsFileIgnored(string file, List<string> classExclusions)
        {
            var filePath = GetProjectItemFullPath(file);

            if (classExclusions.Contains(filePath.ToLower()))
            {
                return true;
            }

            return false;
        }

        private string GetProjectItemFullPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.GetFullPath(CurrentProjectPath.FullName + "\\" + path);
        }
            
        private SWCProject GetSwcProjectSettings(AS3Project as3Project)
        {
            var swcProject = SWCProject.Load(GetSwcProjectSettingsPath(as3Project));

            InitProjectFile(as3Project, swcProject);

            return swcProject;
        }

        private string GetSwcProjectSettingsPath(AS3Project as3Project)
        {
            return new DirectoryInfo(as3Project.Directory).FullName + "\\" + as3Project.Name + ".lxml";
        }
    }
}
