using System;
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
using PluginCore.Utilities;
using ProjectManager.Controls.TreeView;
using ProjectManager.Projects.AS3;

namespace ExportSWC
{
    public class PluginMain : IPlugin
    {
        private string settingFilename;
        private Settings settingObject;
        private Image pluginImage;

        //private ToolStripButton _button;
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

        private readonly bool _anyErrors;
        private readonly bool _running;

        protected SWCBuilder _compiler = new SWCBuilder();
        protected ITraceable _tracer = new TraceManagerTracer();

        #region Required Properties

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

        #endregion

        #region Required Methods

        /// <summary>
        /// Initializes the plugin
        /// </summary>
        public void Initialize()
        {
            InitLocalization();
            InitBasics();
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
                        if (project != null && project.Language.ToLower() == "as3")
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

                        if (cmd == "ProjectManager.TreeSelectionChanged" && _button.Enabled)
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

                        if (CurrentSwcProject.Flex_IgnoreClasses.Contains(nodeBackPathSub))
                        {
                            node.ForeColorRequest = Color.DarkGray;
                        }

                        if (CurrentSwcProject.CS3_IgnoreClasses.Contains(nodeBackPathSub))
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

        /// <summary>
        /// Loads the plugin settings
        /// </summary>
        public void LoadSettings()
        {
            settingObject = new Settings();
            if (!File.Exists(settingFilename))
            {
                SaveSettings();
            }
            else
            {
                object obj = ObjectSerializer.Deserialize(settingFilename, settingObject);
                settingObject = (Settings)obj;
            }
        }

        /// <summary>
        /// Saves the plugin settings
        /// </summary>
        public void SaveSettings()
        {
            ObjectSerializer.Serialize(settingFilename, settingObject);
        }

        #endregion

        #region context menu
        private void InjectContextMenuItems(ProjectTreeView tree)
        {
            // we're only interested in single items
            if (tree.SelectedNodes.Count == 1)
            {
                var node = tree.SelectedNode;

                if (node.BackingPath.Length <= CurrentProjectPath.FullName.Length)
                {
                    return;
                }

                var nodeRelative = GetRelativePath(CurrentProjectPath.FullName, node.BackingPath);
                if (nodeRelative == null)
                {
                    nodeRelative = node.BackingPath;
                }

                nodeRelative = nodeRelative.ToLower();

                // as3 file
                if (Path.GetExtension(node.BackingPath).ToLower() == ".as" ||
                    Path.GetExtension(node.BackingPath).ToLower() == ".mxml")
                {
                    /* cs3 ignore item */
                    var ignoreCs3 = new ToolStripMenuItem("Exclude from CS3 SWC")
                    {
                        CheckOnClick = true,
                        Tag = node,
                        Checked = CurrentSwcProject.CS3_IgnoreClasses.Contains(nodeRelative)
                    };
                    ignoreCs3.CheckedChanged += new EventHandler(ignoreCs3_CheckedChanged);

                    /* flex ignore item */
                    var ignoreFlex = new ToolStripMenuItem("Exclude from Flex SWC")
                    {
                        CheckOnClick = true,
                        Tag = node,
                        Checked = CurrentSwcProject.Flex_IgnoreClasses.Contains(nodeRelative)
                    };
                    ignoreFlex.CheckedChanged += new EventHandler(ignoreFlex_CheckedChanged);

                    tree.ContextMenuStrip.Items.Add(new ToolStripSeparator());
                    tree.ContextMenuStrip.Items.Add(ignoreCs3);
                    tree.ContextMenuStrip.Items.Add(ignoreFlex);
                }

                // as3 project file
                if (Path.GetExtension(node.BackingPath).ToLower() == ".as3proj")
                {
                    var compileToSWC = new ToolStripMenuItem("Compile to SWC")
                    {
                        Tag = node
                    };
                    compileToSWC.Click += delegate
                                              {
                                                  var proj = AS3Project.Load(node.BackingPath);

                                                  _compiler.Build(proj, GetSwcProjectSettings(proj), new TraceManagerTracer());
                                              };

                    var openSwcSettings = new ToolStripMenuItem("SWC Settings")
                    {
                        Tag = node
                    };
                    openSwcSettings.Click += delegate
                        {
                            var proj = AS3Project.Load(node.BackingPath);

                            ConfigureSwcProject(proj, GetSwcProjectSettings(proj));
                        };

                    tree.ContextMenuStrip.Items.Add(new ToolStripSeparator());
                    tree.ContextMenuStrip.Items.Add(compileToSWC);
                    tree.ContextMenuStrip.Items.Add(openSwcSettings);
                }
            }
        }

        private SWCProject GetSwcProjectSettings(AS3Project as3Project)
        {
            var swcProject = SWCProject.Load(GetSwcProjectSettingsPath(as3Project));

            InitProjectFile(as3Project, swcProject);

            return swcProject;
        }
        private string GetSwcProjectSettingsPath(AS3Project as3Project)
        {
            return new DirectoryInfo(as3Project.Directory).FullName + "\\" + as3Project.Name + ".lxml";
        }

        private void ignoreFlex_CheckedChanged(object sender, EventArgs e)
        {
            var exBtn = (ToolStripMenuItem)sender;
            var node = (GenericNode)exBtn.Tag;

            var nodePath = GetRelativePath(CurrentProjectPath.FullName, node.BackingPath);

            CurrentSwcProject.Flex_IgnoreClasses.Remove(nodePath);

            if (exBtn.Checked)
            {
                CurrentSwcProject.Flex_IgnoreClasses.Add(nodePath);
                node.ForeColorRequest = Color.DarkGray;
            }
            else
            {
                if (!IsFileIgnored(nodePath, CurrentSwcProject.CS3_IgnoreClasses))
                {
                    node.ForeColorRequest = Color.Black;
                }
            }

            CurrentSwcProject.Save(CurrentSWCProjectPath);
        }

        private void ignoreCs3_CheckedChanged(object sender, EventArgs e)
        {
            var exBtn = (ToolStripMenuItem)sender;
            var node = (GenericNode)exBtn.Tag;
            var nodePath = GetRelativePath(CurrentProjectPath.FullName, node.BackingPath);

            CurrentSwcProject.CS3_IgnoreClasses.Remove(nodePath);

            if (exBtn.Checked)
            {
                CurrentSwcProject.CS3_IgnoreClasses.Add(nodePath);
                node.ForeColorRequest = Color.DarkGray;
            }
            else
            {
                if (!IsFileIgnored(nodePath, CurrentSwcProject.Flex_IgnoreClasses))
                {
                    node.ForeColorRequest = Color.Black;
                }
            }

            CurrentSwcProject.Save(CurrentSWCProjectPath);
        }

        #endregion

        #region Custom Methods

        /// <summary>
        /// Initializes important variables
        /// </summary>
        public void InitBasics()
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

        #region ARCHIVED_CODE_UNUSED
        // not needed for now, originally had a string[] to update settings but no longer using - kept code for reference
        /*private void InitSettings() {
			dataPath = Path.Combine(PathHelper.DataDir, pluginName);
			if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);
			settingFilename = Path.Combine(dataPath, "Settings.fdb");
			settingObject = new Settings();
			if (!File.Exists(settingFilename)) {
				// default settings
				settingObject.NameSpaces = ExportSWC.Settings.DEFAULT_NAMESPACES;
				SaveSettings();
			} else {
				Object obj = ObjectSerializer.Deserialize(settingFilename, settingObject);
				settingObject = (Settings)obj;
			}
		}*/
        #endregion

        /// <summary>
        /// Initializes the localization of the plugin
        /// </summary>
        public void InitLocalization()
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

        #region Toolstrip
        /// <summary>
        /// Creates a menu item for the plugin and adds a ignored key
        /// </summary>
        public void CreateMenuItem()
        {
            //ToolStripMenuItem viewMenu = (ToolStripMenuItem)PluginBase.MainForm.FindMenuItem("ViewMenu");
            //viewMenu.DropDownItems.Add(new ToolStripMenuItem(LocaleHelper.GetString("Label.ViewMenuItem"), this.pluginImage, new EventHandler(Configure)));

            var mainForm = PluginBase.MainForm;
            // TODO localise button labels
            // toolbar items
            var toolStrip = mainForm.ToolStrip;
            if (toolStrip != null)
            {
                toolStrip.Items.Add(new ToolStripSeparator());
                //_button = new ToolStripButton(LocaleHelper.GetImage("icon"));
                _button = new ToolStripSplitButton(LocaleHelper.GetImage("icon"))
                {
                    ToolTipText = LocaleHelper.GetString("Label.PluginButton"),
                    Enabled = false
                };
                _button.ButtonClick += new EventHandler(Build);
                /* add main button */
                toolStrip.Items.Add(_button);
                /* add menu items */
                /* build */
                _button_build_def = new ToolStripMenuItem
                {
                    Text = "Build All",
                    ToolTipText = LocaleHelper.GetString("Label.PluginButton")
                };
                _button_build_def.Font = new Font(_button_build_def.Font, FontStyle.Bold);
                _button_build_def.Click += new EventHandler(Build);
                _button.DropDown.Items.Add(_button_build_def);
                /* meta */
                _button_partial = new ToolStripMenuItem
                {
                    Text = "Prebuild Meta",
                    ToolTipText = LocaleHelper.GetString("Label.PartialBuildButton")
                };
                _button_partial.Click += new EventHandler(PreBuildClick);
                _button.DropDown.Items.Add(_button_partial);
                /* compile */
                _button_compile = new ToolStripMenuItem
                {
                    Text = "Compile Targets",
                    ToolTipText = LocaleHelper.GetString("Label.CompileButton")
                };
                _button_compile.Click += new EventHandler(CompileClick);
                _button.DropDown.Items.Add(_button_compile);
                /* splitter */
                _button_seperator = new ToolStripSeparator();
                _button.DropDown.Items.Add(_button_seperator);
                /* configure */
                _button_config = new ToolStripMenuItem
                {
                    Text = "Configure",
                    ToolTipText = LocaleHelper.GetString("Label.Configure")
                };
                _button_config.Click += new EventHandler(Configure);
                _button.DropDown.Items.Add(_button_config);
            }
        }

        private void Configure(object sender, EventArgs e)
        {
            ConfigureSwcProject(CurrentProject, CurrentSwcProject);
        }

        private void ConfigureSwcProject(AS3Project project, SWCProject swcProject)
        {
            var dr = ProjectOptions.ShowDialog(swcProject, _compiler);

            if (dr == DialogResult.OK)
            {
                swcProject.Save(GetSwcProjectSettingsPath(project));
            }
        }
        #endregion

        #region Compiling

        /// <summary>
        /// Main method for plugin - Export SWC using compc.exe
        /// </summary>
        /// <param name="sender">the sender</param>
        /// <param name="e">the event args</param>
        protected void Build(object sender, System.EventArgs e)
        {
            _button.Enabled = false;

            _compiler.Build(CurrentProject, CurrentSwcProject, _tracer);

            _button.Enabled = true;
        }

        protected void PreBuildClick(object sender, EventArgs e)
        {
            _compiler.PreBuild(CurrentProject, CurrentSwcProject, _tracer);
        }

        protected void CompileClick(object sender, EventArgs e)
        {
            _compiler.Compile(CurrentProject, CurrentSwcProject, _tracer);
        }

        #endregion

        #region Helper Functions
        protected string GetRelativePath(string rootPath, string targetPath)
        {
            int i, k, j, count;
            rootPath = GetProjectItemFullPath(rootPath).ToLower();
            targetPath = GetProjectItemFullPath(targetPath).ToLower();

            var strsRoot = rootPath.Split(new char[] { '\\' });
            var strsTarget = targetPath.Split(new char[] { '\\' });

            for (i = strsRoot.Length; i > 0; i--)
            {
                var tmpPath = "";
                for (j = 0; j < i; j++)
                {
                    tmpPath += strsRoot[j] + "\\";
                }

                if ((targetPath + "\\").Contains(tmpPath))
                {
                    tmpPath = tmpPath.Substring(0, tmpPath.Length - 1);

                    tmpPath += "\\";
                    count = 0;

                    for (k = i, count = 0; k < strsRoot.Length; k++, count++)
                    {
                        if (tmpPath == rootPath)
                        {
                            break;
                        }

                        tmpPath += strsRoot[k];
                    }

                    tmpPath = "";
                    for (k = 0; k < count; k++)
                    {
                        tmpPath += "..\\";
                    }

                    for (k = i; k < strsTarget.Length; k++)
                    {
                        tmpPath += strsTarget[k] + '\\';
                    }

                    tmpPath = tmpPath.Substring(0, tmpPath.Length - 1);

                    return tmpPath;
                }
            }

            return null;

        }

        protected bool IsFileIgnored(string file, List<string> classExclusions)
        {
            var filePath = GetProjectItemFullPath(file);

            if (classExclusions.Contains(filePath.ToLower()))
            {
                return true;
            }

            return false;
        }

        private string GetProjectItemFullPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.GetFullPath(CurrentProjectPath.FullName + "\\" + path);
        }
        #endregion

        #region properties
        private string CompcConfigPath_Flex => CurrentLibMakerDir + CurrentProject.Name + ".flex.compc.xml";

        private string CompcBinPath_Flex
            //get { return CurrentLibMakerDir + CurrentProject.Name + ".flex.swc"; }
            => Path.Combine(CurrentProjectPath.FullName, CurrentSwcProject.FlexBinPath);

        private string CompcConfigPath_Flash => CurrentLibMakerDir + CurrentProject.Name + ".flash.compc.xml";

        private string CompcBinPath_Flash
            //get { return CurrentLibMakerDir + CurrentProject.Name + ".flash.swc"; }
            => Path.Combine(CurrentProjectPath.FullName, CurrentSwcProject.FlashBinPath);

        private string CurrentASIDir => CurrentProjectPath.FullName + "\\asi\\";

        private string CurrentMXIPath => CurrentLibMakerDir + CurrentProject.Name + ".mxi";

        private string CurrentSWCProjectPath => GetSwcProjectSettingsPath(CurrentProject);

        /// <summary>
        /// The current AS3 project.
        /// </summary>
        private AS3Project CurrentProject => PluginBase.CurrentProject as AS3Project;

        /// <summary>
        /// The Flex SDK base path.
        /// </summary>
        /*private string FlexSdkBase
		{
			get { return (string)AS3Context.PluginMain.Settings.FlexSDK.Clone(); }
		}*/

        private DirectoryInfo CurrentProjectPath => new DirectoryInfo(CurrentProject.Directory);

        private string CurrentLibMakerDir
        {
            get
            {
                var p = CurrentProjectPath.FullName + "\\obj\\";
                if (!Directory.Exists(p))
                {
                    Directory.CreateDirectory(p);
                }

                return p;
            }
        }

        #endregion

        #endregion

        public int Api => 1;
    }
}
