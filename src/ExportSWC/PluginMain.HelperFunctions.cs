using System.Diagnostics.CodeAnalysis;
using System.IO;
using ExportSWC.Options;
using ExportSWC.Utils;
using ProjectManager.Projects;
using ProjectManager.Projects.AS3;

namespace ExportSWC
{
    public partial class PluginMain
    {
        internal static bool IsReleaseBuild(Project project)
        {
            return !ProjectManager.PluginMain.Settings.GetPrefs(project).DebugMode;
        }

        private string? GetRelativePath(string rootPath, string targetPath)
        {
            EnsureNotNull(CurrentProjectPath);

            int i, k, j, count;
            rootPath = PathUtils.GetProjectItemFullPath(CurrentProjectPath.FullName, rootPath).ToLower();
            targetPath = PathUtils.GetProjectItemFullPath(CurrentProjectPath.FullName, targetPath).ToLower();

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

        private SWCProject GetSwcProjectSettings(AS3Project as3Project)
        {
            EnsureNotNull(as3Project);

            var swcProject = SWCProject.Load(GetSwcProjectSettingsPath(as3Project));

            InitProjectFile(as3Project, swcProject);

            return swcProject;
        }

        [return: NotNullIfNotNull(nameof(as3Project))]
        private string? GetSwcProjectSettingsPath(AS3Project? as3Project)
        {
            if (as3Project is null)
            {
                return null;
            }

            return $@"{new DirectoryInfo(as3Project.Directory).FullName}\{as3Project.Name}.lxml";
        }
    }
}
