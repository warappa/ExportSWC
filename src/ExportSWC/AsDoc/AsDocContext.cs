using System.Collections.Generic;
using System.IO;
using ExportSWC.Compiling;
using ProjectManager.Projects.AS3;

namespace ExportSWC.AsDoc
{
    internal class AsDocContext
    {
        private string? _projectFullPath;

        public AsDocContext(AS3Project project, string sdkBase, string targetVersion, Framework framework, string outputPath, string asDocConfigFilepath, List<string> ignoreClasses)
        {
            Project = project;
            SdkBase = sdkBase;
            TargetVersion = targetVersion;
            Framework = framework;
            IsAirSdk = File.Exists(Path.Combine(SdkBase, $"air-sdk-description.xml"));
            SWCOutputPath = outputPath;
            AsDocConfigFilepath = asDocConfigFilepath;
            IgnoreClasses = ignoreClasses;
            TempPath = Path.Combine(Path.GetTempPath(), framework.ToString());
        }

        public AS3Project Project { get; }

        public string SdkBase { get; }

        public string ProjectFullPath => _projectFullPath ??= new DirectoryInfo(Project.Directory).FullName;

        public bool IsAirSdk { get; }
        
        public string TargetVersion { get; }
        
        public List<string> IgnoreClasses { get; } = new List<string>();
        
        public string SWCOutputPath { get; }

        public string AsDocConfigFilepath { get; }

        public string TempPath { get; }

        public Framework Framework { get; }
    }
}
