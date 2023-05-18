using System.Collections.Generic;
using System.IO;
using ExportSWC.Options;
using ProjectManager.Projects.AS3;

namespace ExportSWC.AsDoc
{
    internal class AsDocContext
    {
        private string? _projectFullPath;
        private string _objDirectory;

        public AsDocContext(AS3Project project, SWCProject swcProjectSettings, string sdkBase, string targetVersion, bool isAir, string outputPath, List<string> flexIgnoreClasses)
        {
            Project = project;
            SwcProjectSettings = swcProjectSettings;
            SdkBase = sdkBase;
            TargetVersion = targetVersion;
            IsAir = isAir;
            FlexOutputPath = outputPath;
            FlexIgnoreClasses = flexIgnoreClasses;
        }

        public AS3Project Project { get; }

        public SWCProject SwcProjectSettings { get; }

        public string SdkBase { get; }

        public string ProjectFullPath => _projectFullPath ??= new DirectoryInfo(Project.Directory).FullName;

        public bool IsAir { get; }
        
        public string TargetVersion { get; }
        
        public List<string> FlexIgnoreClasses { get; } = new List<string>();
        
        public string FlexOutputPath { get; }

        private string ObjDirectory
        {
            get
            {
                if (_objDirectory is not null)
                {
                    return _objDirectory;
                }

                _objDirectory = $@"{Path.Combine(ProjectFullPath, "obj")}\";
                if (!Directory.Exists(_objDirectory))
                {
                    Directory.CreateDirectory(_objDirectory);
                }

                return _objDirectory;
            }
        }

        public string AsDocConfigPath => $"{ObjDirectory}{Project.Name}.flex.asdoc.xml";
    }
}
