using System;
using System.Collections.Generic;
using System.Text;
using ProjectManager.Projects.AS3;
using ICSharpCode.SharpZipLib.Zip;
using System.Xml;
using System.IO;
using System.Diagnostics;
using PluginCore.Managers;
using PluginCore;
using System.Threading;
using PluginCore.Utilities;
using PluginCore.Helpers;
using ExportSWC.Resources;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ASCompletion.Model;
using ASCompletion.Context;
using ExportSWC.Tracing.Interfaces;
using ExportSWC.Tracing;

namespace ExportSWC
{
	public class SWCBuilder
	{
		private bool _anyErrors;
		protected bool _running;

		protected SWCProject _swcProjectSettings = null;
		protected AS3Project _project = null;
		protected ITraceable _tracer = null;

		protected void WriteLine(string msg)
		{
			WriteLine(msg, TraceMessageType.Verbose);
		}
		protected void WriteLine(string msg, TraceMessageType messageType)
		{
			if (_tracer == null)
				return;

			_tracer.WriteLine(msg, messageType);
		}

		#region Compiling

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
				return;

			_running = true;

			_project = project;
			_swcProjectSettings = swcProjectSettings;
			_tracer = tracer;

			PreBuild();
			Compile();

			_project = null;
			_swcProjectSettings = null;
			_tracer = null;

			_running = false;
		}

		private bool IncludeAsDoc()
		{
			string arguments = "";

			List<string> classExclusions = _swcProjectSettings.Flex_IgnoreClasses;

			// source-path	
            arguments += " -source-path ";
			foreach (string classPath in _project.Classpaths)
			{
				string absClassPath = GetProjectItemFullPath(classPath).ToLower();
				
				arguments += "\"" + absClassPath + "\" ";
			}
            
			// general options...
			// libarary-path			
			if (_project.CompilerOptions.LibraryPaths.Length > 0)
			{
                arguments += " -library-path ";
				foreach (string libPath in _project.CompilerOptions.LibraryPaths)
				{
					string absLibPath = GetProjectItemFullPath(libPath).ToLower();					
					arguments += "\"" + absLibPath + "\" ";
				}
			}

			// include-libraries
			if (_project.CompilerOptions.IncludeLibraries.Length > 0)
			{
                if(arguments.Contains("-library-path") == false)
				    arguments += " -library-path ";
				foreach (string libPath in _project.CompilerOptions.IncludeLibraries)
				{
					string absLibPath = GetProjectItemFullPath(libPath).ToLower();					
					arguments += "\"" + absLibPath + "\" ";
				}
			}

            // external-library-path 
            if (_project.CompilerOptions.ExternalLibraryPaths != null &&
                _project.CompilerOptions.ExternalLibraryPaths.Length > 0)
            {
                if (arguments.Contains("-library-path") == false)
                    arguments += " -library-path ";
                foreach (string libPath in _project.CompilerOptions.ExternalLibraryPaths)
                {
                    string absLibPath = GetProjectItemFullPath(libPath).ToLower();                   
                    arguments += "\"" + absLibPath + "\" ";
                }
            }

			if (classExclusions.Count > 0)
			{
				arguments += " -exclude-classes ";
				// exclude-classes
				List<string> origClassExclusions = classExclusions;
				classExclusions = new List<string>();
				for (int i = 0; i < origClassExclusions.Count; i++)
				{
					classExclusions.Add(GetProjectItemFullPath(origClassExclusions[i]).ToLower());
					arguments += classExclusions[classExclusions.Count - 1] + " ";
				}
			}

			arguments += " -doc-classes ";
			foreach (string classPath in _project.Classpaths)
			{
				string absClassPath = GetProjectItemFullPath(classPath).ToLower();
				arguments += IncludeClassesInAsDoc(absClassPath, string.Empty, classExclusions) + " ";
			}

            // no documentation for dependencies
            arguments += "-exclude-dependencies=true ";
			
            // the target-player
            arguments += "-target-player="+GetFlexSdkVersionString();

			string tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			Directory.CreateDirectory(tmpPath);

			WriteLine("Building AsDoc");
			WriteLine("AsDoc temp output: " + tmpPath);

			arguments = "-lenient=true -keep-xml=true -skip-xsl=true -output \"" + tmpPath + "\" " + arguments;
			//arguments += " -load-config=\"" + Path.Combine(ProjectPath.FullName, "obj/" + _project.Name + ".flex.compc.xml") + "\"";

			WriteLine("Start AsDoc: " + Path.Combine(FlexSdkBase, @"bin\asdoc.exe")+"\n"+arguments);

			ProcessRunner process = new PluginCore.Utilities.ProcessRunner();
			process.Error += new LineOutputHandler(process_Error);
			process.Output += new LineOutputHandler(process_Output);
			//process.WorkingDirectory = ProjectPath.FullName; // commented out as supposed by i.o. (http://www.flashdevelop.org/community/viewtopic.php?p=36764#p36764)
			process.RedirectInput = true;

			process.Run(Path.Combine(FlexSdkBase, @"bin\asdoc.exe"), arguments);

			while (process.IsRunning)
			{
				Thread.Sleep(5);
				Application.DoEvents();
			}

			WriteLine("AsDoc complete (" + process.HostedProcess.ExitCode + ")",
				process.HostedProcess.ExitCode == 0 ? TraceMessageType.Verbose : TraceMessageType.Error);

            if (process.HostedProcess.ExitCode != 0) // no errors
            {
				return false;
            }

			WriteLine("AsDoc created successfully, including in SWC...");

			try
			{
				FileStream fsZip = new FileStream(CompcBinPath_Flex, FileMode.Open, FileAccess.ReadWrite);

				ZipFile zipFile = new ZipFile(fsZip);

				zipFile.BeginUpdate();

				AddContentsOfDirectory(zipFile, Path.Combine(tmpPath, "tempdita"), Path.Combine(tmpPath, "tempdita"), "docs");

				zipFile.CommitUpdate();

				fsZip.Close();

				WriteLine("AsDoc integration complete (" + process.HostedProcess.ExitCode + ")",
				process.HostedProcess.ExitCode == 0 ? TraceMessageType.Verbose : TraceMessageType.Error);
			}
			catch (Exception exc)
			{
				WriteLine("Integration error " + exc.Message, TraceMessageType.Error);
			}
        
			// delete temporary directory
			Directory.Delete(tmpPath, true);

			return true;
		}

        private void AddContentsOfDirectory(ZipFile zipFile, string path, string basePath, string prefix)
        {
            string[] files = Directory.GetFiles(path);

            foreach (string fileName in files)
            {
                zipFile.Add(fileName, prefix + fileName.Replace(basePath, ""));
            }

            string[] directories = Directory.GetDirectories(path);
            foreach (string directoryPath in directories)
            {
                AddContentsOfDirectory(zipFile, directoryPath, basePath, "");
            }
        }

		

		private string IncludeClassesInAsDoc(string sourcePath, string parentPath, List<string> classExclusions)
		{
			string result = "";
			// take the current folder
			DirectoryInfo directory = new DirectoryInfo(sourcePath);
			// add every AS class
			foreach (FileInfo file in directory.GetFiles())
			{
				if (file.Extension == ".as" ||
					file.Extension == ".mxml")
				{
					if (!IsFileIgnored(file.FullName, classExclusions))
					{
						//CreateElement("class", includeClasses, parentPath + Path.GetFileNameWithoutExtension(file.FullName));
						result += parentPath + Path.GetFileNameWithoutExtension(file.FullName) + " ";
					}
				}
			}

			// process sub folders
			foreach (DirectoryInfo folder in directory.GetDirectories())
				result += IncludeClassesInAsDoc(folder.FullName, parentPath + folder.Name + ".", classExclusions);
			
			return result;
		}

		public void Compile(AS3Project project, SWCProject swcProjectSettings)
		{
			Compile(project, swcProjectSettings, null);
		}
		public void Compile(AS3Project project, SWCProject swcProjectSettings, ITraceable tracer)
		{
			if (_running)
				return;

			_running = true;

			_project = project;
			_swcProjectSettings = swcProjectSettings;
			_tracer = tracer;

			Compile();

			_project = null;
			_swcProjectSettings = null;
			_tracer = null;

			_running = false;
		}

		public void PreBuild(AS3Project project, SWCProject swcProjectSettings)
		{
			PreBuild(project, swcProjectSettings, null);
		}
		public void PreBuild(AS3Project project, SWCProject swcProjectSettings, ITraceable tracer)
		{
			if (_running)
				return;

			_running = true;

			_project = project;
			_swcProjectSettings = swcProjectSettings;
			_tracer = tracer;

			PreBuild();

			_project = null;
			_swcProjectSettings = null;
			_tracer = null;

			_running = false;
		}

		protected void Compile()
		{			
			bool buildSuccess = true;

			SaveModifiedDocuments();

			RunPreBuildEvent();

			buildSuccess &= RunCompc(CompcConfigPath_Flex);
			if (_swcProjectSettings.MakeCS3)
			{
				buildSuccess &= RunCompc(CompcConfigPath_Flash);
				PatchFlashSWC();
				if (_swcProjectSettings.LaunchAEM)
					buildSuccess &= BuildMXP();
			}
			if (buildSuccess || _project.AlwaysRunPostBuild)
				RunPostBuildEvent();

			_running = false;
		}

		protected void RunPreBuildEvent()
		{
			if (_project.PreBuildEvent.Trim().Length == 0)
				return;

			string command = FlashDevelop.Utilities.ArgsProcessor.ProcessString(_project.PreBuildEvent, true);

			Process process = new Process();
			ProcessStartInfo processStI = new ProcessStartInfo();
			processStI.FileName = "cmd.exe";
			processStI.Arguments = "/C " + command;
			processStI.CreateNoWindow = true;
			process.StartInfo = processStI;
			process.Start();

			//TraceManager.AddAsync("Running Pre-Build Command:\ncmd: " + command);
			WriteLine("Running Pre-Build Command:\ncmd: " + command);

			process.WaitForExit(15000);
		}

		protected void RunPostBuildEvent()
		{
			if (_project.PostBuildEvent.Trim().Length == 0)
				return;

			string command = FlashDevelop.Utilities.ArgsProcessor.ProcessString(_project.PostBuildEvent, true);

			Process process = new Process();
			ProcessStartInfo processStI = new ProcessStartInfo();
			processStI.FileName = "cmd.exe";
			processStI.Arguments = "/C " + command;
			processStI.CreateNoWindow = true;
			process.StartInfo = processStI;
			process.Start();

			//TraceManager.AddAsync("Running Post-Build Command:\ncmd: " + command);
			WriteLine("Running Post-Build Command:\ncmd: " + command);
		}

		protected void SaveModifiedDocuments()
		{
			if (PluginBase.MainForm.HasModifiedDocuments == false)
				return;

			foreach (ITabbedDocument document in PluginBase.MainForm.Documents)
			{
				if (document.IsModified)
					document.Save();
			}
		}

		protected bool BuildMXP()
		{
			// throw new NotImplementedException();
			ProcessStartInfo pi = new ProcessStartInfo();
			pi.UseShellExecute = true;
			pi.FileName = MXIPath;
			Process process = Process.Start(pi);

			bool success = process.WaitForExit(15000);

			return success && (process.ExitCode == 0);
		}

		protected void PatchFlashSWC()
		{
			bool livePreviewFile = false;
			string file = CompcBinPath_Flash;

			if (!File.Exists(file))
				return; // TODO: display error

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
			if (!_swcProjectSettings.ValidImage())
				LocaleHelper.GetImage("cs3_component_icon").Save(tempDir + "\\icon.png", ImageFormat.Png);
			else
				Image.FromFile(_swcProjectSettings.CS3_ComponentIconFile).Save(tempDir + "\\icon.png", ImageFormat.Png);
			XmlElement iel = CreateElement("file", catxml.DocumentElement["files"], string.Empty);
			iel.SetAttribute("path", "icon.png");
			iel.SetAttribute("mod", new FileInfo(tempDir + "\\icon.png").LastWriteTimeUtc.ToFileTimeUtc().ToString());

			// <component className="Symbol1" name="Symbol 1" icon="icon_0.png"  />
			if (catxml.DocumentElement["components"] == null)
				CreateElement("components", catxml.DocumentElement);
			XmlElement cel = CreateElement("component", catxml.DocumentElement["components"]);
			cel.SetAttribute("className", _swcProjectSettings.CS3_ComponentClass);
			cel.SetAttribute("name", _swcProjectSettings.CS3_ComponentName);
			cel.SetAttribute("icon", "icon.png");
			cel.SetAttribute("tooltip", _swcProjectSettings.CS3_ComponentToolTip);

			// livePreview
			if (_swcProjectSettings.ValidLivePreview())
			{
				if (_swcProjectSettings.CS3_PreviewType == SWCProject.CS3_PreviewType_ENUM.ExternalSWF)
				{
					// livePreview exists
					File.Copy(_swcProjectSettings.CS3_PreviewResource, tempDir + "\\livePreview.swf");
					livePreviewFile = true;
				}
				else
				{
					// MAKE BUILDSCRIPT
					string lpfile = BuildLivePreview();
					if (lpfile == string.Empty)
					{
						//TraceManager.AddAsync("*** Error building live preview from class: " + _swcProjectSettings.CS3_PreviewResource);
						WriteLine("*** Error building live preview from class: " + _swcProjectSettings.CS3_PreviewResource, TraceMessageType.Error);
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

				//TraceManager.AddAsync("Flash SWC ready: " + file);
				WriteLine("Flash SWC ready: " + file);
			}
		}

		protected string BuildLivePreview()
		{
			if (FindClassPath(_swcProjectSettings.CS3_PreviewResource) == string.Empty)
				return string.Empty;

			//string tproFile = TempFile(Path.GetTempPath().Trim('\\') + "\\flashdevelop_swf\\", ".as3proj");
			//string tswfFile = TempFile(Path.GetTempPath().Trim('\\') + "\\flashdevelop_swf\\", ".swf"); 
			string tproFile = TempFile(ProjectPath.FullName, ".as3proj");
			string tswfFile = TempFile(ProjectPath.FullName, ".swf");

			XmlDocument lpc = new XmlDocument();
			lpc.Load(_project.ProjectPath);
			XmlNodeList opnl = lpc.DocumentElement["output"].ChildNodes;
			foreach (XmlNode node in opnl)
				if (node.Attributes[0].Name == "path")
					node.Attributes[0].Value = Path.GetFileName(tswfFile);

			lpc.DocumentElement["compileTargets"].RemoveAll();
			XmlElement el = CreateElement("compile", lpc.DocumentElement["compileTargets"]);
			el.SetAttribute("path", FindClassPath(_swcProjectSettings.CS3_PreviewResource));

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
			string tcon = _project.Directory.TrimEnd('\\') + "\\obj\\" + Path.GetFileNameWithoutExtension(tproFile) + "Config.xml";
			if (File.Exists(tcon))
				File.Delete(tcon);
			return tswfFile;
		}

		protected string FindClassPath(string className)
		{
			foreach (string cpath_ in _project.Classpaths)
			{

				string cpath = cpath_.Contains(":") ? cpath_ : Path.Combine(_project.Directory, cpath_);
				string[] files = Directory.GetFiles(cpath, "*.as", SearchOption.AllDirectories);
				foreach (string file in files)
				{
					if (Path.GetFileNameWithoutExtension(file) == className)
						return (Path.GetDirectoryName(file).TrimEnd('\\')
							+ "\\" + className + ".as")
							.Replace(_project.Directory.TrimEnd('\\')
							+ "\\", "");
				}
			}
			return string.Empty;
		}

		protected string TempFile(string path, string ext)
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

		protected void SwcAdd(ZipOutputStream str, string file)
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

		protected bool RunCompc(string confpath)
		{
			try
			{
				// get the project root and compc.exe location from the command argument
				FileInfo compc = new FileInfo(FlexSdkBase + "\\bin\\compc.exe");
				if ((!ProjectPath.Exists) | (!compc.Exists))
					throw new FileNotFoundException("Project or compc.exe not found", ProjectPath.FullName + "|" + compc.FullName);

				// generate arguments based on config, additional configs, and additional user arguments
				string cmdArgs = "-load-config+=\"" + confpath + "\"";
				if (_project.CompilerOptions.LoadConfig != string.Empty)
					cmdArgs += " -load-config+=\"" + _project.CompilerOptions.LoadConfig + "\"";
				/* changed for new project manager core */
				//if (Project.CompilerOptions.Additional != string.Empty)
				//cmdArgs += " " + Project.CompilerOptions.Additional;
				if (_project.CompilerOptions.Additional.Length > 0)
					foreach (string op in _project.CompilerOptions.Additional)
						cmdArgs += " " + op;

				_anyErrors = false;

				// start the compc.exe process with arguments
				ProcessRunner process = new PluginCore.Utilities.ProcessRunner();
				process.Error += new LineOutputHandler(process_Error);
				process.Output += new LineOutputHandler(process_Output);
				//process.WorkingDirectory = ProjectPath.FullName; // commented out as supposed by i.o. (http://www.flashdevelop.org/community/viewtopic.php?p=36764#p36764)
				process.RedirectInput = true;				
				process.Run(compc.FullName, cmdArgs);

				PluginBase.MainForm.StatusLabel.Text = (process.IsRunning ? "Build started..." : "Unable to start build. Check output.");
				//TraceManager.Add((process.IsRunning ? "Running Process:" : "Unable to run Process:"));				
				//TraceManager.Add("\"" + compc.FullName + "\" " + cmdArgs);
				WriteLine((process.IsRunning ? "Running Process:" : "Unable to run Process:"));
				WriteLine("\"" + compc.FullName + "\" " + cmdArgs);

				while (process.IsRunning)
				{
					Thread.Sleep(5);
					Application.DoEvents();
				}

				AS3Project project = _project;
				bool checkForIllegalCrossThreadCalls = Control.CheckForIllegalCrossThreadCalls;
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
			NotifyEvent ne = new NotifyEvent(EventType.ProcessStart);
			EventManager.DispatchEvent(this, ne);

			CreateCompcConfig(CompcConfigPath_Flex, CompcBinPath_Flex, true, _swcProjectSettings.Flex_IgnoreClasses);
			if (_swcProjectSettings.FlexIncludeASI)
				PreBuild_Asi();
			if (_swcProjectSettings.MakeCS3)
			{
				CreateCompcConfig(CompcConfigPath_Flash, CompcBinPath_Flash, false, _swcProjectSettings.CS3_IgnoreClasses);
				if (_swcProjectSettings.MakeMXI)
				{
					if (_swcProjectSettings.MXPIncludeASI && !_swcProjectSettings.FlexIncludeASI)
						PreBuild_Asi();
					PreBuild_Mxi();
				}
			}			
		}

		protected void PreBuild_Asi()
		{
			string outdir = ASIDir;
			if (!Directory.Exists(outdir))
				Directory.CreateDirectory(outdir);
			List<string> asfiles = new List<string>();

			foreach (string path in _project.Classpaths)
			{
				string rpath = path.Contains(":")
					? path.Trim('\\') + "\\"
					: (_project.Directory + "\\" + path.Trim('\\') + "\\").Replace("\\\\", "\\");
				asfiles.AddRange(Directory.GetFiles(rpath, "*.as", SearchOption.AllDirectories));
			}

			foreach (string infile in asfiles)
				MakeIntrinsic(infile, outdir);
		}

		protected void MakeIntrinsic(string infile, string outdir)
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

		protected void PreBuild_Mxi()
		{
			string outfile = MXIPath;
			XmlDocument doc = new XmlDocument();
			doc.LoadXml("<?xml version=\"1.0\" encoding=\"UTF-8\" ?><macromedia-extension />");
			doc.DocumentElement.SetAttribute("name", _swcProjectSettings.CS3_ComponentName);
			_swcProjectSettings.IncrementVersion(0, 0, 1);
			_swcProjectSettings.Save(SWCProjectSettingsPath);
			doc.DocumentElement.SetAttribute("version", _swcProjectSettings.MXIVersion);
			doc.DocumentElement.SetAttribute("type", "flashcomponentswc");
			doc.DocumentElement.SetAttribute("requires-restart", "false");
			CreateElement("author", doc.DocumentElement).SetAttribute("name", _swcProjectSettings.MXIAuthor);
			CreateElement("description", doc.DocumentElement).InnerXml = "<![CDATA[ " + _swcProjectSettings.MXIDescription + " ]]>";
			CreateElement("ui-access", doc.DocumentElement).InnerXml = "<![CDATA[ " + _swcProjectSettings.MXIUIAccessText + " ]]>";
			XmlElement pro = CreateElement("product", CreateElement("products", doc.DocumentElement));
			pro.SetAttribute("name", "Flash");
			pro.SetAttribute("version", "9");
			pro.SetAttribute("primary", "true");
			XmlElement fil = CreateElement("file", CreateElement("files", doc.DocumentElement));
			fil.SetAttribute("name", Path.GetFileName(CompcBinPath_Flash));
			fil.SetAttribute("destination", "$flash/Components/" + _swcProjectSettings.CS3_ComponentGroup);

			doc.Save(outfile);
		}

		protected void CreateCompcConfig(string confout, string binout, bool rsl, List<string> classExclusions)
		{
			//TraceManager.Add("Prebuilding config " + confout + "...");
			WriteLine("Prebuilding config " + confout + "...");

			// build the config file
			XmlDocument config = new XmlDocument();

			config.LoadXml("<?xml version=\"1.0\"?><flex-config/>");

			// general options...
			// output
			CreateElement("output", config.DocumentElement, binout);

			// use-network
			CreateElement("use-network", config.DocumentElement, _project.CompilerOptions.UseNetwork.ToString().ToLower());

			// target
			CreateElement("target-player", config.DocumentElement, GetFlexSdkVersionString());

			// warnings
			CreateElement("warnings", config.DocumentElement, _project.CompilerOptions.Warnings.ToString().ToLower());

			// locale
			if (_project.CompilerOptions.Locale != string.Empty)
				CreateElement("locale", config.DocumentElement, _project.CompilerOptions.Locale);

			// runtime-shared-libraries
			if (_project.CompilerOptions.RSLPaths.Length > 0)
			{
				XmlElement rslUrls = CreateElement("runtime-shared-libraries", config.DocumentElement, null);
				foreach (string rslUrl in _project.CompilerOptions.RSLPaths)
					CreateElement("rsl-url", rslUrls, rslUrl);
			}

			// benchmark
			CreateElement("benchmark", config.DocumentElement, _project.CompilerOptions.Benchmark.ToString().ToLower());

			// compiler options...
			XmlElement compiler = CreateElement("compiler", config.DocumentElement, null);

			// compute-digest
			if (!rsl)
				CreateElement("compute-digest", config.DocumentElement, "false");

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
			XmlElement sourcePath = CreateElement("source-path", compiler, null);
			foreach (string classPath in _project.Classpaths)
			{
				string absClassPath = GetProjectItemFullPath(classPath).ToLower();
				CreateElement("path-element", sourcePath, absClassPath);
			}

			// general options...
			// libarary-path			
			if (_project.CompilerOptions.LibraryPaths.Length > 0)
			{
				XmlElement includeLibraries = CreateElement("library-path", compiler, null);
				foreach (string libPath in _project.CompilerOptions.LibraryPaths)
				{
					string absLibPath = GetProjectItemFullPath(libPath).ToLower();
					CreateElement("path-element", includeLibraries, absLibPath);
				}
			}

			// include-libraries
			if (_project.CompilerOptions.IncludeLibraries.Length > 0)
			{
				XmlElement includeLibraries = CreateElement("include-libraries", compiler, null);
				foreach (string libPath in _project.CompilerOptions.IncludeLibraries)
				{
					string absLibPath = GetProjectItemFullPath(libPath).ToLower();
					CreateElement("library", includeLibraries, absLibPath);
				}
			}

			// include-classes
			List<string> origClassExclusions = classExclusions;
			classExclusions = new List<string>();
			for (int i = 0; i < origClassExclusions.Count; i++)
				classExclusions.Add(GetProjectItemFullPath(origClassExclusions[i]).ToLower());

			XmlElement includeClasses = CreateElement("include-classes", config.DocumentElement, null);
			foreach (string classPath in _project.Classpaths)
			{
				string absClassPath = GetProjectItemFullPath(classPath).ToLower();
				IncludeClassesIn(includeClasses, absClassPath, string.Empty, classExclusions);
			}

			// external-library-path 
			if (_project.CompilerOptions.ExternalLibraryPaths != null && _project.CompilerOptions.ExternalLibraryPaths.Length > 0)
			{
				XmlElement externalLibs = CreateElement("external-library-path", compiler, null);
				XmlAttribute attr = externalLibs.OwnerDocument.CreateAttribute("append");
				attr.InnerXml = "true";
				externalLibs.Attributes.Append(attr);

				foreach (string libPath in _project.CompilerOptions.ExternalLibraryPaths)
				{
					string absLibPath = GetProjectItemFullPath(libPath).ToLower();
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
			DirectoryInfo directory = new DirectoryInfo(sourcePath);
			// add every AS class to the manifest
			foreach (FileInfo file in directory.GetFiles())
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
			foreach (DirectoryInfo folder in directory.GetDirectories())
				IncludeClassesIn(includeClasses, folder.FullName, parentPath + folder.Name + ".", classExclusions);
		}
		#endregion


		#region properties
		protected string CompcConfigPath_Flex
		{
			get { return LibMakerDir + _project.Name + ".flex.compc.xml"; }
		}

		protected string CompcBinPath_Flex
		{
			//get { return LibMakerDir + Project.Name + ".flex.swc"; }
			get { return Path.Combine(ProjectPath.FullName, _swcProjectSettings.FlexBinPath); }
		}

		protected string CompcConfigPath_Flash
		{
			get { return LibMakerDir + _project.Name + ".flash.compc.xml"; }
		}

		protected string CompcBinPath_Flash
		{
			//get { return LibMakerDir + Project.Name + ".flash.swc"; }
			get { return Path.Combine(ProjectPath.FullName, _swcProjectSettings.FlashBinPath); }
		}

		protected string ASIDir
		{
			get { return ProjectPath.FullName + "\\asi\\"; }
		}

		protected string MXIPath
		{
			get { return LibMakerDir + _project.Name + ".mxi"; }
		}

		protected string SWCProjectSettingsPath
		{
			get { return ProjectPath.FullName + "\\" + _project.Name + ".lxml"; }
			//get { return ProjectPath.FullName + "\\SWCSettings.lxml"; }
		}

		/// <summary>
		/// The Flex SDK base path.
		/// </summary>
		protected string FlexSdkBase
		{
			get 
			{
				// Patch: Use custom SDK path (if available)
				if (_project == null ||
					string.IsNullOrEmpty(_project.CompilerOptions.CustomSDK) == true)
					return AS3Context.PluginMain.Settings.FlexSDK;
				return _project.CompilerOptions.CustomSDK;
			}
		}

        /// <summary>
        /// The Flex SDK Version.
        /// </summary>
        protected Version FlexSdkVersion
        {
            get
            {
                XmlDocument doc = new XmlDocument();
				doc.Load(Path.Combine(FlexSdkBase, "flex-sdk-description.xml"));

                XmlNode versionNode = doc.SelectSingleNode("flex-sdk-description/version");
                XmlNode buildNode = doc.SelectSingleNode("flex-sdk-description/build");

                string[] versionParts = versionNode.InnerText.Split(new char[]{'.'},
                                                                    StringSplitOptions.RemoveEmptyEntries);
                
                Version version = new Version(int.Parse(versionParts[0]),
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
					return true;
				return false;
			}
		}

		protected DirectoryInfo ProjectPath
		{
			get { return new DirectoryInfo(_project.Directory); }
		}

		protected string LibMakerDir
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

		protected XmlElement CreateElement(string name, XmlElement parent)
		{
			return CreateElement(name, parent, string.Empty);
		}

		protected XmlElement CreateElement(string name, XmlElement parent, string innerText)
		{
			XmlElement element = parent.OwnerDocument.CreateElement(name, parent.OwnerDocument.DocumentElement.NamespaceURI);
			if (innerText != null && innerText != string.Empty)
				element.InnerText = innerText;
			parent.AppendChild(element);

			return element;
		}

		protected bool IsFileIgnored(string file, List<string> classExclusions)
		{
			string filePath = GetProjectItemFullPath(file);

			if (classExclusions.Contains(filePath.ToLower()))
				return true;
			return false;
		}

		protected string GetProjectItemFullPath(string path)
		{
			if (Path.IsPathRooted(path))
				return path;

			return Path.GetFullPath(ProjectPath.FullName + "\\" + path);
		}

		protected string GetFlexSdkVersionString()
		{
			int version = ((AS3Project)_project).MovieOptions.Version;
			
			if (version < 11)
				return version.ToString();
			if (version == 11)
				return "10.1";
			return (version - 1).ToString();
		}
		#endregion


		protected void process_Output(object sender, string line)
		{
			//TraceManager.AddAsync(line);
		}

		protected void process_Error(object sender, string line)
		{
			_anyErrors = true;
			//TraceManager.AddAsync(line, 3);
			WriteLine(line, TraceMessageType.Error);
		}
	}
}
