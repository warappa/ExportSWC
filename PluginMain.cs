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
                    if(Project!=null)
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

        #endregion

        private void InjectContextMenuItems(ProjectTreeView tree, ArrayList te)
        {
            // we're only interested in single items
            if (tree.SelectedNodes.Count == 1)
            {
                GenericNode node = tree.SelectedNode;

                if (node.BackingPath.Length <= ProjectPath.FullName.Length)
                    return;

                string nodeRelative = node.BackingPath.Substring(ProjectPath.FullName.Length);
                // as3 file
                if (Path.GetExtension(node.BackingPath).ToLower() == ".as")
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
            }
            //tree.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        }

        void ignoreFlex_CheckedChanged(object sender, EventArgs e)
        {
            ToolStripMenuItem exBtn = (ToolStripMenuItem)sender;
            GenericNode node = (GenericNode)exBtn.Tag;
            SwcProject.Flex_IgnoreClasses.Remove(node.BackingPath.Substring(ProjectPath.FullName.Length));
            if (exBtn.Checked)
            {
                SwcProject.Flex_IgnoreClasses.Add(node.BackingPath.Substring(ProjectPath.FullName.Length));
                node.ForeColorRequest = Color.DarkGray;
            }
            else if (!SwcProject.CS3_IgnoreClasses.Contains(node.BackingPath.Substring(ProjectPath.FullName.Length)))
                node.ForeColorRequest = Color.Black;
            SwcProject.Save(SWCProjectPath);
        }

        void ignoreCs3_CheckedChanged(object sender, EventArgs e)
        {
            ToolStripMenuItem exBtn = (ToolStripMenuItem)sender;
            GenericNode node = (GenericNode)exBtn.Tag;
            SwcProject.CS3_IgnoreClasses.Remove(node.BackingPath.Substring(ProjectPath.FullName.Length));
            if (exBtn.Checked)
            {
                SwcProject.CS3_IgnoreClasses.Add(node.BackingPath.Substring(ProjectPath.FullName.Length));
                node.ForeColorRequest = Color.DarkGray;
            }
            else if (!SwcProject.Flex_IgnoreClasses.Contains(node.BackingPath.Substring(ProjectPath.FullName.Length)))
                node.ForeColorRequest = Color.Black;
            SwcProject.Save(SWCProjectPath);
        }

        #region Custom Methods

        /// <summary>
        /// Initializes important variables
        /// </summary>
        public void InitBasics()
        {
            String dataPath = Path.Combine(PathHelper.DataDir, "ExportSWC");
            if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);
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
                _button_partial.Click += new EventHandler(PreBuild);
                _button.DropDown.Items.Add(_button_partial);
                /* compile */
                _button_compile = new ToolStripMenuItem();
                _button_compile.Text = "Compile Targets";
                _button_compile.ToolTipText = LocaleHelper.GetString("Label.CompileButton");
                _button_compile.Click += new EventHandler(Compile);
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
            if(dr==DialogResult.OK)
                SwcProject.Save(SWCProjectPath);
        }

        void Compile(object sender, EventArgs e)
        {
            bool buildSuccess = true;

            _button.Enabled = false;

            SaveModifiedDocuments();

            RunPreBuildEvent();

            buildSuccess &= RunCompc(CompcConfigPath_Flex);
            if (SwcProject.MakeCS3)
            {
                buildSuccess &= RunCompc(CompcConfigPath_Flash);
                PatchFlashSWC();
                if (SwcProject.LaunchAEM)
                    buildSuccess &= BuildMXP();
            }
            if(buildSuccess || Project.AlwaysRunPostBuild)
                RunPostBuildEvent();

            _button.Enabled = true;
        }

        private void RunPreBuildEvent()
        {
            if (Project.PreBuildEvent.Trim().Length == 0)
                return;

            Process process = new Process();
            ProcessStartInfo processStI = new ProcessStartInfo();
            processStI.FileName = "cmd.exe";
            processStI.Arguments = "/C " + Project.PreBuildEvent;
            processStI.CreateNoWindow = true;
            process.StartInfo = processStI;            
            process.Start();

            TraceManager.AddAsync("Running Pre-Build Command:\ncmd: " + Project.PreBuildEvent);

            process.WaitForExit(15000);
        }

        private void RunPostBuildEvent()
        {
            if (Project.PostBuildEvent.Trim().Length == 0)
                return;

            Process process = new Process();
            ProcessStartInfo processStI = new ProcessStartInfo();
            processStI.FileName = "cmd.exe";
            processStI.Arguments = "/C " + Project.PostBuildEvent;
            processStI.CreateNoWindow = true;
            process.StartInfo = processStI;
            process.Start();

            TraceManager.AddAsync("Running Post-Build Command:\ncmd: " + Project.PostBuildEvent);
        }

        private void SaveModifiedDocuments()
        {
            if (PluginBase.MainForm.HasModifiedDocuments == false)
                return;

            foreach (ITabbedDocument document in PluginBase.MainForm.Documents)
            {
                if (document.IsModified)
                    document.Save();
            }
        }

        private bool BuildMXP()
        {
            // throw new NotImplementedException();
            ProcessStartInfo pi = new ProcessStartInfo();
            pi.UseShellExecute = true;
            pi.FileName = MXIPath;
            Process process = Process.Start(pi);

            bool success = process.WaitForExit(15000);

            return success && (process.ExitCode == 0);
        }

        private void PatchFlashSWC()
        {
            bool livePreviewFile = false;
            string file = CompcBinPath_Flash;

            if (!File.Exists(file)) return; // TODO: display error

            FastZipEvents fze = new FastZipEvents();
            FastZip fzip = new FastZip(fze);

            string tempDir = Path.GetTempPath().Trim('\\');
            tempDir += "\\flashdevelop_swc";
            
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            fzip.ExtractZip(file, tempDir, ".*");

            XmlDocument catxml = new XmlDocument();

            bool loaded = false;
            while (loaded == false)
            {
                try
                {
                    catxml.Load(tempDir + "\\catalog.xml");
                    loaded = true;
                }
                catch
                {
                    Thread.Sleep(50);                    
                }
            }
            // <flash version="9.0" build="r494" platform="WIN" />
            XmlElement fel = CreateElement("flash", catxml.DocumentElement["versions"]);
            fel.SetAttribute("version", "9.0");
            fel.SetAttribute("build", "r494");
            fel.SetAttribute("platform", "WIN");

            // <feature-components />
            CreateElement("feature-components", catxml.DocumentElement["features"]);

            // <file path="icon_0.png" mod="1061758088000" />
            if (catxml.DocumentElement["files"] == null)
                CreateElement("files", catxml.DocumentElement, string.Empty);
            if (!SwcProject.ValidImage())
                LocaleHelper.GetImage("cs3_component_icon").Save(tempDir + "\\icon.png", ImageFormat.Png);
            else
                Image.FromFile(SwcProject.CS3_ComponentIconFile).Save(tempDir + "\\icon.png", ImageFormat.Png);
            XmlElement iel = CreateElement("file", catxml.DocumentElement["files"], string.Empty);
            iel.SetAttribute("path", "icon.png");
            iel.SetAttribute("mod", new FileInfo(tempDir + "\\icon.png").LastWriteTimeUtc.ToFileTimeUtc().ToString());

            // <component className="Symbol1" name="Symbol 1" icon="icon_0.png"  />
            if (catxml.DocumentElement["components"] == null)
                CreateElement("components", catxml.DocumentElement);
            XmlElement cel = CreateElement("component", catxml.DocumentElement["components"]);
            cel.SetAttribute("className", SwcProject.CS3_ComponentClass);
            cel.SetAttribute("name", SwcProject.CS3_ComponentName);
            cel.SetAttribute("icon", "icon.png");
            cel.SetAttribute("tooltip", SwcProject.CS3_ComponentToolTip);

            // livePreview
            if (SwcProject.ValidLivePreview())
            {
                if (SwcProject.CS3_PreviewType == SWCProject.CS3_PreviewType_ENUM.ExternalSWF)
                {
                    // livePreview exists
                    File.Copy(SwcProject.CS3_PreviewResource, tempDir + "\\livePreview.swf");
                    livePreviewFile = true;
                }
                else
                {
                    // MAKE BUILDSCRIPT
                    string lpfile = BuildLivePreview();
                    if (lpfile == string.Empty)
                    {
                        TraceManager.AddAsync("*** Error building live preview from class: " + SwcProject.CS3_PreviewResource);
                        _anyErrors = true;                        
                    }
                    else
                    {
                        if (File.Exists(tempDir + "\\livePreview.swf"))
                            File.Delete(tempDir + "\\livePreview.swf");
                        File.Move(lpfile, tempDir + "\\livePreview.swf");
                        livePreviewFile = true;
                    }
                }
                if (livePreviewFile)
                {
                    cel.SetAttribute("preview", "livePreview.swf");
                    XmlElement lpf = CreateElement("file", catxml.DocumentElement["files"]);
                    lpf.SetAttribute("path", "livePreview.swf");
                    lpf.SetAttribute("mod", new FileInfo(tempDir + "\\livePreview.swf").LastWriteTimeUtc.ToFileTimeUtc().ToString());
                }
            }

            if (!_anyErrors)
            {
                // drop digests
                try 
                {
                    catxml.DocumentElement["libraries"]["library"].RemoveChild(catxml.DocumentElement["libraries"]["library"]["digests"]); 
                }
                catch { }

                catxml.Save(tempDir + "\\catalog.xml");

                File.Delete(file);
                fzip.CreateZip(file, tempDir, false, ".*");

                using (ZipOutputStream zo = new ZipOutputStream(File.Create(file)))
                {
                    zo.UseZip64 = UseZip64.Off;
                    zo.SetLevel(9);
                    SwcAdd(zo, tempDir + "\\catalog.xml");
                    SwcAdd(zo, tempDir + "\\library.swf");
                    SwcAdd(zo, tempDir + "\\icon.png");
                    if (livePreviewFile)
                        SwcAdd(zo, tempDir + "\\livePreview.swf");
                    zo.Finish();
                    zo.Close();
                }
                Directory.Delete(tempDir, true);
                
                TraceManager.AddAsync("Flash SWC ready: " + file);
            }
        }

        private string BuildLivePreview()
        {
            if (FindClassPath(SwcProject.CS3_PreviewResource) == string.Empty)
                return string.Empty;
                        
            //string tproFile = TempFile(Path.GetTempPath().Trim('\\') + "\\flashdevelop_swf\\", ".as3proj");
            //string tswfFile = TempFile(Path.GetTempPath().Trim('\\') + "\\flashdevelop_swf\\", ".swf"); 
            string tproFile = TempFile(ProjectPath.FullName, ".as3proj");
            string tswfFile = TempFile(ProjectPath.FullName, ".swf");

            XmlDocument lpc = new XmlDocument();
            lpc.Load(Project.ProjectPath);
            XmlNodeList opnl = lpc.DocumentElement["output"].ChildNodes;
            foreach (XmlNode node in opnl)
                if (node.Attributes[0].Name == "path")
                    node.Attributes[0].Value = Path.GetFileName(tswfFile);

            lpc.DocumentElement["compileTargets"].RemoveAll();
            XmlElement el = CreateElement("compile", lpc.DocumentElement["compileTargets"]);
            el.SetAttribute("path", FindClassPath(SwcProject.CS3_PreviewResource));

            lpc.Save(tproFile);

            string fdBuildDir = Path.Combine(PathHelper.ToolDir, "fdbuild");
            string fdBuildPath = Path.Combine(fdBuildDir, "fdbuild.exe");

            string arguments = "\"" + tproFile + "\"";

            arguments += " -library \"" + PathHelper.LibraryDir + "\"";
            arguments += " -compiler \"" + FlexSdkBase + "\"";

            ProcessRunner fdp = new ProcessRunner();
            fdp.Run(fdBuildPath, arguments);

            while (fdp.IsRunning) { Thread.Sleep(20); Application.DoEvents(); }

            File.Delete(tproFile);
            string tcon = Project.Directory.TrimEnd('\\') + "\\obj\\" + Path.GetFileNameWithoutExtension(tproFile) + "Config.xml";
            if (File.Exists(tcon))
                File.Delete(tcon);
            return tswfFile;
        }

        private string FindClassPath(string className)
        {
            foreach (string cpath_ in Project.Classpaths)
            {

                string cpath = cpath_.Contains(":") ? cpath_ : Path.Combine(Project.Directory, cpath_);
                string[] files = Directory.GetFiles(cpath, "*.as", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    if (Path.GetFileNameWithoutExtension(file) == className)
                        return (Path.GetDirectoryName(file).TrimEnd('\\')
                            + "\\" + className + ".as")
                            .Replace(Project.Directory.TrimEnd('\\')
                            + "\\", "");
                }
            }
            return string.Empty;
        }

        private string TempFile(string path, string ext)
        {
            path = path.TrimEnd('\\') + "\\";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            string output = "";
            byte[] ten = new byte[10];
        
            output += new Random().Next(0, 10).ToString();

            while (File.Exists(path + output + ext))
                output += new Random().Next(0, 10).ToString();

            File.Create(path + output + ext).Close();
            return path + output + ext;
        }

        private void SwcAdd(ZipOutputStream str, string file)
        {
            ZipEntry entry = new ZipEntry(file.Substring(file.LastIndexOf('\\') + 1));
            entry.CompressionMethod = CompressionMethod.Deflated;
            byte[] buf = new byte[8192];
            str.PutNextEntry(entry);
            using (FileStream fstr = File.OpenRead(file))
            {
                int c;
                do
                {
                    c = fstr.Read(buf, 0, buf.Length);
                    str.Write(buf, 0, c);
                } while (c > 0);
            }
        }

        private bool RunCompc(string confpath)
        {
            try
            {
                // get the project root and compc.exe location from the command argument
                FileInfo compc = new FileInfo(FlexSdkBase + "\\bin\\compc.exe");
                if ((!ProjectPath.Exists) | (!compc.Exists))
                    throw new FileNotFoundException("Project or compc.exe not found", ProjectPath.FullName + "|" + compc.FullName);


                // generate arguments based on config, additional configs, and additional user arguments
                string cmdArgs = "-load-config+=\"" + confpath + "\"";
                if (Project.CompilerOptions.LoadConfig != string.Empty)
                    cmdArgs += " -load-config+=\"" + Project.CompilerOptions.LoadConfig + "\"";
                /* changed for new project manager core */
                //if (Project.CompilerOptions.Additional != string.Empty)
                //cmdArgs += " " + Project.CompilerOptions.Additional;
                if (Project.CompilerOptions.Additional.Length > 0)
                    foreach (string op in Project.CompilerOptions.Additional)
                        cmdArgs += " " + op;


                _anyErrors = false;

                // start the compc.exe process with arguments
                ProcessRunner process = new PluginCore.Utilities.ProcessRunner();
                process.Error += new LineOutputHandler(process_Error);
                process.Output += new LineOutputHandler(process_Output);
                process.WorkingDirectory = ProjectPath.FullName;
                process.RedirectInput = true;
                // process.ProcessEnded += new ProcessEndedHandler(process_ProcessEnded);
                process.Run(compc.FullName, cmdArgs);

                PluginBase.MainForm.StatusLabel.Text = (process.IsRunning ? "Build started..." : "Unable to start build. Check output.");
                TraceManager.Add((process.IsRunning ? "Running Process:" : "Unable to run Process:"));
                TraceManager.Add("\"" + compc.FullName + "\" " + cmdArgs);

                while (process.IsRunning)
                {
                    Thread.Sleep(5);
                    Application.DoEvents();
                }

                AS3Project project = Project;
                bool checkForIllegalCrossThreadCalls = Control.CheckForIllegalCrossThreadCalls;
                Control.CheckForIllegalCrossThreadCalls = false;
                if (!_anyErrors)
                {
                    PluginBase.MainForm.StatusLabel.Text = "Build Successful.";
                    TraceManager.AddAsync(string.Format("Build Successful ({0}).\n", process.HostedProcess.ExitCode), 2);
                }
                else
                {
                    PluginBase.MainForm.StatusLabel.Text = "Build failed.";
                    TraceManager.AddAsync(string.Format("Build failed ({0}).\n", process.HostedProcess.ExitCode), 2);
                }
                Control.CheckForIllegalCrossThreadCalls = checkForIllegalCrossThreadCalls;

                return (_anyErrors == false) & (process.HostedProcess.ExitCode == 0);
            }
            catch (Exception ex)
            {
                // somethings happened, report it
                TraceManager.Add("*** Unable to build SWC: " + ex.Message);
                TraceManager.Add(ex.StackTrace);
                
                return false;
            }
        }

        void PreBuild(object sender, EventArgs e)
        {
            _button.Enabled = false;
            //Clear Outputpanel
            NotifyEvent ne = new NotifyEvent(EventType.ProcessStart);
            EventManager.DispatchEvent(this, ne);

            CreateCompcConfig(CompcConfigPath_Flex, CompcBinPath_Flex, true, SwcProject.Flex_IgnoreClasses);
            if (SwcProject.FlexIncludeASI)
                PreBuild_Asi();
            if (SwcProject.MakeCS3)
            {
                CreateCompcConfig(CompcConfigPath_Flash, CompcBinPath_Flash, false, SwcProject.CS3_IgnoreClasses);
                if (SwcProject.MakeMXI)
                {
                    if (SwcProject.MXPIncludeASI && !SwcProject.FlexIncludeASI)
                        PreBuild_Asi();
                    PreBuild_Mxi();
                }
            }
            _button.Enabled = true;
        }

        private void PreBuild_Asi()
        {
            string outdir = ASIDir;
            if (!Directory.Exists(outdir))
                Directory.CreateDirectory(outdir);
            List<string> asfiles = new List<string>();

            foreach (string path in Project.Classpaths)
            {
                string rpath = path.Contains(":")
                    ? path.Trim('\\') + "\\"
                    : (Project.Directory + "\\" + path.Trim('\\') + "\\").Replace("\\\\", "\\");
                asfiles.AddRange(Directory.GetFiles(rpath, "*.as", SearchOption.AllDirectories));
            }

            foreach (string infile in asfiles)
                MakeIntrinsic(infile, outdir);
        }

        private void MakeIntrinsic(string infile, string outdir)
        {
            FileModel aFile = ASFileParser.ParseFile(infile, ASContext.Context);
            if (aFile.Version == 0) return;
            string code = aFile.GenerateIntrinsic(false);

            try
            {
                string dest = outdir.Trim('\\') + "\\";
                dest += aFile.Package.Length < 1
                    ? aFile.Classes[0].Name
                    : aFile.Package + "." + aFile.Classes[0].Name;
                dest += ".asi";


                File.WriteAllText(dest, code, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ErrorManager.ShowError(ex);
            }
        }

        private void PreBuild_Mxi()
        {
            string outfile = MXIPath;
            XmlDocument doc = new XmlDocument();
            doc.LoadXml("<?xml version=\"1.0\" encoding=\"UTF-8\" ?><macromedia-extension />");
            doc.DocumentElement.SetAttribute("name", SwcProject.CS3_ComponentName);
            SwcProject.IncrementVersion(0, 0, 1);
            SwcProject.Save(SWCProjectPath);
            doc.DocumentElement.SetAttribute("version", SwcProject.MXIVersion);
            doc.DocumentElement.SetAttribute("type", "flashcomponentswc");
            doc.DocumentElement.SetAttribute("requires-restart", "false");
            CreateElement("author", doc.DocumentElement).SetAttribute("name", SwcProject.MXIAuthor);
            CreateElement("description", doc.DocumentElement).InnerXml = "<![CDATA[ " + SwcProject.MXIDescription + " ]]>";
            CreateElement("ui-access", doc.DocumentElement).InnerXml = "<![CDATA[ " + SwcProject.MXIUIAccessText + " ]]>";
            XmlElement pro = CreateElement("product", CreateElement("products", doc.DocumentElement));
            pro.SetAttribute("name", "Flash");
            pro.SetAttribute("version", "9");
            pro.SetAttribute("primary", "true");
            XmlElement fil = CreateElement("file", CreateElement("files", doc.DocumentElement));
            fil.SetAttribute("name", Path.GetFileName(CompcBinPath_Flash));
            fil.SetAttribute("destination", "$flash/Components/" + SwcProject.CS3_ComponentGroup);

            doc.Save(outfile);
        }

        private void CreateCompcConfig(string confout, string binout, bool rsl, List<string> classExclusions)
        {
            TraceManager.Add("Prebuilding config " + confout + "...");
            // build the config file
            XmlDocument config = new XmlDocument();

            config.LoadXml("<?xml version=\"1.0\"?><flex-config/>");

            // general options...
            // output
            CreateElement("output", config.DocumentElement, binout);

            // use-network
            CreateElement("use-network", config.DocumentElement, Project.CompilerOptions.UseNetwork.ToString().ToLower());

            // warnings
            CreateElement("warnings", config.DocumentElement, Project.CompilerOptions.Warnings.ToString().ToLower());

            // locale
            if (Project.CompilerOptions.Locale != string.Empty)
                CreateElement("locale", config.DocumentElement, Project.CompilerOptions.Locale);

            // runtime-shared-libraries
            if (Project.CompilerOptions.RSLPaths.Length > 0)
            {
                XmlElement rslUrls = CreateElement("runtime-shared-libraries", config.DocumentElement, null);
                foreach (string rslUrl in Project.CompilerOptions.RSLPaths)
                    CreateElement("rsl-url", rslUrls, rslUrl);
            }

            // benchmark
            CreateElement("benchmark", config.DocumentElement, Project.CompilerOptions.Benchmark.ToString().ToLower());

            // compiler options...
            XmlElement compiler = CreateElement("compiler", config.DocumentElement, null);

            // compute-digest
            if (!rsl)
                CreateElement("compute-digest", config.DocumentElement, "false");

            // accessible
            CreateElement("accessible", compiler, Project.CompilerOptions.Accessible.ToString().ToLower());

            // allow-source-path-overlap
            CreateElement("allow-source-path-overlap", compiler, Project.CompilerOptions.AllowSourcePathOverlap.ToString().ToLower());

            // optimize
            CreateElement("optimize", compiler, Project.CompilerOptions.Optimize.ToString().ToLower());

            // strict
            CreateElement("strict", compiler, Project.CompilerOptions.Strict.ToString().ToLower());

            // es
            CreateElement("es", compiler, Project.CompilerOptions.ES.ToString().ToLower());

            // show-actionscript-warnings
            CreateElement("show-actionscript-warnings", compiler, Project.CompilerOptions.ShowActionScriptWarnings.ToString().ToLower());

            // show-binding-warnings
            CreateElement("show-binding-warnings", compiler, Project.CompilerOptions.ShowBindingWarnings.ToString().ToLower());

            // show-unused-type-selector- warnings
            CreateElement("show-unused-type-selector-warnings", compiler, Project.CompilerOptions.ShowUnusedTypeSelectorWarnings.ToString().ToLower());

            // use-resource-bundle-metadata
            CreateElement("use-resource-bundle-metadata", compiler, Project.CompilerOptions.UseResourceBundleMetadata.ToString().ToLower());

            // verbose-stacktraces
            CreateElement("verbose-stacktraces", compiler, Project.CompilerOptions.VerboseStackTraces.ToString().ToLower());

            // source-path & include-classes
            XmlElement sourcePath = CreateElement("source-path", compiler, null);
            foreach (string classPath in Project.Classpaths)
                CreateElement("path-element", sourcePath, ProjectPath.FullName + "\\" + classPath);

            // general options...
            // libarary-path
            if (Project.CompilerOptions.LibraryPaths.Length > 0)
            {
                XmlElement includeLibraries = CreateElement("library-path", compiler, null);
                foreach (string libPath in Project.CompilerOptions.LibraryPaths)
                    CreateElement("path-element", includeLibraries, ProjectPath.FullName + "\\" + libPath);
            }

            // include-libraries
            if (Project.CompilerOptions.IncludeLibraries.Length > 0)
            {
                XmlElement includeLibraries = CreateElement("include-libraries", compiler, null);
                foreach (string libPath in Project.CompilerOptions.IncludeLibraries)
                    CreateElement("library", includeLibraries, ProjectPath.FullName + "\\" + libPath);
            }

            // include-classes
            XmlElement includeClasses = CreateElement("include-classes", config.DocumentElement, null);
            foreach (string classPath in Project.Classpaths)
                IncludeClassesIn(includeClasses, ProjectPath + "\\" + classPath, string.Empty, classExclusions);

            // add namespace, save config to obj folder
            config.DocumentElement.SetAttribute("xmlns", "http://www.adobe.com/2006/flex-config");
            config.Save(confout);
            TraceManager.AddAsync("Configuration writen to: " + confout, 2);

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

        private string CompcConfigPath_Flex
        {
            get { return LibMakerDir + Project.Name + ".flex.compc.xml"; }            
        }

        private string CompcBinPath_Flex
        {
            //get { return LibMakerDir + Project.Name + ".flex.swc"; }
            get { return Path.Combine(ProjectPath.FullName,SwcProject.FlexBinPath); }
        }

        private string CompcConfigPath_Flash
        {
            get { return LibMakerDir + Project.Name + ".flash.compc.xml"; }            
        }

        private string CompcBinPath_Flash
        {            
            //get { return LibMakerDir + Project.Name + ".flash.swc"; }
            get { return Path.Combine(ProjectPath.FullName,SwcProject.FlashBinPath); }
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

        /// <summary>
        /// Main method for plugin - Export SWC using compc.exe
        /// </summary>
        /// <param name="sender">the sender</param>
        /// <param name="e">the event args</param>
        public void Build(object sender, System.EventArgs e)
        {            
            PreBuild(sender, e);
            Compile(sender, e);
        }

        private XmlElement CreateElement(string name, XmlElement parent)
        {
            return CreateElement(name, parent, string.Empty);
        }

        private XmlElement CreateElement(string name, XmlElement parent, string innerText)
        {
            XmlElement element = parent.OwnerDocument.CreateElement(name, parent.OwnerDocument.DocumentElement.NamespaceURI);
            if (innerText != null && innerText != string.Empty)
                element.InnerText = innerText;
            parent.AppendChild(element);

            return element;
        }

        private void IncludeClassesIn(XmlElement includeClasses, string sourcePath, string parentPath, List<string> classExclusions)
        {
            // take the current folder
            DirectoryInfo directory = new DirectoryInfo(sourcePath);
            string rspath = (sourcePath.TrimEnd('\\') + "\\").Replace(ProjectPath.FullName, "");
            // add every AS class to the manifest
            foreach (FileInfo file in directory.GetFiles())
            {
                if (file.Extension == ".as")
                {
                    string className = Path.GetFileNameWithoutExtension(file.FullName);
                    if (!classExclusions.Contains(rspath + className + ".as"))
                        CreateElement("class", includeClasses, parentPath + className);
                }
            }

            // process sub folders
            foreach (DirectoryInfo folder in directory.GetDirectories())
                IncludeClassesIn(includeClasses, folder.FullName, parentPath + folder.Name + ".", classExclusions);
        }

        void process_Output(object sender, string line)
        {
            //TraceManager.AddAsync(line);
        }

        void process_Error(object sender, string line)
        {
            _anyErrors = true;
            TraceManager.AddAsync(line, 3);
        }

        void process_ProcessEnded(object sender, int exitCode)
        {
            AS3Project project = PluginBase.CurrentProject as AS3Project;
            bool checkForIllegalCrossThreadCalls = Control.CheckForIllegalCrossThreadCalls;
            Control.CheckForIllegalCrossThreadCalls = false;
            if (!_anyErrors)
            {
                PluginBase.MainForm.StatusLabel.Text = "Build Successful.";
                TraceManager.AddAsync(string.Format("Build Successful ({0}).\n", exitCode), 2);
            }
            else
            {
                PluginBase.MainForm.StatusLabel.Text = "Build failed.";
                TraceManager.AddAsync(string.Format("Build failed ({0}).\n", exitCode), 2);
            }
            Control.CheckForIllegalCrossThreadCalls = checkForIllegalCrossThreadCalls;
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

        /// <summary>
        /// Opens the plugin panel if closed
        /// </summary>
        public void OpenPanel(Object sender, System.EventArgs e)
        {
            MessageBox.Show("asdf");
            //this.pluginPanel.Show();
        }

        #endregion

    }
}