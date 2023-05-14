using System.Collections.Generic;
using System.IO;
using ProjectManager.Projects.AS3;

namespace ExportSWC.AsDoc
{
    internal class AsDocContext
    {
        private string? _projectFullPath;

        public AsDocContext(AS3Project project, string sdkBase, string targetVersion, bool isAir, string outputPath, List<string> flexIgnoreClasses)
        {
            Project = project;
            SdkBase = sdkBase;
            TargetVersion = targetVersion;
            IsAir = isAir;
            FlexOutputPath = outputPath;
            FlexIgnoreClasses = flexIgnoreClasses;
        }

        public AS3Project Project { get; }
        
        public string SdkBase { get; }

        public string ProjectFullPath => _projectFullPath ??= new DirectoryInfo(Project.Directory).FullName;

        public bool IsAir { get; }
        
        public string TargetVersion { get; }
        
        public List<string> FlexIgnoreClasses { get; } = new List<string>();
        
        public string FlexOutputPath { get; }
    }
}
