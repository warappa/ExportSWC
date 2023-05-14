using System.Collections.Generic;
using System.IO;
using ProjectManager.Projects.AS3;

namespace ExportSWC.AsDoc
{
    internal class AsDocContext
    {
        private string _projectFullPath;

        public string FlexSdkBase { get; set; }
        public AS3Project Project { get; set; }

        public string ProjectFullPath => _projectFullPath ??= new DirectoryInfo(Project.Directory).FullName;

        public bool IsAir { get; set; }
        public string FlashPlayerTargetVersion { get; set; }
        public List<string> FlexIgnoreClasses { get; set; } = new List<string>();
        public string FlexOutputPath { get; set; }
        public string AirSdkBase { get; internal set; }
    }
}
