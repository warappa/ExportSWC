using System;
using System.IO;
using System.Xml;
using ExportSWC.Tracing;
using ExportSWC.Tracing.Interfaces;
using ProjectManager.Actions;
using ProjectManager.Projects.AS3;

namespace ExportSWC
{
    public partial class SWCBuilder
    {
        private bool _anyErrors;
        private bool _running;

        private SWCProject _swcProjectSettings = null;
        private AS3Project _project = null;
        private ITraceable _tracer = null;

        private DirectoryInfo ProjectPath => new DirectoryInfo(_project.Directory);

        private string CompcBinPath_Flex => Path.Combine(ProjectPath.FullName, _swcProjectSettings.FlexBinPath);

        /// <summary>
        /// The Flex SDK base path.
        /// </summary>
        private string FlexSdkBase => BuildActions.GetCompilerPath(_project);

        /// <summary>
        /// The Flex SDK Version.
        /// </summary>
        private Version FlexSdkVersion
        {
            get
            {
                var doc = new XmlDocument();
                doc.Load(Path.Combine(FlexSdkBase, "flex-sdk-description.xml"));

                var versionNode = doc.SelectSingleNode("flex-sdk-description/version");
                var buildNode = doc.SelectSingleNode("flex-sdk-description/build");

                var versionParts = versionNode.InnerText.Split(new char[] { '.' },
                                                                    StringSplitOptions.RemoveEmptyEntries);

                var version = new Version(int.Parse(versionParts[0]),
                                              int.Parse(versionParts[1]),
                                              int.Parse(versionParts[2]),
                                              int.Parse(buildNode.InnerText));

                return version;
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

        private string LibMakerDir
        {
            get
            {
                var p = ProjectPath.FullName + "\\obj\\";
                if (!Directory.Exists(p))
                {
                    Directory.CreateDirectory(p);
                }

                return p;
            }
        }

        private void ProcessOutput(object sender, string line)
        {
            //TraceManager.AddAsync(line);
        }

        private void ProcessError(object sender, string line)
        {
            _anyErrors = true;
            //TraceManager.AddAsync(line, 3);
            WriteLine(line, TraceMessageType.Error);
        }
    }
}
