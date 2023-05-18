using System;
using System.IO;
using ProjectManager.Projects.AS3;
using System.Xml;
using ProjectManager.Actions;
using ExportSWC.Options;
using ExportSWC.AsDoc;
using System.Collections.Generic;

namespace ExportSWC.Compiling
{
    internal class CompileContext
    {
        private string? _projectFullPath;
        private string? _sdkBase;
        private string? _objDirectory;
        private bool _isAir;
        private Version? _sdkVersion;
        private AsDocContext _asDocContext;
        private readonly Framework _framework;

        public CompileContext(AS3Project project, SWCProject swcProjectSettings, Framework framework)
        {
            Project = project;
            SwcProjectSettings = swcProjectSettings;
            _framework = framework;
            _objDirectory = PluginMain.GetObjDirectory(project);
            _isAir = File.Exists(Path.Combine(SdkBase, $"air-sdk-description.xml"));

            if (ShouldIntegrateAsDoc)
            {
                _asDocContext = new AsDocContext(
                    project,
                    SdkBase,
                    TargetVersion,
                    Framework,
                    TempCompcOutputPath,
                    AsDocConfigFilepath,
                    IgnoreClasses);
            }
        }

        public AS3Project Project { get; }

        public SWCProject SwcProjectSettings { get; }

        public Framework Framework => _framework;

        public string ProjectFullPath => _projectFullPath ??= new DirectoryInfo(Project.Directory).FullName;

        public bool ShouldIntegrateAsDoc => SwcProjectSettings.IntegrateAsDoc && IsAsDocIntegrationAvailable;

        /// <summary>
        /// The SDK base path.
        /// </summary>
        public string SdkBase => _sdkBase ??= BuildActions.GetCompilerPath(Project);

        public bool IsAirSdk => _isAir;

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
                    var discriminator = IsAirSdk ? "air" : "flex";

                    var sdkDescriptionFilepath = Path.Combine(SdkBase, $"{discriminator}-sdk-description.xml");
                    if (!File.Exists(sdkDescriptionFilepath))
                    {
                        if (IsAirSdk)
                        {
                            // try fall back to flex
                            discriminator = "flex";
                            sdkDescriptionFilepath = Path.Combine(SdkBase, $"{discriminator}-sdk-description.xml");
                        }

                        if (!File.Exists(sdkDescriptionFilepath))
                        {
                            return null;
                        }
                    }

                    var doc = new XmlDocument();
                    doc.Load(Path.Combine(SdkBase, sdkDescriptionFilepath));

                    var versionNode = doc.SelectSingleNode($"{discriminator}-sdk-description/version");
                    var buildNode = doc.SelectSingleNode($"{discriminator}-sdk-description/build");

                    var versionParts = versionNode.InnerText.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

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
                if (Framework == Framework.Flex &&
                    EnsureNotNull(SdkVersion).Major >= 4)
                {
                    return true;
                }
                else if (IsAirSdk) //???
                {
                    return true;
                }

                return false;
            }
        }

        public AsDocContext? AsDocContext => _asDocContext;

        public string CompcConfigPath => $"{_objDirectory}{Project.Name}.{_framework.ToString().ToLowerInvariant()}.compc.xml";
        public string CompcOutputPath => Path.Combine(ProjectFullPath, Framework == Framework.Flash ? SwcProjectSettings.FlashBinPath : SwcProjectSettings.FlexBinPath);
        public string AsDocConfigFilepath => $"{_objDirectory}{Project.Name}.{_framework.ToString().ToLowerInvariant()}.asdoc.xml";
        public string TempCompcOutputPath => $"{_objDirectory}temp.{_framework.ToString().ToLowerInvariant()}.compc.swc";
        public List<string> IgnoreClasses => Framework == Framework.Flash ? SwcProjectSettings.CS3IgnoreClasses : SwcProjectSettings.FlexIgnoreClasses;
        public string MXIPath => $"{_objDirectory}{Project.Name}.mxi";
        public string ASIDir => Path.Combine(ProjectFullPath, "asi");
        public string SWCProjectSettingsPath => Path.Combine(ProjectFullPath, $"{Project.Name}{ExportSWCConstants.SwcConfigFileExentions}");
        public string TargetVersion => Project.MovieOptions.Version;

        public static bool GetIsAir(AS3Project project)
        {
            var platform = ExtractPlatform(project);
            if (platform == "air")
            {
                return true;
            }

            return false;
        }

        private static string? ExtractPlatform(AS3Project project)
        {
            // expected from project manager: "Flash Player;9.0;path;path..."
            var platform = project.MovieOptions.Platform;
            var exPath = platform ?? "";
            if (exPath.Length > 0)
            {
                var p = exPath.IndexOf(';');
                if (p >= 0)
                {
                    platform = exPath.Substring(0, p);
                }
            }

            platform = platform?.ToLowerInvariant();

            return platform;
        }
    }
}
