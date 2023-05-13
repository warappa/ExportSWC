using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ExportSWC.Resources;
using ExportSWC.Tracing;
using ExportSWC.Tracing.Interfaces;
using PluginCore;
using PluginCore.Helpers;
using PluginCore.Localization;
using PluginCore.Managers;
using ProjectManager.Controls.TreeView;
using ProjectManager.Projects.AS3;

namespace ExportSWC
{
    public partial class PluginMain : IPlugin
    {
        private string settingFilename;
        private object settingObject;
        private Image pluginImage;
        private string CurrentSWCProjectPath => GetSwcProjectSettingsPath(CurrentProject);

        /* added split button */
        private ToolStripSplitButton _button;
        /* added extra buttons */
        private ToolStripMenuItem _button_build_def;
        private ToolStripMenuItem _button_partial;
        private ToolStripMenuItem _button_compile;
        private ToolStripSeparator _button_seperator;
        private ToolStripMenuItem _button_config;


        private ICollection<GenericNode> FilesTreeView;

        /* SWC project */
        private SWCProject CurrentSwcProject;

        protected SWCBuilder _compiler = new SWCBuilder();
        protected ITraceable _tracer = new TraceManagerTracer();

        /// <summary>
        /// The current AS3 project.
        /// </summary>
        private AS3Project CurrentProject => PluginBase.CurrentProject as AS3Project;

        private DirectoryInfo CurrentProjectPath => new DirectoryInfo(CurrentProject.Directory);

        /// <summary>
        /// Name of the plugin
        /// </summary> 
        public string Name { get; } = "ExportSWC";

        /// <summary>
        /// GUID of the plugin
        /// </summary>
        public string Guid { get; } = "91cbed14-18db-11dd-9772-818b56d89593";

        /// <summary>
        /// Author of the plugin
        /// </summary> 
        public string Author { get; } = "Ali Chamas & Ben Babik & David Rettenbacher";

        /// <summary>
        /// Description of the plugin
        /// </summary> 
        public string Description { get; private set; } = "Export SWC using compc and project compiler settings.";

        /// <summary>
        /// Web address for help
        /// </summary> 
        public string Help { get; } = "www.flashdevelop.org/community/";

        /// <summary>
        /// Object that contains the settings
        /// </summary>
        [Browsable(false)]
        public object Settings => settingObject;

        public int Api => 1;

        /// <summary>
        /// Initializes the plugin
        /// </summary>
        public void Initialize()
        {
            InitializeLocalization();
            InitializeBasics();

            LoadSettings();

            AddEventHandlers();

            CreateMenuItem();
        }

        /// <summary>
        /// Disposes the plugin
        /// </summary>
        public void Dispose()
        {
            SaveSettings();
        }

        /// <summary>
        /// Handles the incoming events
        /// </summary>
        public void HandleEvent(object sender, NotifyEvent e, HandlingPriority prority)
        {
            switch (e.Type)
            {
                // Catches CurrentProject change event and display the active project path
                case EventType.Command:
                    var cmd = (e as DataEvent).Action;
                    if (cmd == "ProjectManager.Project")
                    {
                        var project = PluginBase.CurrentProject;
                        // update button when project opens / closes
                        if (project != null &&
                            project.Language.ToLower() == "as3")
                        {
                            CurrentSwcProject = SWCProject.Load(CurrentSWCProjectPath);

                            InitProjectFile(CurrentProject, CurrentSwcProject);

                            _button.Enabled = true;
                        }
                        else
                        {
                            _button.Enabled = false;
                            FilesTreeView = null;
                        }
                    }

                    if (sender?.GetType() == typeof(ProjectTreeView))
                    {
                        var tree = (ProjectTreeView)sender;
                        if (FilesTreeView == null)
                        {
                            FilesTreeView = tree.NodeMap.Values;
                        }

                        if (cmd == "ProjectManager.TreeSelectionChanged" &&
                            _button.Enabled)
                        {
                            InjectContextMenuItems(tree);
                        }
                    }
                    //If the current project isn't a AS3Project: don't try to repaint the treenodes (->exception!)
                    if (CurrentProject != null)
                    {
                        RepaintNodes();
                    }

                    break;
            }
        }

        private void InitProjectFile(AS3Project project, SWCProject swcProject)
        {
            if (swcProject.FlexBinPath == "")
            {
                swcProject.FlexBinPath = ".\\bin\\" + project.Name + ".swc";
            }

            if (swcProject.FlashBinPath == "")
            {
                swcProject.FlashBinPath = ".\\bin\\" + project.Name + ".flash.swc";
            }
        }

        /// <summary>
        /// Loads the plugin settings
        /// </summary>
        public void LoadSettings()
        {
            settingObject = new object();
        }

        /// <summary>
        /// Saves the plugin settings
        /// </summary>
        public void SaveSettings()
        {
            // noop
        }


        /// <summary>
        /// Initializes important variables
        /// </summary>
        public void InitializeBasics()
        {
            var dataPath = Path.Combine(PathHelper.DataDir, "ExportSWC");
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            settingFilename = Path.Combine(dataPath, "Settings.fdb");
            pluginImage = LocaleHelper.GetImage("icon");
        }

        /// <summary>
        /// Adds the required event handlers
        /// </summary> 
        public void AddEventHandlers()
        {
            // Set events you want to listen (combine as flags)
            EventManager.AddEventHandler(this, EventType.FileSwitch | EventType.Command);
        }

        /// <summary>
        /// Initializes the localization of the plugin
        /// </summary>
        public void InitializeLocalization()
        {
            var locale = PluginBase.MainForm.Settings.LocaleVersion;

            switch (locale)
            {
                /*
				case LocaleVersion.fi_FI : 
					// We have Finnish available... or not. :)
					LocaleHelper.Initialize(LocaleVersion.fi_FI);
					break;
				*/
                default:
                    // Plugins should default to English...
                    LocaleHelper.Initialize(LocaleVersion.en_US);
                    break;
            }

            Description = LocaleHelper.GetString("Info.Description");
        }

        private void RepaintNodes()
        {
            if (FilesTreeView == null || CurrentSwcProject == null)
            {
                return;
            }

            PaintTreeNodes(FilesTreeView);
        }

        private void PaintTreeNodes(ICollection<GenericNode> nodes)
        {
            var projPathFNm = CurrentProjectPath.FullName;
            var projPathFNmLen = projPathFNm.Length;
            string nodeBackPath;
            string nodeBackPathSub;

            foreach (var node in nodes)
            {
                nodeBackPath = node.BackingPath;

                if (nodeBackPath.Contains(projPathFNm))
                {
                    //Check if the backing path is longer than the project path...
                    if (projPathFNmLen < nodeBackPath.Length)
                    {
                        nodeBackPathSub = nodeBackPath.Substring(projPathFNmLen);

                        if (CurrentSwcProject.FlexIgnoreClasses.Contains(nodeBackPathSub))
                        {
                            node.ForeColorRequest = Color.DarkGray;
                        }

                        if (CurrentSwcProject.CS3IgnoreClasses.Contains(nodeBackPathSub))
                        {
                            node.ForeColorRequest = Color.DarkGray;
                        }
                    }

                    if (node.GetType() == typeof(TreeNode))
                    {
                        PaintTreeNodes(node.Nodes as ICollection<GenericNode>);
                    }
                }
            }
        }
    }
}
