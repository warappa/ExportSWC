using System;
using System.IO;
using ProjectManager.Projects.AS3;
using System.Xml;
using ProjectManager.Actions;
using ExportSWC.Options;

namespace ExportSWC.Compiling
{
    internal class CompileContext
    {
        private string _projectFullPath;
        private string _sdkBase;
        private string _objDirectory;
        private Version _sdkVersion;

        public AS3Project Project { get; set; }
        public SWCProject SwcProjectSettings { get; set; }

        public string ProjectFullPath => _projectFullPath ??= new DirectoryInfo(Project.Directory).FullName;


        /// <summary>
        /// The SDK base path.
        /// </summary>
        public string SdkBase => _sdkBase ??= BuildActions.GetCompilerPath(Project);

        public bool IsAir => ExtractPlatform() == "AIR";

        public virtual string ExtractPlatform()
        {
            // expected from project manager: "Flash Player;9.0;path;path..."
            var platform = Project.MovieOptions.Platform;
            var exPath = platform ?? "";
            if (exPath.Length > 0)
            {
                var p = exPath.IndexOf(';');
                if (p >= 0)
                {
                    platform = exPath.Substring(0, p);
                }
            }

            return platform;
        }

        public bool IsAirSdk()
        {
            return File.Exists(Path.Combine(SdkBase, "air-sdk-description.xml"));
        }

        /// <summary>
        /// The SDK Version.
        /// </summary>
        public Version? SdkVersion
        {
            get
            {
                if (_sdkVersion is not null)
                {
                    return _sdkVersion;
                }
                else
                {
                    var discriminator = IsAir ? "air" : "flex";

                    var doc = new XmlDocument();
                    doc.Load(Path.Combine(SdkBase, $"{discriminator}-sdk-description.xml"));

                    var versionNode = doc.SelectSingleNode($"{discriminator}-sdk-description/version");
                    var buildNode = doc.SelectSingleNode($"{discriminator}-sdk-description/build");

                    var versionParts = versionNode.InnerText.Split(new char[] { '.' },StringSplitOptions.RemoveEmptyEntries);

                    _sdkVersion = new Version(
                        int.Parse(versionParts[0]),
                        int.Parse(versionParts[1]),
                        int.Parse(versionParts[2]),
                        int.Parse(buildNode.InnerText));

                    return _sdkVersion;
                }
            }
        }

        /// <summary>
        /// Checks if AsDoc integration is available.
        /// </summary>
        public bool IsAsDocIntegrationAvailable
        {
            get
            {
                // Patch: Use custom SDK path (if available)
                if (!IsAir && SdkVersion.Major >= 4)
                {
                    return true;
                }
                else if (IsAir) //???
                {
                    return true;
                }

                return false;
            }
        }

        private string ObjDirectory
        {
            get
            {
                if(_objDirectory is not null)
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

        public string CompcConfigPathFlex => $"{ObjDirectory}{Project.Name}.flex.compc.xml";
        public string CompcConfigPathFlash => $"{ObjDirectory}{Project.Name}.flash.compc.xml";
        public string CompcOutputPathFlex => Path.Combine(ProjectFullPath, SwcProjectSettings.FlexBinPath);
        public string CompcOutputPathFlash => Path.Combine(ProjectFullPath, SwcProjectSettings.FlashBinPath);
        public string MXIPath => $"{ObjDirectory}{Project.Name}.mxi";
        public string ASIDir => Path.Combine(ProjectFullPath, "asi");
        public string SWCProjectSettingsPath => Path.Combine(ProjectFullPath, $"{Project.Name}.lxml");
        public string TargetVersion => Project.MovieOptions.Version;
    }
}
