using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;
using ExportSWC.Compiling;
using ExportSWC.Options;
using ExportSWC.Resources;
using ExportSWC.Tracing;
using ExportSWC.Tracing.Interfaces;
using PluginCore;
using PluginCore.Helpers;
using PluginCore.Localization;
using PluginCore.Managers;
using PluginCore.Utilities;
using ProjectManager;
using ProjectManager.Actions;
using ProjectManager.Controls.TreeView;
using ProjectManager.Projects.AS3;

namespace ExportSWC
{
    public partial class PluginMain : IPlugin
    {
        private string _settingsFilename = null!;
        private ExportSWCSettings _settingsObject;
        private string? CurrentSWCProjectPath => GetSwcProjectSettingsPath(CurrentProject);

        /* added split button */
        private ToolStripSplitButton _button = null!;
        /* added extra buttons */
        private ToolStripMenuItem _button_build_def = null!;
        private ToolStripMenuItem _button_partial = null!;
        private ToolStripMenuItem _button_compile = null!;
        private ToolStripSeparator _button_seperator = null!;
        private ToolStripMenuItem _button_config = null!;
        private ToolStripMenuItem _button_override_default_build_command = null!;

        private ICollection<GenericNode>? FilesTreeView;

        /* SWC project */
        private SWCProject? CurrentSwcProject;
        private DataContractJsonSerializer _settingsSerializer = new DataContractJsonSerializer(typeof(ExportSWCSettings));
        private readonly ITraceable _tracer;
        private readonly SWCBuilder _compiler;

        /// <summary>
        /// The current AS3 project.
        /// </summary>
        private AS3Project? CurrentProject => PluginBase.CurrentProject as AS3Project;

        private DirectoryInfo? CurrentProjectPath => CurrentProject is null ? null : new DirectoryInfo(CurrentProject.Directory);

        public PluginMain()
        {
            _tracer = new TraceManagerTracer();
            _compiler = new SWCBuilder(_tracer);
            _settingsObject = new ExportSWCSettings();
        }

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
        public object Settings => _settingsObject;

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
                    var dataEvent = (DataEvent)e;
                    var cmd = dataEvent.Action;
                    if (cmd == ProjectManagerEvents.Project)
                    {
                        var project = PluginBase.CurrentProject;
                        // update button when project opens / closes
                        if (project != null &&
                            project.Language.ToLower() == "as3")
                        {
                            LoadSWCProject();
                        }
                        else
                        {
                            UnloadSWCProject();
                        }
                    }
                    else if (_settingsObject.OverrideBuildCommand &&
                        cmd == ProjectManagerEvents.BuildProject)
                    {
                        if (CurrentSwcProject is not null &&
                            CurrentProject is not null)
                        {
                            e.Handled = true;

                            Build(null, null);
                        }
                    }
                    else if (cmd == ProjectManagerEvents.FileMoved)
                    {
                        var data = (Hashtable)dataEvent.Data;
                        var oldFullFilepath = (string)data["fromPath"];
                        var newFullFilename = (string)data["toPath"];

                        var oldExtension = Path.GetExtension(oldFullFilepath).ToLowerInvariant();
                        var newExtension = Path.GetExtension(newFullFilename).ToLowerInvariant();

                        if (oldExtension != newExtension)
                        {
                            if (oldExtension == ".lxml")
                            {
                                UnloadSWCProject();
                            }
                            else if (newExtension == ".lxml")
                            {
                                LoadSWCProject();
                            }

                            UpdateToolstrip();
                        }
                    }

                    if (sender?.GetType() == typeof(ProjectTreeView))
                    {
                        var tree = (ProjectTreeView)sender;

                        FilesTreeView ??= tree.NodeMap.Values;

                        if (cmd == ProjectManagerEvents.TreeSelectionChanged &&
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

        private void LoadSWCProject()
        {
            CurrentSwcProject = SWCProject.Load(CurrentSWCProjectPath!);

            InitProjectFile(CurrentProject!, CurrentSwcProject!);

            UpdateToolstrip();
        }

        private void UnloadSWCProject()
        {
            CurrentSwcProject = null;
            FilesTreeView = null;

            UpdateToolstrip();
        }

        private void InitProjectFile(AS3Project project, SWCProject swcProject)
        {
            if (swcProject is null)
            {
                return;
            }

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
        private void LoadSettings()
        {
            if (!File.Exists(_settingsFilename))
            {
                SaveSettings();
            }
            else
            {
                _settingsObject = new ExportSWCSettings();
                try
                {
                    if (File.Exists(_settingsFilename))
                    {
                        using var stream = File.Open(_settingsFilename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                        _settingsObject = (ExportSWCSettings)_settingsSerializer.ReadObject(stream);
                    }
                }
                catch (Exception exc)
                {
                    ErrorManager.ShowError("Could not read ExportSWC settings file", exc);
                }
                //_settingsObject = ObjectSerializer.Deserialize<ExportSWCSettings>(_settingsFilename, _settingsObject);
            }
        }

        /// <summary>
        /// Saves the plugin settings
        /// </summary>
        private void SaveSettings()
        {
            using var stream = File.Open(_settingsFilename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            stream.SetLength(0);
            _settingsSerializer.WriteObject(stream, Settings);
            //ObjectSerializer.Serialize(_settingsFilename, Settings);
        }

        /// <summary>
        /// Initializes important variables
        /// </summary>
        private void InitializeBasics()
        {
            var dataPath = Path.Combine(PathHelper.DataDir, "ExportSWC");
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            _settingsFilename = Path.Combine(dataPath, "Settings.fdb");
            // pluginImage = LocaleHelper.GetImage("icon");
        }

        /// <summary>
        /// Adds the required event handlers
        /// </summary> 
        private void AddEventHandlers()
        {
            // Set events you want to listen (combine as flags)
            EventManager.AddEventHandler(this, EventType.FileSwitch | EventType.Command, HandlingPriority.High);
        }

        /// <summary>
        /// Initializes the localization of the plugin
        /// </summary>
        private void InitializeLocalization()
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
            EnsureNotNull(CurrentSwcProject);
            EnsureNotNull(CurrentProjectPath);

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
                        PaintTreeNodes((ICollection<GenericNode>)node.Nodes);
                    }
                }
            }
        }
    }
}
