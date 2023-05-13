using System;
using System.IO;
using ProjectManager.Projects.AS3;
using System.Xml;
using ProjectManager.Actions;

namespace ExportSWC
{
    public class CompileContext
    {
        private string _projectFullPath;
        private string _flexSdkBase;
        private Version _flexSdkVersion;

        public AS3Project Project { get; set; }
        public SWCProject SwcProjectSettings { get; set; }

        public string ProjectFullPath => _projectFullPath ??= new DirectoryInfo(Project.Directory).FullName;


        /// <summary>
        /// The Flex SDK base path.
        /// </summary>
        public string FlexSdkBase => _flexSdkBase ??= BuildActions.GetCompilerPath(Project);

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

        /// <summary>
        /// The Flex SDK Version.
        /// </summary>
        public Version FlexSdkVersion
        {
            get
            {
                if (_flexSdkVersion is not null)
                {
                    return _flexSdkVersion;
                }
                else
                {
                    var doc = new XmlDocument();
                    doc.Load(Path.Combine(FlexSdkBase, "flex-sdk-description.xml"));

                    var versionNode = doc.SelectSingleNode("flex-sdk-description/version");
                    var buildNode = doc.SelectSingleNode("flex-sdk-description/build");

                    var versionParts = versionNode.InnerText.Split(new char[] { '.' },
                                                                        StringSplitOptions.RemoveEmptyEntries);

                    _flexSdkVersion = new Version(int.Parse(versionParts[0]),
                                                  int.Parse(versionParts[1]),
                                                  int.Parse(versionParts[2]),
                                                  int.Parse(buildNode.InnerText));

                    return _flexSdkVersion;
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
                if (FlexSdkVersion.Major >= 4)
                {
                    return true;
                }

                return false;
            }
        }

        public string LibMakerDir
        {
            get
            {
                var p = Path.Combine(ProjectFullPath, "obj");
                if (!Directory.Exists(p))
                {
                    Directory.CreateDirectory(p);
                }

                return p;
            }
        }

        public string CompcConfigPath_Flex => LibMakerDir + Project.Name + ".flex.compc.xml";
        public string CompcConfigPath_Flash => LibMakerDir + Project.Name + ".flash.compc.xml";
        public string CompcBinPath_Flex => Path.Combine(ProjectFullPath, SwcProjectSettings.FlexBinPath);
        public string CompcBinPath_Flash => Path.Combine(ProjectFullPath, SwcProjectSettings.FlashBinPath);
        public string MXIPath => LibMakerDir + Project.Name + ".mxi";
        public string ASIDir => Path.Combine(ProjectFullPath, "asi");
        public string SWCProjectSettingsPath => Path.Combine(ProjectFullPath, $"{Project.Name}.lxml");

        public string FlashPlayerTargetVersion => Project.MovieOptions.Version;
    }
}
