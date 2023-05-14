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
using ExportSWC.Utils;
using ExportSWC.Options;
using ExportSWC.AsDoc;
using ProjectManager.Actions;

namespace ExportSWC.Compiling
{
    internal class SWCBuilder
    {
        private bool _anyErrors;
        private bool _running;
        private readonly ITraceable _tracer;

        public SWCBuilder(ITraceable tracer)
        {
            _tracer = tracer;
        }

        private void ProcessOutput(object sender, string line)
        {
            //TraceManager.AddAsync(line);
        }

        private void ProcessError(object sender, string line)
        {
            var isError = line.StartsWithOrdinal("Error:");
            _anyErrors |= isError;
            var level = TraceMessageType.Warning;
            if (isError)
            {
                level = TraceMessageType.Error;
            }
            //TraceManager.AddAsync(line, 3);
            WriteLine(line, level);
        }

        /// <summary>
        /// Main method for plugin - Export SWC using compc.exe
        /// </summary>
        /// <param name="sender">the sender</param>
        /// <param name="e">the event args</param>
        public void Build(AS3Project project, SWCProject swcProjectSettings)
        {
            if (_running)
            {
                return;
            }

            _running = true;

            var context = new CompileContext
            {
                Project = project,
                SwcProjectSettings = swcProjectSettings,
            };


            BuildActions.GetCompilerPath(project); // use correct SDK

            try
            {
                PreBuild(context);
                Compile(context);
            }
            finally
            {
                _running = false;
            }
        }

        protected void Compile(CompileContext context)
        {
            var buildSuccess = true;

            SaveModifiedDocuments();

            RunPreBuildEvent(context);

            buildSuccess &= RunCompc(context, context.CompcConfigPathFlex);
            if (context.SwcProjectSettings.MakeCS3)
            {
                buildSuccess &= RunCompc(context, context.CompcConfigPathFlash);
                PatchFlashSWC(context);
                if (context.SwcProjectSettings.LaunchAEM)
                {
                    buildSuccess &= BuildMXP(context);
                }
            }

            if (buildSuccess ||
                context.Project.AlwaysRunPostBuild)
            {
                RunPostBuildEvent(context);
            }
        }

        public void Compile(AS3Project project, SWCProject swcProjectSettings)
        {
            if (_running)
            {
                return;
            }

            _running = true;

            var context = new CompileContext
            {
                Project = project,
                SwcProjectSettings = swcProjectSettings
            };

            try
            {
                Compile(context);
            }
            finally
            {
                _running = false;
            }
        }

        public void PreBuild(AS3Project project, SWCProject swcProjectSettings)
        {
            if (_running)
            {
                return;
            }

            _running = true;

            var context = new CompileContext
            {
                Project = project,
                SwcProjectSettings = swcProjectSettings
            };


            try
            {
                PreBuild(context);
            }
            finally
            {
                _running = false;
            }
        }

        protected void RunPreBuildEvent(CompileContext context)
        {
            if (context.Project.PreBuildEvent.Trim().Length == 0)
            {
                return;
            }

            var command = FlashDevelop.Utilities.ArgsProcessor.ProcessString(context.Project.PreBuildEvent, true);

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

        protected void RunPostBuildEvent(CompileContext context)
        {
            var hasBuildEvent = context.Project.PostBuildEvent.Trim().Length >= 0;
            if (hasBuildEvent)
            {
                return;
            }

            var command = FlashDevelop.Utilities.ArgsProcessor.ProcessString(context.Project.PostBuildEvent, true);

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

        protected bool BuildMXP(CompileContext context)
        {
            var pi = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = context.MXIPath
            };
            var process = Process.Start(pi);

            var success = process.WaitForExit(15000);

            return success && process.ExitCode == 0;
        }

        protected void PatchFlashSWC(CompileContext context)
        {
            var livePreviewFile = false;
            var file = context.CompcOutputPathFlash;
            var swcProjectSettings = context.SwcProjectSettings;

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
            var fel = catxml.DocumentElement["versions"].CreateElement("flash");
            fel.SetAttribute("version", "9.0");
            fel.SetAttribute("build", "r494");
            fel.SetAttribute("platform", "WIN");

            // <feature-components />
            catxml.DocumentElement["features"].CreateElement("feature-components");

            // <file path="icon_0.png" mod="1061758088000" />
            if (catxml.DocumentElement["files"] == null)
            {
                catxml.DocumentElement.CreateElement("files", string.Empty);
            }

            if (!swcProjectSettings.ValidImage())
            {
                LocaleHelper.GetImage("cs3_component_icon").Save(tempDir + "\\icon.png", ImageFormat.Png);
            }
            else
            {
                Image.FromFile(swcProjectSettings.CS3ComponentIconFile).Save(tempDir + "\\icon.png", ImageFormat.Png);
            }

            var iel = catxml.DocumentElement["files"].CreateElement("file", string.Empty);
            iel.SetAttribute("path", "icon.png");
            iel.SetAttribute("mod", new FileInfo(tempDir + "\\icon.png").LastWriteTimeUtc.ToFileTimeUtc().ToString());

            // <component className="Symbol1" name="Symbol 1" icon="icon_0.png"  />
            if (catxml.DocumentElement["components"] == null)
            {
                catxml.DocumentElement.CreateElement("components");
            }

            var cel = catxml.DocumentElement["components"].CreateElement("component");
            cel.SetAttribute("className", swcProjectSettings.CS3ComponentClass);
            cel.SetAttribute("name", swcProjectSettings.CS3ComponentName);
            cel.SetAttribute("icon", "icon.png");
            cel.SetAttribute("tooltip", swcProjectSettings.CS3ComponentToolTip);

            // livePreview
            if (swcProjectSettings.ValidLivePreview())
            {
                if (swcProjectSettings.CS3PreviewType == CS3PreviewType.ExternalSWF)
                {
                    // livePreview exists
                    File.Copy(swcProjectSettings.CS3PreviewResource, tempDir + "\\livePreview.swf");
                    livePreviewFile = true;
                }
                else
                {
                    // MAKE BUILDSCRIPT
                    var lpfile = BuildLivePreview(context);
                    if (lpfile == string.Empty)
                    {
                        //TraceManager.AddAsync("*** Error building live preview from class: " + _swcProjectSettings.CS3_PreviewResource);
                        WriteLine("*** Error building live preview from class: " + swcProjectSettings.CS3PreviewResource, TraceMessageType.Error);
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
                    var lpf = catxml.DocumentElement["files"].CreateElement("file");
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

        protected string BuildLivePreview(CompileContext context)
        {
            var swcProjectSettings = context.SwcProjectSettings;
            var project = context.Project;

            var env = new Dictionary<string, string>();

            env.SetApacheFlexCompatibilityEnvironment(context.Project);

            if (FindClassPath(context, swcProjectSettings.CS3PreviewResource) == string.Empty)
            {
                return string.Empty;
            }

            var tempProjectFile = TempFile(context.ProjectFullPath, ".as3proj");
            var tempSwfFile = TempFile(context.ProjectFullPath, ".swf");

            var projectXml = new XmlDocument();
            projectXml.Load(project.ProjectPath);

            var opnl = projectXml.DocumentElement["output"].ChildNodes;
            foreach (XmlNode node in opnl)
            {
                if (node.Attributes[0].Name == "path")
                {
                    node.Attributes[0].Value = Path.GetFileName(tempSwfFile);
                }
            }

            projectXml.DocumentElement["compileTargets"].RemoveAll();
            var el = projectXml.DocumentElement["compileTargets"].CreateElement("compile");
            el.SetAttribute("path", FindClassPath(context, swcProjectSettings.CS3PreviewResource));

            projectXml.Save(tempProjectFile);

            var fdBuildDirectory = Path.Combine(PathHelper.ToolDir, "fdbuild");
            var fdBuildFilepath = Path.Combine(fdBuildDirectory, "fdbuild.exe");

            var arguments = $@"""{tempProjectFile}""";

            arguments += $@" -library ""{PathHelper.LibraryDir}""";
            arguments += $@" -compiler ""{context.SdkBase}""";

            var fdp = new ProcessRunnerExtended();
            fdp.Run(fdBuildFilepath, arguments, env);

            while (fdp.IsRunning)
            {
                Thread.Sleep(20);
                Application.DoEvents();
            }

            File.Delete(tempProjectFile);
            var tcon = $@"{project.Directory.TrimEnd('\\')}\obj\{Path.GetFileNameWithoutExtension(tempProjectFile)}Config.xml";
            if (File.Exists(tcon))
            {
                File.Delete(tcon);
            }

            return tempSwfFile;
        }

        protected string FindClassPath(CompileContext context, string className)
        {
            var _project = context.Project;
            var projectFullPath = context.ProjectFullPath;

            foreach (var cpath in _project.Classpaths)
            {
                // TODO: is this trying to make it an absolute path?
                var fullClassPath = cpath.Contains(":") ? cpath : Path.Combine(projectFullPath, cpath);
                var files = Directory.GetFiles(fullClassPath, "*.as", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (Path.GetFileNameWithoutExtension(file) == className)
                    {
                        var fullFilepath = $"{Path.GetDirectoryName(file).TrimEnd('\\')}\\{className}.as";
                        return fullFilepath.Replace($"{_project.Directory.TrimEnd('\\')}\\", "");
                    }
                }
            }

            return string.Empty;
        }

        private string TempFile(string path, string ext)
        {
            path = $"{path.TrimEnd('\\')}\\";
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

        protected bool RunCompc(CompileContext context, string confpath)
        {
            var project = context.Project;
            var projectFullPath = context.ProjectFullPath;
            var sdkBase = context.SdkBase;
            var swcProjectSettings = context.SwcProjectSettings;

            var env = new Dictionary<string, string>();

            // Apache Flex compat
            env.SetApacheFlexCompatibilityEnvironment(context.Project);

            var checkForIllegalCrossThreadCalls = Control.CheckForIllegalCrossThreadCalls;

            try
            {
                // get the project root and compc.exe location from the command argument
                var compc = PathUtils.GetExeOrBatPath(Path.Combine(sdkBase, "bin", "compc.exe"));
                if (!Directory.Exists(projectFullPath) | !compc.Exists)
                {
                    throw new FileNotFoundException("Project or compc.exe not found", projectFullPath + "|" + compc.FullName);
                }

                var cmdArgs = "";
                // prevent flaky builds by specifying configname explicitly
                cmdArgs += $"+configname={(context.IsAir ? "air" : "flex")}";

                // generate arguments based on config, additional configs, and additional user arguments
                cmdArgs += $@" -load-config+=""{confpath}""";
                if (project.CompilerOptions.LoadConfig != string.Empty)
                {
                    cmdArgs += $@" -load-config+=""{project.CompilerOptions.LoadConfig}""";
                }

                if (project.CompilerOptions.Additional.Length > 0)
                {
                    foreach (var additionalOption in project.CompilerOptions.Additional)
                    {
                        cmdArgs += $" {additionalOption}";
                    }
                }

                _anyErrors = false;

                // start the compc.exe process with arguments
                var process = new ProcessRunnerExtended();
                process.Error += new LineOutputHandler(ProcessError);
                process.Output += new LineOutputHandler(ProcessOutput);
                //process.WorkingDirectory = ProjectPath.FullName; // commented out as supposed by i.o. (http://www.flashdevelop.org/community/viewtopic.php?p=36764#p36764)
                process.RedirectInput = true;

                process.Run(compc.FullName, cmdArgs, env);

                PluginBase.MainForm.StatusLabel.Text = process.IsRunning ? "Build started..." : "Unable to start build. Check output.";

                WriteLine(process.IsRunning ? "Running Process:" : "Unable to run Process:");
                WriteLine($@"""{compc.FullName}"" {cmdArgs}");

                while (process.IsRunning)
                {
                    Thread.Sleep(5);
                    Application.DoEvents();
                }

                var success = process.HostedProcess.ExitCode == 0;

                Control.CheckForIllegalCrossThreadCalls = false;

                // Include AsDoc if FlexSdkVersion >= 4
                if (swcProjectSettings.IntegrateAsDoc &&
                    context.IsAsDocIntegrationAvailable)
                {
                    var generator = new AsDocGenerator(_tracer);
                    var asDocContext = new AsDocContext
                    {
                        TargetVersion = context.TargetVersion,
                        FlexIgnoreClasses = swcProjectSettings.FlexIgnoreClasses,
                        FlexOutputPath = context.CompcOutputPathFlex,
                        SdkBase = sdkBase,
                        IsAir = context.IsAir,
                        Project = project
                    };
                    _anyErrors |= generator.IncludeAsDoc(asDocContext) == false;
                }

                success = success && !_anyErrors;

                if (success)
                {
                    PluginBase.MainForm.StatusLabel.Text = "Build Successful.";
                    WriteLine($"Build Successful ({process.HostedProcess.ExitCode}).\n", TraceMessageType.Message);
                }
                else
                {
                    PluginBase.MainForm.StatusLabel.Text = "Build failed.";
                    WriteLine($"Build failed ({process.HostedProcess.ExitCode}).\n", TraceMessageType.Error);
                }

                return success;
            }
            catch (Exception ex)
            {
                // somethings happened, report it
                WriteLine($"*** Unable to build SWC: {ex.Message}", TraceMessageType.Error);
                WriteLine(ex.StackTrace, TraceMessageType.Message);

                return false;
            }
            finally
            {
                Control.CheckForIllegalCrossThreadCalls = checkForIllegalCrossThreadCalls;
            }
        }

        protected void PreBuild(CompileContext context)
        {
            WriteLine("");
            WriteLine("PreBuild", TraceMessageType.Message);

            var swcProjectSettings = context.SwcProjectSettings;

            //Clear Outputpanel
            var ne = new NotifyEvent(EventType.ProcessStart);
            EventManager.DispatchEvent(this, ne);

            CreateCompcConfig(context, context.CompcConfigPathFlex, context.CompcOutputPathFlex, true, context.SwcProjectSettings.FlexIgnoreClasses);
            if (swcProjectSettings.FlexIncludeASI)
            {
                PreBuild_Asi(context);
            }

            if (swcProjectSettings.MakeCS3)
            {
                CreateCompcConfig(context, context.CompcConfigPathFlash, context.CompcOutputPathFlash, false, swcProjectSettings.CS3IgnoreClasses);
                if (swcProjectSettings.MakeMXI)
                {
                    if (swcProjectSettings.MXPIncludeASI &&
                        !swcProjectSettings.FlexIncludeASI)
                    {
                        PreBuild_Asi(context);
                    }

                    PreBuild_Mxi(context);
                }
            }
        }

        protected void PreBuild_Asi(CompileContext context)
        {
            var outdir = context.ASIDir;
            var project = context.Project;

            if (!Directory.Exists(outdir))
            {
                Directory.CreateDirectory(outdir);
            }

            var asfiles = new List<string>();

            foreach (var path in project.Classpaths)
            {
                // TODO: Is this trying to get absolute path?
                var rpath = path.Contains(":")
                    ? $@"{path.Trim('\\')}\"
                    : $@"{project.Directory}\{path.Trim('\\')}\".Replace(@"\\", @"\");
                asfiles.AddRange(Directory.GetFiles(rpath, "*.as", SearchOption.AllDirectories));
            }

            foreach (var infile in asfiles)
            {
                MakeIntrinsic(infile, outdir);
            }
        }

        protected void MakeIntrinsic(string infile, string outdir)
        {
            var asFile = ASFileParser.ParseFile(new FileModel(infile)
            {
                Context = ASContext.Context
            });
            if (asFile.Version == 0)
            {
                return;
            }

            var code = asFile.GenerateIntrinsic(false);

            try
            {
                var dest = $@"{outdir.Trim('\\')}\";
                dest += asFile.Package.Length < 1
                    ? asFile.Classes[0].Name
                    : asFile.Package + "." + asFile.Classes[0].Name;
                dest += ".asi";

                File.WriteAllText(dest, code, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ErrorManager.ShowError(ex);
            }
        }

        protected void PreBuild_Mxi(CompileContext context)
        {
            var swcProjectSettings = context.SwcProjectSettings;

            var outfile = context.MXIPath;

            var doc = new XmlDocument();
            doc.LoadXml("""<?xml version="1.0" encoding="UTF-8" ?><macromedia-extension />""");
            doc.DocumentElement.SetAttribute("name", swcProjectSettings.CS3ComponentName);

            swcProjectSettings.IncrementVersion(0, 0, 1);
            swcProjectSettings.Save(context.SWCProjectSettingsPath);

            doc.DocumentElement.SetAttribute("version", swcProjectSettings.MXIVersion);
            doc.DocumentElement.SetAttribute("type", "flashcomponentswc");
            doc.DocumentElement.SetAttribute("requires-restart", "false");

            doc.DocumentElement.CreateElement("author").SetAttribute("name", swcProjectSettings.MXIAuthor);
            doc.DocumentElement.CreateElement("description").InnerXml = "<![CDATA[ " + swcProjectSettings.MXIDescription + " ]]>";
            doc.DocumentElement.CreateElement("ui-access").InnerXml = "<![CDATA[ " + swcProjectSettings.MXIUIAccessText + " ]]>";

            var pro = doc.DocumentElement.CreateElement("products").CreateElement("product");
            pro.SetAttribute("name", "Flash");
            pro.SetAttribute("version", "9");
            pro.SetAttribute("primary", "true");

            var fil = doc.DocumentElement.CreateElement("files").CreateElement("file");
            fil.SetAttribute("name", Path.GetFileName(context.CompcOutputPathFlash));
            fil.SetAttribute("destination", "$flash/Components/" + swcProjectSettings.CS3ComponentGroup);

            doc.Save(outfile);
        }

        protected void CreateCompcConfig(CompileContext context, string configFilepath, string outputFilepath, bool isRuntimeSharedLibrary, List<string> classExclusions)
        {
            var project = context.Project;

            //TraceManager.Add("Prebuilding config " + confout + "...");
            WriteLine("Prebuilding config " + configFilepath + "...");

            // build the config file
            var config = new XmlDocument();

            config.LoadXml("<?xml version=\"1.0\"?><flex-config/>");

            // general options...
            // output
            config.DocumentElement.CreateElement("output", outputFilepath);

            // use-network
            config.DocumentElement.CreateElement("use-network", project.CompilerOptions.UseNetwork.ToString().ToLower());

            // If Air is used, target version is AIR version
            // target
            config.DocumentElement.CreateElement("target-player", context.TargetVersion);

            // warnings
            config.DocumentElement.CreateElement("warnings", project.CompilerOptions.Warnings.ToString().ToLower());

            //// locale
            //if (!context.IsAir &&
            //    project.CompilerOptions.Locale != string.Empty)
            //{
            //    config.DocumentElement.CreateElement("locale", project.CompilerOptions.Locale);
            //}

            // runtime-shared-libraries
            if (project.CompilerOptions.RSLPaths.Length > 0)
            {
                var rslUrls = config.DocumentElement.CreateElement("runtime-shared-libraries", null);
                foreach (var rslUrl in project.CompilerOptions.RSLPaths)
                {
                    rslUrls.CreateElement("rsl-url", rslUrl);
                }
            }

            // benchmark
            config.DocumentElement.CreateElement("benchmark", project.CompilerOptions.Benchmark.ToString().ToLower());

            // compiler options...
            var compiler = config.DocumentElement.CreateElement("compiler", null);

            // compute-digest
            if (!isRuntimeSharedLibrary)
            {
                config.DocumentElement.CreateElement("compute-digest", "false");
            }

            // accessible
            compiler.CreateElement("accessible", project.CompilerOptions.Accessible.ToString().ToLower());

            // allow-source-path-overlap
            compiler.CreateElement("allow-source-path-overlap", project.CompilerOptions.AllowSourcePathOverlap.ToString().ToLower());

            // optimize
            compiler.CreateElement("optimize", project.CompilerOptions.Optimize.ToString().ToLower());

            // strict
            compiler.CreateElement("strict", project.CompilerOptions.Strict.ToString().ToLower());

            // es
            compiler.CreateElement("es", project.CompilerOptions.ES.ToString().ToLower());

            // show-actionscript-warnings
            compiler.CreateElement("show-actionscript-warnings", project.CompilerOptions.ShowActionScriptWarnings.ToString().ToLower());

            // show-binding-warnings
            compiler.CreateElement("show-binding-warnings", project.CompilerOptions.ShowBindingWarnings.ToString().ToLower());

            // show-unused-type-selector- warnings
            compiler.CreateElement("show-unused-type-selector-warnings", project.CompilerOptions.ShowUnusedTypeSelectorWarnings.ToString().ToLower());

            // use-resource-bundle-metadata
            compiler.CreateElement("use-resource-bundle-metadata", project.CompilerOptions.UseResourceBundleMetadata.ToString().ToLower());

            // verbose-stacktraces
            compiler.CreateElement("verbose-stacktraces", project.CompilerOptions.VerboseStackTraces.ToString().ToLower());

            // source-path & include-classes
            var sourcePath = compiler.CreateElement("source-path", null);
            foreach (var classPath in project.Classpaths)
            {
                var absClassPath = PathUtils.GetProjectItemFullPath(context.ProjectFullPath, classPath).ToLower();
                sourcePath.CreateElement("path-element", absClassPath);
            }

            // general options...
            // libarary-path			
            if (project.CompilerOptions.LibraryPaths.Length > 0)
            {
                var includeLibraries = compiler.CreateElement("library-path", null);
                foreach (var libPath in project.CompilerOptions.LibraryPaths)
                {
                    var absLibPath = PathUtils.GetProjectItemFullPath(context.ProjectFullPath, libPath).ToLower();
                    includeLibraries.CreateElement("path-element", absLibPath);
                }
            }

            // include-libraries
            if (project.CompilerOptions.IncludeLibraries.Length > 0)
            {
                var includeLibraries = compiler.CreateElement("include-libraries", null);
                foreach (var libPath in project.CompilerOptions.IncludeLibraries)
                {
                    var absLibPath = PathUtils.GetProjectItemFullPath(context.ProjectFullPath, libPath).ToLower();
                    includeLibraries.CreateElement("library", absLibPath);
                }
            }

            // include-classes
            var origClassExclusions = classExclusions;
            classExclusions = new List<string>();
            for (var i = 0; i < origClassExclusions.Count; i++)
            {
                classExclusions.Add(PathUtils.GetProjectItemFullPath(context.ProjectFullPath, origClassExclusions[i]).ToLower());
            }

            var includeClasses = config.DocumentElement.CreateElement("include-classes", null);
            foreach (var classPath in project.Classpaths)
            {
                var absClassPath = PathUtils.GetProjectItemFullPath(context.ProjectFullPath, classPath).ToLower();
                IncludeClassesIn(includeClasses, context.ProjectFullPath, absClassPath, string.Empty, classExclusions);
            }

            // external-library-path 
            if (project.CompilerOptions.ExternalLibraryPaths != null && project.CompilerOptions.ExternalLibraryPaths.Length > 0)
            {
                var externalLibs = compiler.CreateElement("external-library-path", null);
                var attr = externalLibs.OwnerDocument.CreateAttribute("append");
                attr.InnerXml = "true";
                externalLibs.Attributes.Append(attr);

                foreach (var libPath in project.CompilerOptions.ExternalLibraryPaths)
                {
                    var absLibPath = PathUtils.GetProjectItemFullPath(context.ProjectFullPath, libPath).ToLower();
                    externalLibs.CreateElement("path-element", absLibPath);
                }
            }

            // add namespace, save config to obj folder
            config.DocumentElement.SetAttribute("xmlns", "http://www.adobe.com/2006/flex-config");
            config.Save(configFilepath);
            //TraceManager.AddAsync("Configuration writen to: " + confout, 2);
            WriteLine("Configuration writen to: " + configFilepath);

        }

        protected void IncludeClassesIn(XmlElement includeClasses, string projectPath, string sourcePath, string parentPath, List<string> classExclusions)
        {
            // take the current folder
            var directory = new DirectoryInfo(sourcePath);

            if (!directory.Exists)
            {
                WriteLine($"Path '{sourcePath}' does not exist - skipped", TraceMessageType.Warning);
                return;
            }

            // add every AS class to the manifest
            foreach (var file in directory.GetFiles())
            {
                if (file.Extension == ".as" ||
                    file.Extension == ".mxml")
                {
                    if (!PathUtils.IsFileIgnored(projectPath, file.FullName, classExclusions))
                    {
                        includeClasses.CreateElement("class", parentPath + Path.GetFileNameWithoutExtension(file.FullName));
                    }
                }
            }

            // process sub folders
            foreach (var folder in directory.GetDirectories())
            {
                IncludeClassesIn(includeClasses, projectPath, folder.FullName, parentPath + folder.Name + ".", classExclusions);
            }
        }

        private void WriteLine(string msg)
        {
            WriteLine(msg, TraceMessageType.Verbose);
        }

        private void WriteLine(string msg, TraceMessageType messageType)
        {
            if (_tracer == null)
            {
                return;
            }

            _tracer.WriteLine(msg, messageType);
        }
    }

}
