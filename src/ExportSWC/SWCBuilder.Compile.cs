using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using ASCompletion.Context;
using ASCompletion.Model;
using ExportSWC.Resources;
using ExportSWC.Tracing.Interfaces;
using ExportSWC.Tracing;
using ICSharpCode.SharpZipLib.Zip;
using PluginCore.Helpers;
using PluginCore.Managers;
using PluginCore.Utilities;
using PluginCore;
using ProjectManager.Projects.AS3;
using System.Windows.Forms;
using System.Xml;
using System.Drawing;

namespace ExportSWC
{
    public partial class SWCBuilder
    {
        private string CompcConfigPath_Flex => LibMakerDir + _project.Name + ".flex.compc.xml";
        private string CompcConfigPath_Flash => LibMakerDir + _project.Name + ".flash.compc.xml";
        private string CompcBinPath_Flash => Path.Combine(ProjectPath.FullName, _swcProjectSettings.FlashBinPath);
        private string MXIPath => LibMakerDir + _project.Name + ".mxi";
        private string ASIDir => ProjectPath.FullName + "\\asi\\";
        private string SWCProjectSettingsPath => ProjectPath.FullName + "\\" + _project.Name + ".lxml";

        /// <summary>
        /// Main method for plugin - Export SWC using compc.exe
        /// </summary>
        /// <param name="sender">the sender</param>
        /// <param name="e">the event args</param>
        public void Build(AS3Project project, SWCProject swcProjectSettings)
        {
            Build(project, swcProjectSettings, null);
        }

        public void Build(AS3Project project, SWCProject swcProjectSettings, ITraceable tracer)
        {
            if (_running)
            {
                return;
            }

            _running = true;

            _project = project;
            _swcProjectSettings = swcProjectSettings;
            _tracer = tracer;

            try
            {
                PreBuild();
                Compile();
            }
            finally
            {
                _project = null;
                _swcProjectSettings = null;
                _tracer = null;

                _running = false;
            }
        }

        public void Compile(AS3Project project, SWCProject swcProjectSettings)
        {
            Compile(project, swcProjectSettings, null);
        }
        public void Compile(AS3Project project, SWCProject swcProjectSettings, ITraceable tracer)
        {
            if (_running)
            {
                return;
            }

            _running = true;

            _project = project;
            _swcProjectSettings = swcProjectSettings;
            _tracer = tracer;

            try
            {
                Compile();
            }
            finally
            {
                _project = null;
                _swcProjectSettings = null;
                _tracer = null;

                _running = false;
            }
        }

        public void PreBuild(AS3Project project, SWCProject swcProjectSettings)
        {
            PreBuild(project, swcProjectSettings, null);
        }

        public void PreBuild(AS3Project project, SWCProject swcProjectSettings, ITraceable tracer)
        {
            if (_running)
            {
                return;
            }

            _running = true;

            _project = project;
            _swcProjectSettings = swcProjectSettings;
            _tracer = tracer;

            try
            {
                PreBuild();
            }
            finally
            {
                _project = null;
                _swcProjectSettings = null;
                _tracer = null;

                _running = false;
            }
        }

        protected void Compile()
        {
            var buildSuccess = true;

            SaveModifiedDocuments();

            RunPreBuildEvent();

            buildSuccess &= RunCompc(CompcConfigPath_Flex);
            if (_swcProjectSettings.MakeCS3)
            {
                buildSuccess &= RunCompc(CompcConfigPath_Flash);
                PatchFlashSWC();
                if (_swcProjectSettings.LaunchAEM)
                {
                    buildSuccess &= BuildMXP();
                }
            }

            if (buildSuccess ||
                _project.AlwaysRunPostBuild)
            {
                RunPostBuildEvent();
            }
        }

        protected void RunPreBuildEvent()
        {
            if (_project.PreBuildEvent.Trim().Length == 0)
            {
                return;
            }

            var command = FlashDevelop.Utilities.ArgsProcessor.ProcessString(_project.PreBuildEvent, true);

            var process = new Process();
            var processStI = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C " + command,
                CreateNoWindow = true
            };
            process.StartInfo = processStI;
            process.Start();

            //TraceManager.AddAsync("Running Pre-Build Command:\ncmd: " + command);
            WriteLine("Running Pre-Build Command:\ncmd: " + command);

            process.WaitForExit(15000);
        }

        protected void RunPostBuildEvent()
        {
            var hasBuildEvent = _project.PostBuildEvent.Trim().Length >= 0;
            if (hasBuildEvent)
            {
                return;
            }

            var command = FlashDevelop.Utilities.ArgsProcessor.ProcessString(_project.PostBuildEvent, true);

            var process = new Process();
            var processStI = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C " + command,
                CreateNoWindow = true
            };
            process.StartInfo = processStI;
            process.Start();

            //TraceManager.AddAsync("Running Post-Build Command:\ncmd: " + command);
            WriteLine("Running Post-Build Command:\ncmd: " + command);
        }

        protected void SaveModifiedDocuments()
        {
            if (PluginBase.MainForm.HasModifiedDocuments == false)
            {
                return;
            }

            foreach (var document in PluginBase.MainForm.Documents)
            {
                if (document.IsModified)
                {
                    document.Save();
                }
            }
        }

        protected bool BuildMXP()
        {
            var pi = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = MXIPath
            };
            var process = Process.Start(pi);

            var success = process.WaitForExit(15000);

            return success && (process.ExitCode == 0);
        }

        protected void PatchFlashSWC()
        {
            var livePreviewFile = false;
            var file = CompcBinPath_Flash;

            if (!File.Exists(file))
            {
                return; // TODO: display error
            }

            var fze = new FastZipEvents();
            var fzip = new FastZip(fze);

            var tempDir = Path.GetTempPath().Trim('\\');
            tempDir += "\\flashdevelop_swc";

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }

            fzip.ExtractZip(file, tempDir, ".*");

            var catxml = new XmlDocument();

            var loaded = false;
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
            var fel = CreateElement("flash", catxml.DocumentElement["versions"]);
            fel.SetAttribute("version", "9.0");
            fel.SetAttribute("build", "r494");
            fel.SetAttribute("platform", "WIN");

            // <feature-components />
            CreateElement("feature-components", catxml.DocumentElement["features"]);

            // <file path="icon_0.png" mod="1061758088000" />
            if (catxml.DocumentElement["files"] == null)
            {
                CreateElement("files", catxml.DocumentElement, string.Empty);
            }

            if (!_swcProjectSettings.ValidImage())
            {
                LocaleHelper.GetImage("cs3_component_icon").Save(tempDir + "\\icon.png", ImageFormat.Png);
            }
            else
            {
                Image.FromFile(_swcProjectSettings.CS3ComponentIconFile).Save(tempDir + "\\icon.png", ImageFormat.Png);
            }

            var iel = CreateElement("file", catxml.DocumentElement["files"], string.Empty);
            iel.SetAttribute("path", "icon.png");
            iel.SetAttribute("mod", new FileInfo(tempDir + "\\icon.png").LastWriteTimeUtc.ToFileTimeUtc().ToString());

            // <component className="Symbol1" name="Symbol 1" icon="icon_0.png"  />
            if (catxml.DocumentElement["components"] == null)
            {
                CreateElement("components", catxml.DocumentElement);
            }

            var cel = CreateElement("component", catxml.DocumentElement["components"]);
            cel.SetAttribute("className", _swcProjectSettings.CS3ComponentClass);
            cel.SetAttribute("name", _swcProjectSettings.CS3ComponentName);
            cel.SetAttribute("icon", "icon.png");
            cel.SetAttribute("tooltip", _swcProjectSettings.CS3ComponentToolTip);

            // livePreview
            if (_swcProjectSettings.ValidLivePreview())
            {
                if (_swcProjectSettings.CS3PreviewType == CS3PreviewType.ExternalSWF)
                {
                    // livePreview exists
                    File.Copy(_swcProjectSettings.CS3PreviewResource, tempDir + "\\livePreview.swf");
                    livePreviewFile = true;
                }
                else
                {
                    // MAKE BUILDSCRIPT
                    var lpfile = BuildLivePreview();
                    if (lpfile == string.Empty)
                    {
                        //TraceManager.AddAsync("*** Error building live preview from class: " + _swcProjectSettings.CS3_PreviewResource);
                        WriteLine("*** Error building live preview from class: " + _swcProjectSettings.CS3PreviewResource, TraceMessageType.Error);
                        _anyErrors = true;
                    }
                    else
                    {
                        if (File.Exists(tempDir + "\\livePreview.swf"))
                        {
                            File.Delete(tempDir + "\\livePreview.swf");
                        }

                        File.Move(lpfile, tempDir + "\\livePreview.swf");
                        livePreviewFile = true;
                    }
                }

                if (livePreviewFile)
                {
                    cel.SetAttribute("preview", "livePreview.swf");
                    var lpf = CreateElement("file", catxml.DocumentElement["files"]);
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

                using (var zo = new ZipOutputStream(File.Create(file)))
                {
                    zo.UseZip64 = UseZip64.Off;
                    zo.SetLevel(9);
                    SwcAdd(zo, tempDir + "\\catalog.xml");
                    SwcAdd(zo, tempDir + "\\library.swf");
                    SwcAdd(zo, tempDir + "\\icon.png");
                    if (livePreviewFile)
                    {
                        SwcAdd(zo, tempDir + "\\livePreview.swf");
                    }

                    zo.Finish();
                    zo.Close();
                }

                Directory.Delete(tempDir, true);

                //TraceManager.AddAsync("Flash SWC ready: " + file);
                WriteLine("Flash SWC ready: " + file);
            }
        }

        protected string BuildLivePreview()
        {
            if (FindClassPath(_swcProjectSettings.CS3PreviewResource) == string.Empty)
            {
                return string.Empty;
            }

            var tproFile = TempFile(ProjectPath.FullName, ".as3proj");
            var tswfFile = TempFile(ProjectPath.FullName, ".swf");

            var lpc = new XmlDocument();
            lpc.Load(_project.ProjectPath);

            var opnl = lpc.DocumentElement["output"].ChildNodes;
            foreach (XmlNode node in opnl)
            {
                if (node.Attributes[0].Name == "path")
                {
                    node.Attributes[0].Value = Path.GetFileName(tswfFile);
                }
            }

            lpc.DocumentElement["compileTargets"].RemoveAll();
            var el = CreateElement("compile", lpc.DocumentElement["compileTargets"]);
            el.SetAttribute("path", FindClassPath(_swcProjectSettings.CS3PreviewResource));

            lpc.Save(tproFile);

            var fdBuildDir = Path.Combine(PathHelper.ToolDir, "fdbuild");
            var fdBuildPath = Path.Combine(fdBuildDir, "fdbuild.exe");

            var arguments = "\"" + tproFile + "\"";

            arguments += " -library \"" + PathHelper.LibraryDir + "\"";
            arguments += " -compiler \"" + FlexSdkBase + "\"";

            var fdp = new ProcessRunner();
            fdp.Run(fdBuildPath, arguments);

            while (fdp.IsRunning)
            {
                Thread.Sleep(20);
                Application.DoEvents();
            }

            File.Delete(tproFile);
            var tcon = _project.Directory.TrimEnd('\\') + "\\obj\\" + Path.GetFileNameWithoutExtension(tproFile) + "Config.xml";
            if (File.Exists(tcon))
            {
                File.Delete(tcon);
            }

            return tswfFile;
        }

        protected string FindClassPath(string className)
        {
            foreach (var cpath_ in _project.Classpaths)
            {

                var cpath = cpath_.Contains(":") ? cpath_ : Path.Combine(_project.Directory, cpath_);
                var files = Directory.GetFiles(cpath, "*.as", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (Path.GetFileNameWithoutExtension(file) == className)
                    {
                        return (Path.GetDirectoryName(file).TrimEnd('\\')
                            + "\\" + className + ".as")
                            .Replace(_project.Directory.TrimEnd('\\')
                            + "\\", "");
                    }
                }
            }

            return string.Empty;
        }

        private string TempFile(string path, string ext)
        {
            path = path.TrimEnd('\\') + "\\";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var output = "";
            var ten = new byte[10];

            output += new Random().Next(0, 10).ToString();

            while (File.Exists(path + output + ext))
            {
                output += new Random().Next(0, 10).ToString();
            }

            File.Create(path + output + ext).Close();
            return path + output + ext;
        }

        private void SwcAdd(ZipOutputStream str, string file)
        {
            var entry = new ZipEntry(file.Substring(file.LastIndexOf('\\') + 1))
            {
                CompressionMethod = CompressionMethod.Deflated
            };
            var buf = new byte[8192];
            str.PutNextEntry(entry);
            using (var fstr = File.OpenRead(file))
            {
                int c;
                do
                {
                    c = fstr.Read(buf, 0, buf.Length);
                    str.Write(buf, 0, c);
                } while (c > 0);
            }
        }

        protected bool IsAIR()
        {
            return ExtractPlatform() == "AIR";
        }
        protected virtual string ExtractPlatform()
        {
            // expected from project manager: "Flash Player;9.0;path;path..."
            var platform = _project.MovieOptions.Platform;
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

        protected bool RunCompc(string confpath)
        {
            // Apache Flex compat
            if (_project.Language == "as3")
            {
                var setPlayerglobalHomeEnv = false;
                var playerglobalHome = Environment.ExpandEnvironmentVariables("%PLAYERGLOBAL_HOME%");
                if (playerglobalHome.StartsWith('%'))
                {
                    setPlayerglobalHomeEnv = true;
                }

                if (setPlayerglobalHomeEnv)
                {
                    Environment.SetEnvironmentVariable("PLAYERGLOBAL_HOME", Path.Combine(_project.CurrentSDK, "frameworks/libs/player"));
                }
            }

            try
            {
                // get the project root and compc.exe location from the command argument
                var compc = GetExeOrBatPath(FlexSdkBase + "\\bin\\compc.exe");
                if ((!ProjectPath.Exists) | (!compc.Exists))
                {
                    throw new FileNotFoundException("Project or compc.exe not found", ProjectPath.FullName + "|" + compc.FullName);
                }
                // generate arguments based on config, additional configs, and additional user arguments
                var cmdArgs = "-load-config+=\"" + confpath + "\"";
                if (_project.CompilerOptions.LoadConfig != string.Empty)
                {
                    cmdArgs += " -load-config+=\"" + _project.CompilerOptions.LoadConfig + "\"";
                }

                /* changed for new project manager core */
                //if (Project.CompilerOptions.Additional != string.Empty)
                //cmdArgs += " " + Project.CompilerOptions.Additional;
                if (_project.CompilerOptions.Additional.Length > 0)
                {
                    foreach (var op in _project.CompilerOptions.Additional)
                    {
                        cmdArgs += " " + op;
                    }
                }

                _anyErrors = false;

                // start the compc.exe process with arguments
                var process = new ProcessRunner();
                process.Error += new LineOutputHandler(ProcessError);
                process.Output += new LineOutputHandler(ProcessOutput);
                //process.WorkingDirectory = ProjectPath.FullName; // commented out as supposed by i.o. (http://www.flashdevelop.org/community/viewtopic.php?p=36764#p36764)
                process.RedirectInput = true;
                process.Run(compc.FullName, cmdArgs);

                PluginBase.MainForm.StatusLabel.Text = process.IsRunning ? "Build started..." : "Unable to start build. Check output.";
                //TraceManager.Add((process.IsRunning ? "Running Process:" : "Unable to run Process:"));				
                //TraceManager.Add("\"" + compc.FullName + "\" " + cmdArgs);
                WriteLine(process.IsRunning ? "Running Process:" : "Unable to run Process:");
                WriteLine("\"" + compc.FullName + "\" " + cmdArgs);

                while (process.IsRunning)
                {
                    Thread.Sleep(5);
                    Application.DoEvents();
                }

                var project = _project;
                var checkForIllegalCrossThreadCalls = Control.CheckForIllegalCrossThreadCalls;
                Control.CheckForIllegalCrossThreadCalls = false;

                // Include AsDoc if FlexSdkVersion >= 4
                if (_swcProjectSettings.IntegrateAsDoc &&
                    FlexSdkVersion.Major >= 4)
                {
                    _anyErrors |= IncludeAsDoc() == false;
                }

                if (!_anyErrors)
                {
                    PluginBase.MainForm.StatusLabel.Text = "Build Successful.";
                    //TraceManager.AddAsync(string.Format("Build Successful ({0}).\n", process.HostedProcess.ExitCode), 2);
                    WriteLine(string.Format("Build Successful ({0}).\n", process.HostedProcess.ExitCode), TraceMessageType.Message);
                }
                else
                {
                    PluginBase.MainForm.StatusLabel.Text = "Build failed.";
                    //TraceManager.AddAsync(string.Format("Build failed ({0}).\n", process.HostedProcess.ExitCode), 2);
                    WriteLine(string.Format(string.Format("Build failed ({0}).\n", process.HostedProcess.ExitCode)), TraceMessageType.Error);
                }

                Control.CheckForIllegalCrossThreadCalls = checkForIllegalCrossThreadCalls;

                return (_anyErrors == false) & (process.HostedProcess.ExitCode == 0);
            }
            catch (Exception ex)
            {
                // somethings happened, report it
                //TraceManager.Add("*** Unable to build SWC: " + ex.Message);
                //TraceManager.Add(ex.StackTrace);
                WriteLine("*** Unable to build SWC: " + ex.Message, TraceMessageType.Error);
                WriteLine(ex.StackTrace, TraceMessageType.Message);

                return false;
            }
        }

        protected void PreBuild()
        {
            //Clear Outputpanel
            var ne = new NotifyEvent(EventType.ProcessStart);
            EventManager.DispatchEvent(this, ne);

            CreateCompcConfig(CompcConfigPath_Flex, CompcBinPath_Flex, true, _swcProjectSettings.FlexIgnoreClasses);
            if (_swcProjectSettings.FlexIncludeASI)
            {
                PreBuild_Asi();
            }

            if (_swcProjectSettings.MakeCS3)
            {
                CreateCompcConfig(CompcConfigPath_Flash, CompcBinPath_Flash, false, _swcProjectSettings.CS3IgnoreClasses);
                if (_swcProjectSettings.MakeMXI)
                {
                    if (_swcProjectSettings.MXPIncludeASI && !_swcProjectSettings.FlexIncludeASI)
                    {
                        PreBuild_Asi();
                    }

                    PreBuild_Mxi();
                }
            }
        }

        protected void PreBuild_Asi()
        {
            var outdir = ASIDir;
            if (!Directory.Exists(outdir))
            {
                Directory.CreateDirectory(outdir);
            }

            var asfiles = new List<string>();

            foreach (var path in _project.Classpaths)
            {
                var rpath = path.Contains(":")
                    ? path.Trim('\\') + "\\"
                    : (_project.Directory + "\\" + path.Trim('\\') + "\\").Replace("\\\\", "\\");
                asfiles.AddRange(Directory.GetFiles(rpath, "*.as", SearchOption.AllDirectories));
            }

            foreach (var infile in asfiles)
            {
                MakeIntrinsic(infile, outdir);
            }
        }

        protected void MakeIntrinsic(string infile, string outdir)
        {
            var aFile = ASFileParser.ParseFile(new FileModel(infile)
            {
                Context = ASContext.Context
            });
            if (aFile.Version == 0)
            {
                return;
            }

            var code = aFile.GenerateIntrinsic(false);

            try
            {
                var dest = outdir.Trim('\\') + "\\";
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

        protected void PreBuild_Mxi()
        {
            var outfile = MXIPath;
            var doc = new XmlDocument();
            doc.LoadXml("<?xml version=\"1.0\" encoding=\"UTF-8\" ?><macromedia-extension />");
            doc.DocumentElement.SetAttribute("name", _swcProjectSettings.CS3ComponentName);
            _swcProjectSettings.IncrementVersion(0, 0, 1);
            _swcProjectSettings.Save(SWCProjectSettingsPath);
            doc.DocumentElement.SetAttribute("version", _swcProjectSettings.MXIVersion);
            doc.DocumentElement.SetAttribute("type", "flashcomponentswc");
            doc.DocumentElement.SetAttribute("requires-restart", "false");
            CreateElement("author", doc.DocumentElement).SetAttribute("name", _swcProjectSettings.MXIAuthor);
            CreateElement("description", doc.DocumentElement).InnerXml = "<![CDATA[ " + _swcProjectSettings.MXIDescription + " ]]>";
            CreateElement("ui-access", doc.DocumentElement).InnerXml = "<![CDATA[ " + _swcProjectSettings.MXIUIAccessText + " ]]>";
            var pro = CreateElement("product", CreateElement("products", doc.DocumentElement));
            pro.SetAttribute("name", "Flash");
            pro.SetAttribute("version", "9");
            pro.SetAttribute("primary", "true");
            var fil = CreateElement("file", CreateElement("files", doc.DocumentElement));
            fil.SetAttribute("name", Path.GetFileName(CompcBinPath_Flash));
            fil.SetAttribute("destination", "$flash/Components/" + _swcProjectSettings.CS3ComponentGroup);

            doc.Save(outfile);
        }

        protected void CreateCompcConfig(string confout, string binout, bool rsl, List<string> classExclusions)
        {
            //TraceManager.Add("Prebuilding config " + confout + "...");
            WriteLine("Prebuilding config " + confout + "...");

            // build the config file
            var config = new XmlDocument();

            config.LoadXml("<?xml version=\"1.0\"?><flex-config/>");

            // general options...
            // output
            CreateElement("output", config.DocumentElement, binout);

            // use-network
            CreateElement("use-network", config.DocumentElement, _project.CompilerOptions.UseNetwork.ToString().ToLower());

            // If Air is used, target version is AIR version
            if (IsAIR() == false)
            {
                // target
                CreateElement("target-player", config.DocumentElement, GetTargetVersionString());
            }
            // warnings
            CreateElement("warnings", config.DocumentElement, _project.CompilerOptions.Warnings.ToString().ToLower());

            // locale
            if (_project.CompilerOptions.Locale != string.Empty)
            {
                CreateElement("locale", config.DocumentElement, _project.CompilerOptions.Locale);
            }

            // runtime-shared-libraries
            if (_project.CompilerOptions.RSLPaths.Length > 0)
            {
                var rslUrls = CreateElement("runtime-shared-libraries", config.DocumentElement, null);
                foreach (var rslUrl in _project.CompilerOptions.RSLPaths)
                {
                    CreateElement("rsl-url", rslUrls, rslUrl);
                }
            }

            // benchmark
            CreateElement("benchmark", config.DocumentElement, _project.CompilerOptions.Benchmark.ToString().ToLower());

            // compiler options...
            var compiler = CreateElement("compiler", config.DocumentElement, null);

            // compute-digest
            if (!rsl)
            {
                CreateElement("compute-digest", config.DocumentElement, "false");
            }

            // accessible
            CreateElement("accessible", compiler, _project.CompilerOptions.Accessible.ToString().ToLower());

            // allow-source-path-overlap
            CreateElement("allow-source-path-overlap", compiler, _project.CompilerOptions.AllowSourcePathOverlap.ToString().ToLower());

            // optimize
            CreateElement("optimize", compiler, _project.CompilerOptions.Optimize.ToString().ToLower());

            // strict
            CreateElement("strict", compiler, _project.CompilerOptions.Strict.ToString().ToLower());

            // es
            CreateElement("es", compiler, _project.CompilerOptions.ES.ToString().ToLower());

            // show-actionscript-warnings
            CreateElement("show-actionscript-warnings", compiler, _project.CompilerOptions.ShowActionScriptWarnings.ToString().ToLower());

            // show-binding-warnings
            CreateElement("show-binding-warnings", compiler, _project.CompilerOptions.ShowBindingWarnings.ToString().ToLower());

            // show-unused-type-selector- warnings
            CreateElement("show-unused-type-selector-warnings", compiler, _project.CompilerOptions.ShowUnusedTypeSelectorWarnings.ToString().ToLower());

            // use-resource-bundle-metadata
            CreateElement("use-resource-bundle-metadata", compiler, _project.CompilerOptions.UseResourceBundleMetadata.ToString().ToLower());

            // verbose-stacktraces
            CreateElement("verbose-stacktraces", compiler, _project.CompilerOptions.VerboseStackTraces.ToString().ToLower());

            // source-path & include-classes
            var sourcePath = CreateElement("source-path", compiler, null);
            foreach (var classPath in _project.Classpaths)
            {
                var absClassPath = GetProjectItemFullPath(classPath).ToLower();
                CreateElement("path-element", sourcePath, absClassPath);
            }

            // general options...
            // libarary-path			
            if (_project.CompilerOptions.LibraryPaths.Length > 0)
            {
                var includeLibraries = CreateElement("library-path", compiler, null);
                foreach (var libPath in _project.CompilerOptions.LibraryPaths)
                {
                    var absLibPath = GetProjectItemFullPath(libPath).ToLower();
                    CreateElement("path-element", includeLibraries, absLibPath);
                }
            }

            // include-libraries
            if (_project.CompilerOptions.IncludeLibraries.Length > 0)
            {
                var includeLibraries = CreateElement("include-libraries", compiler, null);
                foreach (var libPath in _project.CompilerOptions.IncludeLibraries)
                {
                    var absLibPath = GetProjectItemFullPath(libPath).ToLower();
                    CreateElement("library", includeLibraries, absLibPath);
                }
            }

            // include-classes
            var origClassExclusions = classExclusions;
            classExclusions = new List<string>();
            for (var i = 0; i < origClassExclusions.Count; i++)
            {
                classExclusions.Add(GetProjectItemFullPath(origClassExclusions[i]).ToLower());
            }

            var includeClasses = CreateElement("include-classes", config.DocumentElement, null);
            foreach (var classPath in _project.Classpaths)
            {
                var absClassPath = GetProjectItemFullPath(classPath).ToLower();
                IncludeClassesIn(includeClasses, absClassPath, string.Empty, classExclusions);
            }

            // external-library-path 
            if (_project.CompilerOptions.ExternalLibraryPaths != null && _project.CompilerOptions.ExternalLibraryPaths.Length > 0)
            {
                var externalLibs = CreateElement("external-library-path", compiler, null);
                var attr = externalLibs.OwnerDocument.CreateAttribute("append");
                attr.InnerXml = "true";
                externalLibs.Attributes.Append(attr);

                foreach (var libPath in _project.CompilerOptions.ExternalLibraryPaths)
                {
                    var absLibPath = GetProjectItemFullPath(libPath).ToLower();
                    CreateElement("path-element", externalLibs, absLibPath);
                }
            }

            // add namespace, save config to obj folder
            config.DocumentElement.SetAttribute("xmlns", "http://www.adobe.com/2006/flex-config");
            config.Save(confout);
            //TraceManager.AddAsync("Configuration writen to: " + confout, 2);
            WriteLine("Configuration writen to: " + confout);

        }

        protected void IncludeClassesIn(XmlElement includeClasses, string sourcePath, string parentPath, List<string> classExclusions)
        {
            // take the current folder
            var directory = new DirectoryInfo(sourcePath);
            // add every AS class to the manifest
            foreach (var file in directory.GetFiles())
            {
                if (file.Extension == ".as" ||
                    file.Extension == ".mxml")
                {
                    if (!IsFileIgnored(file.FullName, classExclusions))
                    {
                        CreateElement("class", includeClasses, parentPath + Path.GetFileNameWithoutExtension(file.FullName));
                    }
                }
            }

            // process sub folders
            foreach (var folder in directory.GetDirectories())
            {
                IncludeClassesIn(includeClasses, folder.FullName, parentPath + folder.Name + ".", classExclusions);
            }
        }
    }
}
