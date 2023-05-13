using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ExportSWC.Tracing;
using ICSharpCode.SharpZipLib.Zip;
using PluginCore.Utilities;

namespace ExportSWC
{
    public partial class SWCBuilder
    {
        private bool IncludeAsDoc()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tmpPath);

            var arguments = BuildAsDocArguments();

            WriteLine("Building AsDoc");
            WriteLine("AsDoc temp output: " + tmpPath);

            arguments = $@"-lenient=true -keep-xml=true -skip-xsl=true -output ""{tmpPath}"" {arguments}";
            //arguments += " -load-config=\"" + Path.Combine(ProjectPath.FullName, "obj/" + _project.Name + ".flex.compc.xml") + "\"";

            var asdoc = GetExeOrBatPath(Path.Combine(FlexSdkBase, @"bin\asdoc.exe"));
            if (asdoc is null)
            {
                throw new FileNotFoundException("asdoc not found", Path.Combine(FlexSdkBase, @"bin\asdoc.exe"));
            }

            WriteLine($"Start AsDoc: {asdoc.FullName}\n{arguments}");
            
            var success = RunAsDoc(arguments, asdoc, out var exitCode);

            WriteLine($"AsDoc complete ({exitCode})", success ? TraceMessageType.Verbose : TraceMessageType.Error);

            if (!success)
            {
                return false;
            }

            WriteLine("AsDoc created successfully, including in SWC...");

            try
            {
                MergeAsDocIntoSWC(tmpPath);

                WriteLine($"AsDoc integration complete ({exitCode})",
                    success ? TraceMessageType.Verbose : TraceMessageType.Error);
            }
            catch (Exception exc)
            {
                WriteLine($"Integration error {exc.Message}", TraceMessageType.Error);
            }

            // delete temporary directory
            Directory.Delete(tmpPath, true);

            return true;
        }

        private bool RunAsDoc(string arguments, FileInfo asdoc, out int exitCode)
        {
            var process = new ProcessRunner();
            process.Error += new LineOutputHandler(ProcessError);
            process.Output += new LineOutputHandler(ProcessOutput);
            //process.WorkingDirectory = ProjectPath.FullName; // commented out as supposed by i.o. (http://www.flashdevelop.org/community/viewtopic.php?p=36764#p36764)
            process.RedirectInput = true;

            process.Run(asdoc.FullName, arguments);

            while (process.IsRunning)
            {
                Thread.Sleep(5);
                Application.DoEvents();
            }

            exitCode = process.HostedProcess.ExitCode;

            var success = exitCode == 0;
            
            return success;
        }

        private string BuildAsDocArguments()
        {
            var arguments = "";

            // source-path	
            arguments += " -source-path ";
            foreach (var classPath in _project.Classpaths)
            {
                var absClassPath = GetProjectItemFullPath(classPath).ToLower();

                arguments += $@"""{absClassPath}"" ";
            }

            // general options...
            // libarary-path			
            if (_project.CompilerOptions.LibraryPaths.Length > 0)
            {
                arguments += " -library-path ";
                foreach (var libPath in _project.CompilerOptions.LibraryPaths)
                {
                    var absLibPath = GetProjectItemFullPath(libPath).ToLower();
                    arguments += $@"""{absLibPath}"" ";
                }
            }

            // include-libraries
            if (_project.CompilerOptions.IncludeLibraries.Length > 0)
            {
                if (arguments.Contains("-library-path") == false)
                {
                    arguments += " -library-path ";
                }

                foreach (var libPath in _project.CompilerOptions.IncludeLibraries)
                {
                    var absLibPath = GetProjectItemFullPath(libPath).ToLower();
                    arguments += $@"""{absLibPath}"" ";
                }
            }

            // external-library-path 
            if (_project.CompilerOptions.ExternalLibraryPaths != null &&
                _project.CompilerOptions.ExternalLibraryPaths.Length > 0)
            {
                if (arguments.Contains("-library-path") == false)
                {
                    arguments += " -library-path ";
                }

                foreach (var libPath in _project.CompilerOptions.ExternalLibraryPaths)
                {
                    var absLibPath = GetProjectItemFullPath(libPath).ToLower();
                    arguments += $@"""{absLibPath}"" ";
                }
            }

            var classExclusions = _swcProjectSettings.FlexIgnoreClasses;
            if (classExclusions.Count > 0)
            {
                arguments += " -exclude-classes ";
                // exclude-classes
                var origClassExclusions = classExclusions;
                classExclusions = new List<string>();
                for (var i = 0; i < origClassExclusions.Count; i++)
                {
                    classExclusions.Add(GetProjectItemFullPath(origClassExclusions[i]).ToLower());
                    arguments += classExclusions[classExclusions.Count - 1] + " ";
                }
            }

            arguments += " -doc-classes ";
            foreach (var classPath in _project.Classpaths)
            {
                var absClassPath = GetProjectItemFullPath(classPath).ToLower();
                arguments += IncludeClassesInAsDoc(absClassPath, string.Empty, classExclusions) + " ";
            }

            // no documentation for dependencies
            arguments += "-exclude-dependencies=true ";

            if (IsAIR())
            {
                arguments += "+configname=air ";
            }
            else
            {
                // the target-player
                arguments += $"-target-player={GetTargetVersionString()}";
            }

            return arguments;
        }

        private void MergeAsDocIntoSWC(string tmpPath)
        {
            using var fsZip = new FileStream(CompcBinPath_Flex, FileMode.Open, FileAccess.ReadWrite);
            using (var zipFile = new ZipFile(fsZip))
            {
                zipFile.BeginUpdate();

                AddContentsOfDirectory(zipFile, Path.Combine(tmpPath, "tempdita"), Path.Combine(tmpPath, "tempdita"), "docs");

                zipFile.CommitUpdate();
            }
        }

        private void AddContentsOfDirectory(ZipFile zipFile, string path, string basePath, string prefix)
        {
            var files = Directory.GetFiles(path);

            foreach (var fileName in files)
            {
                zipFile.Add(fileName, prefix + fileName.Replace(basePath, ""));
            }

            var directories = Directory.GetDirectories(path);
            foreach (var directoryPath in directories)
            {
                AddContentsOfDirectory(zipFile, directoryPath, basePath, "");
            }
        }

        private string IncludeClassesInAsDoc(string sourcePath, string parentPath, List<string> classExclusions)
        {
            var result = "";
            // take the current folder
            var directory = new DirectoryInfo(sourcePath);
            // add every AS class
            foreach (var file in directory.GetFiles())
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
            foreach (var folder in directory.GetDirectories())
            {
                result += IncludeClassesInAsDoc(folder.FullName, parentPath + folder.Name + ".", classExclusions);
            }

            return result;
        }
    }
}
