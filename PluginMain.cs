using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using WeifenLuo.WinFormsUI.Docking;
using ExportSWC.Resources;
using PluginCore.Localization;
using PluginCore.Utilities;
using PluginCore.Managers;
using PluginCore.Helpers;
using PluginCore;
using AS3Context;
using ASCompletion.Context;
using System.Collections.Generic;
using ProjectManager.Projects;
using ProjectManager.Projects.AS3;
using System.Xml;
using System.Diagnostics;
using ProjectManager.Controls.TreeView;
using System.Collections;
using System.Threading;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Core;
using System.Drawing.Imaging;
using System.Text;
using ProjectManager.Helpers;
using ASCompletion.Model;
using ExportSWC.Tracing;
using ExportSWC.Tracing.Interfaces;

namespace ExportSWC
{
	public class PluginMain : IPlugin
	{
		private String pluginName = "ExportSWC";
		private String pluginGuid = "91cbed14-18db-11dd-9772-818b56d89593";
		private String pluginHelp = "www.flashdevelop.org/community/";
		private String pluginDesc = "Export SWC using compc and project compiler settings.";
		private String pluginAuth = "Ali Chamas & Ben Babik & David Rettenbacher";
		private String settingFilename;
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
		private SWCProject SwcProject;

		private bool _anyErrors;
		private bool _running;

		protected SWCCompiler _compiler = new SWCCompiler();
		protected ITraceable _tracer = new TraceManagerTracer();

		#region Required Properties

		/// <summary>
		/// Name of the plugin
		/// </summary> 
		public String Name
		{
			get { return this.pluginName; }
		}

		/// <summary>
		/// GUID of the plugin
		/// </summary>
		public String Guid
		{
			get { return this.pluginGuid; }
		}

		/// <summary>
		/// Author of the plugin
		/// </summary> 
		public String Author
		{
			get { return this.pluginAuth; }
		}

		/// <summary>
		/// Description of the plugin
		/// </summary> 
		public String Description
		{
			get { return this.pluginDesc; }
		}

		/// <summary>
		/// Web address for help
		/// </summary> 
		public String Help
		{
			get { return this.pluginHelp; }
		}

		/// <summary>
		/// Object that contains the settings
		/// </summary>
		[Browsable(false)]
		public Object Settings
		{
			get { return this.settingObject; }
		}

		#endregion

		#region Required Methods

		/// <summary>
		/// Initializes the plugin
		/// </summary>
		public void Initialize()
		{
			this.InitLocalization();
			this.InitBasics();
			this.LoadSettings();
			this.AddEventHandlers();
			this.CreateMenuItem();
		}

		/// <summary>
		/// Disposes the plugin
		/// </summary>
		public void Dispose()
		{
			this.SaveSettings();
		}

		/// <summary>
		/// Handles the incoming events
		/// </summary>
		public void HandleEvent(Object sender, NotifyEvent e, HandlingPriority prority)
		{
			switch (e.Type)
			{
				// Catches Project change event and display the active project path
				case EventType.Command:
					string cmd = (e as DataEvent).Action;
					if (cmd == "ProjectManager.Project")
					{
						IProject project = PluginBase.CurrentProject;
						// update button when project opens / closes
						if (project != null && project.Language.ToLower() == "as3")
						{
							SwcProject = SWCProject.Load(SWCProjectPath);

							if (SwcProject.FlexBinPath == "")
								SwcProject.FlexBinPath = ".\\bin\\" + project.Name + ".swc";
							if (SwcProject.FlashBinPath == "")
								SwcProject.FlashBinPath = ".\\bin\\" + project.Name + ".flash.swc";

							_button.Enabled = true;
						}
						else
						{
							_button.Enabled = false;
							FilesTreeView = null;
						}
					}
					if (sender.GetType() == typeof(ProjectTreeView))
					{
						ProjectTreeView tree = (ProjectTreeView)sender;
						if (FilesTreeView == null)
							FilesTreeView = tree.NodeMap.Values;
						if (cmd == "ProjectManager.TreeSelectionChanged" && _button.Enabled)
							InjectContextMenuItems(tree, (ArrayList)(e as DataEvent).Data);
					}
					//If the current project isn't a AS3Project: don't try to repaint the treenodes (->exception!)
					if (Project != null)
						RepaintNodes();
					break;
			}
		}

		private void RepaintNodes()
		{
			if (FilesTreeView == null || SwcProject == null)
				return;
			PaintTreeNodes(FilesTreeView);
		}

		private void PaintTreeNodes(ICollection<GenericNode> nodes)
		{
			foreach (GenericNode node in nodes)
			{
				if (node.BackingPath.Contains(ProjectPath.FullName))
				{
					//Check if the backing path is longer than the project path...
					if (ProjectPath.FullName.Length < node.BackingPath.Length)
					{
						if (SwcProject.Flex_IgnoreClasses.Contains(node.BackingPath.Substring(ProjectPath.FullName.Length)))
							node.ForeColorRequest = Color.DarkGray;
						if (SwcProject.CS3_IgnoreClasses.Contains(node.BackingPath.Substring(ProjectPath.FullName.Length)))
							node.ForeColorRequest = Color.DarkGray;
					}
					if (node.GetType() == typeof(TreeNode))
						PaintTreeNodes(((TreeNode)node).Nodes as ICollection<GenericNode>);
				}
			}
		}

		/// <summary>
		/// Loads the plugin settings
		/// </summary>
		public void LoadSettings()
		{
			this.settingObject = new Settings();
			if (!File.Exists(this.settingFilename)) this.SaveSettings();
			else
			{
				Object obj = ObjectSerializer.Deserialize(this.settingFilename, this.settingObject);
				this.settingObject = (Settings)obj;
			}
		}

		/// <summary>
		/// Saves the plugin settings
		/// </summary>
		public void SaveSettings()
		{
			ObjectSerializer.Serialize(this.settingFilename, this.settingObject);
		}

		#endregion

		

		#region context menu
		private void InjectContextMenuItems(ProjectTreeView tree, ArrayList te)
		{
			// we're only interested in single items
			if (tree.SelectedNodes.Count == 1)
			{
				GenericNode node = tree.SelectedNode;

				if (node.BackingPath.Length <= ProjectPath.FullName.Length)
					return;

				string nodeRelative = GetRelativePath(ProjectPath.FullName, node.BackingPath);
				if (nodeRelative == null)
					nodeRelative = node.BackingPath;
				nodeRelative = nodeRelative.ToLower();

				// as3 file
				if (Path.GetExtension(node.BackingPath).ToLower() == ".as" ||
					Path.GetExtension(node.BackingPath).ToLower() == ".mxml")
				{
					/* cs3 ignore item */
					ToolStripMenuItem ignoreCs3 = new ToolStripMenuItem("Exclude from CS3 SWC");
					ignoreCs3.CheckOnClick = true;
					ignoreCs3.Tag = node;
					ignoreCs3.Checked = SwcProject.CS3_IgnoreClasses.Contains(nodeRelative);
					ignoreCs3.CheckedChanged += new EventHandler(ignoreCs3_CheckedChanged);

					/* flex ignore item */
					ToolStripMenuItem ignoreFlex = new ToolStripMenuItem("Exclude from Flex SWC");
					ignoreFlex.CheckOnClick = true;
					ignoreFlex.Tag = node;
					ignoreFlex.Checked = SwcProject.Flex_IgnoreClasses.Contains(nodeRelative);
					ignoreFlex.CheckedChanged += new EventHandler(ignoreFlex_CheckedChanged);

					tree.ContextMenuStrip.Items.Add(new ToolStripSeparator());
					tree.ContextMenuStrip.Items.Add(ignoreCs3);
					tree.ContextMenuStrip.Items.Add(ignoreFlex);
				}

				// as3 project file
				if (Path.GetExtension(node.BackingPath).ToLower() == ".as3proj")
				{

				}
			}
			//tree.ContextMenuStrip.Items.Add(new ToolStripSeparator());
		}

		void ignoreFlex_CheckedChanged(object sender, EventArgs e)
		{
			ToolStripMenuItem exBtn = (ToolStripMenuItem)sender;
			GenericNode node = (GenericNode)exBtn.Tag;

			string nodePath = GetRelativePath(ProjectPath.FullName, node.BackingPath);

			SwcProject.Flex_IgnoreClasses.Remove(nodePath);

			if (exBtn.Checked)
			{
				SwcProject.Flex_IgnoreClasses.Add(nodePath);
				node.ForeColorRequest = Color.DarkGray;
			}
			else
			{
				if (!IsFileIgnored(nodePath, SwcProject.CS3_IgnoreClasses))
					node.ForeColorRequest = Color.Black;
			}
			SwcProject.Save(SWCProjectPath);
		}

		void ignoreCs3_CheckedChanged(object sender, EventArgs e)
		{
			ToolStripMenuItem exBtn = (ToolStripMenuItem)sender;
			GenericNode node = (GenericNode)exBtn.Tag;
			string nodePath = GetRelativePath(ProjectPath.FullName, node.BackingPath);

			SwcProject.CS3_IgnoreClasses.Remove(nodePath);

			if (exBtn.Checked)
			{
				SwcProject.CS3_IgnoreClasses.Add(nodePath);
				node.ForeColorRequest = Color.DarkGray;
			}
			else
			{
				if (!IsFileIgnored(nodePath, SwcProject.Flex_IgnoreClasses))
					node.ForeColorRequest = Color.Black;
			}
			SwcProject.Save(SWCProjectPath);
		}

		#endregion

		#region Custom Methods

		/// <summary>
		/// Initializes important variables
		/// </summary>
		public void InitBasics()
		{
			String dataPath = Path.Combine(PathHelper.DataDir, "ExportSWC");
			if (!Directory.Exists(dataPath))
				Directory.CreateDirectory(dataPath);
			this.settingFilename = Path.Combine(dataPath, "Settings.fdb");
			this.pluginImage = LocaleHelper.GetImage("icon");
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
			LocaleVersion locale = PluginBase.MainForm.Settings.LocaleVersion;

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
			this.pluginDesc = LocaleHelper.GetString("Info.Description");


		}

		#region Toolstrip
		/// <summary>
		/// Creates a menu item for the plugin and adds a ignored key
		/// </summary>
		public void CreateMenuItem()
		{
			//ToolStripMenuItem viewMenu = (ToolStripMenuItem)PluginBase.MainForm.FindMenuItem("ViewMenu");
			//viewMenu.DropDownItems.Add(new ToolStripMenuItem(LocaleHelper.GetString("Label.ViewMenuItem"), this.pluginImage, new EventHandler(Configure)));

			IMainForm mainForm = PluginBase.MainForm;
			// TODO localise button labels
			// toolbar items
			ToolStrip toolStrip = mainForm.ToolStrip;
			if (toolStrip != null)
			{
				toolStrip.Items.Add(new ToolStripSeparator());
				//_button = new ToolStripButton(LocaleHelper.GetImage("icon"));
				_button = new ToolStripSplitButton(LocaleHelper.GetImage("icon"));
				_button.ToolTipText = LocaleHelper.GetString("Label.PluginButton");
				_button.Enabled = false;
				_button.ButtonClick += new EventHandler(Build);
				/* add main button */
				toolStrip.Items.Add(_button);
				/* add menu items */
				/* build */
				_button_build_def = new ToolStripMenuItem();
				_button_build_def.Text = "Build All";
				_button_build_def.ToolTipText = LocaleHelper.GetString("Label.PluginButton");
				_button_build_def.Font = new Font(_button_build_def.Font, FontStyle.Bold);
				_button_build_def.Click += new EventHandler(Build);
				_button.DropDown.Items.Add(_button_build_def);
				/* meta */
				_button_partial = new ToolStripMenuItem();
				_button_partial.Text = "Prebuild Meta";
				_button_partial.ToolTipText = LocaleHelper.GetString("Label.PartialBuildButton");
				_button_partial.Click += new EventHandler(PreBuildClick);
				_button.DropDown.Items.Add(_button_partial);
				/* compile */
				_button_compile = new ToolStripMenuItem();
				_button_compile.Text = "Compile Targets";
				_button_compile.ToolTipText = LocaleHelper.GetString("Label.CompileButton");
				_button_compile.Click += new EventHandler(CompileClick);
				_button.DropDown.Items.Add(_button_compile);
				/* splitter */
				_button_seperator = new ToolStripSeparator();
				_button.DropDown.Items.Add(_button_seperator);
				/* configure */
				_button_config = new ToolStripMenuItem();
				_button_config.Text = "Configure";
				_button_config.ToolTipText = LocaleHelper.GetString("Label.Configure");
				_button_config.Click += new EventHandler(Configure);
				_button.DropDown.Items.Add(_button_config);
			}
		}

		

		void Configure(object sender, EventArgs e)
		{
			DialogResult dr = ProjectOptions.ShowDialog(SwcProject);
			if (dr == DialogResult.OK)
				SwcProject.Save(SWCProjectPath);
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
			_compiler.Build(Project, SwcProject, new TraceManagerTracer());
		}

		protected void PreBuildClick(object sender, EventArgs e)
		{
			_compiler.PreBuild(Project, SwcProject, _tracer);
		}

		protected void CompileClick(object sender, EventArgs e)
		{
			_compiler.Compile(Project, SwcProject, _tracer);
		}

		#endregion

		#region Helper Functions
		protected string GetRelativePath(string rootPath, string targetPath)
		{
			int i, k, j, count;
			rootPath = GetProjectItemFullPath(rootPath).ToLower();
			targetPath = GetProjectItemFullPath(targetPath).ToLower();

			string[] strsRoot = rootPath.Split(new char[] { '\\' });
			string[] strsTarget = targetPath.Split(new char[] { '\\' });

			for (i = strsRoot.Length; i > 0; i--)
			{
				string tmpPath = "";
				for (j = 0; j < i; j++)
					tmpPath += strsRoot[j] + "\\";

				tmpPath = tmpPath.Substring(0, tmpPath.Length - 1);

				if (targetPath.Contains(tmpPath))
				{
					tmpPath += "\\";
					count = 0;

					for (k = i, count = 0; k < strsRoot.Length; k++, count++)
					{
						if (tmpPath == rootPath)
							break;

						tmpPath += strsRoot[k];
					}

					tmpPath = "";
					for (k = 0; k < count; k++)
					{
						tmpPath += "..\\";
					}

					for (k = i; k < strsTarget.Length; k++)
						tmpPath += strsTarget[k] + '\\';
					tmpPath = tmpPath.Substring(0, tmpPath.Length - 1);

					return tmpPath;
				}
			}

			return null;


		}

		protected bool IsFileIgnored(string file, List<string> classExclusions)
		{
			string filePath = GetProjectItemFullPath(file);

			if (classExclusions.Contains(filePath.ToLower()))
				return true;
			return false;
		}

		private string GetProjectItemFullPath(string path)
		{
			if (Path.IsPathRooted(path))
				return path;

			return Path.GetFullPath(ProjectPath.FullName + "\\" + path);
		}
		#endregion


		#region properties
		private string CompcConfigPath_Flex
		{
			get { return LibMakerDir + Project.Name + ".flex.compc.xml"; }
		}

		private string CompcBinPath_Flex
		{
			//get { return LibMakerDir + Project.Name + ".flex.swc"; }
			get { return Path.Combine(ProjectPath.FullName, SwcProject.FlexBinPath); }
		}

		private string CompcConfigPath_Flash
		{
			get { return LibMakerDir + Project.Name + ".flash.compc.xml"; }
		}

		private string CompcBinPath_Flash
		{
			//get { return LibMakerDir + Project.Name + ".flash.swc"; }
			get { return Path.Combine(ProjectPath.FullName, SwcProject.FlashBinPath); }
		}

		private string ASIDir
		{
			get { return ProjectPath.FullName + "\\asi\\"; }
		}

		private string MXIPath
		{
			get { return LibMakerDir + Project.Name + ".mxi"; }
		}

		private string SWCProjectPath
		{
			//get { return ProjectPath.FullName + "\\" + Project.Name + ".lxml"; }
			get { return ProjectPath.FullName + "\\SWCSettings.lxml"; }
		}

		/// <summary>
		/// The current AS3 project.
		/// </summary>
		private AS3Project Project
		{
			get { return PluginBase.CurrentProject as AS3Project; }
		}

		/// <summary>
		/// The Flex SDK base path.
		/// </summary>
		private string FlexSdkBase
		{
			get { return (string)AS3Context.PluginMain.Settings.FlexSDK.Clone(); }
		}

		private DirectoryInfo ProjectPath
		{
			get { return new DirectoryInfo(Project.Directory); }
		}

		private string LibMakerDir
		{
			get
			{
				string p = ProjectPath.FullName + "\\obj\\";
				if (!Directory.Exists(p))
					Directory.CreateDirectory(p);
				return p;
			}
		}

		#endregion

		
		#endregion

	}
}