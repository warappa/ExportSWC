﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ExportSWC.Tracing;
using ExportSWC.Tracing.Interfaces;
using ICSharpCode.SharpZipLib.Zip;
using PluginCore.Utilities;

namespace ExportSWC
{
    internal class AsDocGenerator
    {
        private readonly ITraceable _tracer;
        private bool _anyErrors;

        public AsDocGenerator(ITraceable tracer)
        {
            _tracer = tracer;
        }

        public bool IncludeAsDoc(AsDocContext context)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            var arguments = BuildAsDocArguments(context, tempPath);

            WriteLine("Building AsDoc");
            WriteLine("AsDoc temp output: " + tempPath);


            var asDocPath = Path.Combine(context.FlexSdkBase, @"bin\asdoc.exe");
            var asdoc = FileUtils.GetExeOrBatPath(asDocPath);
            if (asdoc is null)
            {
                throw new FileNotFoundException("asdoc not found", asDocPath);
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
                MergeAsDocIntoSWC(tempPath, context.FlexOutputPath);

                WriteLine($"AsDoc integration complete ({exitCode})",
                    success ? TraceMessageType.Verbose : TraceMessageType.Error);
            }
            catch (Exception exc)
            {
                WriteLine($"Integration error {exc.Message}", TraceMessageType.Error);
            }

            // delete temporary directory
            Directory.Delete(tempPath, true);

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

        private string BuildAsDocArguments(AsDocContext context, string tempPath)
        {
            var project = context.Project;
            var projectFullPath = context.ProjectFullPath;

            var arguments = "";

            // source-path	
            arguments += " -source-path ";
            foreach (var classPath in project.Classpaths)
            {
                var absClassPath = PathUtils.GetProjectItemFullPath(projectFullPath, classPath).ToLower();

                arguments += $@"""{absClassPath}"" ";
            }

            // general options...
            // libarary-path			
            if (project.CompilerOptions.LibraryPaths.Length > 0)
            {
                arguments += " -library-path ";
                foreach (var libPath in project.CompilerOptions.LibraryPaths)
                {
                    var absLibPath = PathUtils.GetProjectItemFullPath(projectFullPath, libPath).ToLower();
                    arguments += $@"""{absLibPath}"" ";
                }
            }

            // include-libraries
            if (project.CompilerOptions.IncludeLibraries.Length > 0)
            {
                if (arguments.Contains("-library-path") == false)
                {
                    arguments += " -library-path ";
                }

                foreach (var libPath in project.CompilerOptions.IncludeLibraries)
                {
                    var absLibPath = PathUtils.GetProjectItemFullPath(projectFullPath, libPath).ToLower();
                    arguments += $@"""{absLibPath}"" ";
                }
            }

            // external-library-path 
            if (project.CompilerOptions.ExternalLibraryPaths != null &&
                project.CompilerOptions.ExternalLibraryPaths.Length > 0)
            {
                if (arguments.Contains("-library-path") == false)
                {
                    arguments += " -library-path ";
                }

                foreach (var libPath in project.CompilerOptions.ExternalLibraryPaths)
                {
                    var absLibPath = PathUtils.GetProjectItemFullPath(projectFullPath, libPath).ToLower();
                    arguments += $@"""{absLibPath}"" ";
                }
            }

            var classExclusions = context.FlexIgnoreClasses;
            if (classExclusions.Count > 0)
            {
                arguments += " -exclude-classes ";
                // exclude-classes
                var origClassExclusions = classExclusions;
                classExclusions = new List<string>();
                for (var i = 0; i < origClassExclusions.Count; i++)
                {
                    classExclusions.Add(PathUtils.GetProjectItemFullPath(projectFullPath, origClassExclusions[i]).ToLower());
                    arguments += classExclusions[classExclusions.Count - 1] + " ";
                }
            }

            arguments += " -doc-classes ";
            foreach (var classPath in project.Classpaths)
            {
                var absClassPath = PathUtils.GetProjectItemFullPath(projectFullPath, classPath).ToLower();
                arguments += IncludeClassesInAsDoc(context, absClassPath, string.Empty, classExclusions) + " ";
            }

            // no documentation for dependencies
            arguments += "-exclude-dependencies=true ";

            if (context.IsAir)
            {
                arguments += "+configname=air ";
            }
            else
            {
                // the target-player
                arguments += $"-target-player={context.FlashPlayerTargetVersion}";
            }

            arguments = $@"-lenient=true -keep-xml=true -skip-xsl=true -output ""{tempPath}"" {arguments}";
            //arguments += " -load-config=\"" + Path.Combine(ProjectPath.FullName, "obj/" + _project.Name + ".flex.compc.xml") + "\"";


            return arguments;
        }

        private void MergeAsDocIntoSWC(string tmpPath, string targetSwcFile)
        {
            using var fsZip = new FileStream(targetSwcFile, FileMode.Open, FileAccess.ReadWrite); //CompcBinPath_Flex
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

        private string IncludeClassesInAsDoc(AsDocContext context, string sourcePath, string parentPath, List<string> classExclusions)
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
                    if (!PathUtils.IsFileIgnored(context.ProjectFullPath, file.FullName, classExclusions))
                    {
                        //CreateElement("class", includeClasses, parentPath + Path.GetFileNameWithoutExtension(file.FullName));
                        result += parentPath + Path.GetFileNameWithoutExtension(file.FullName) + " ";
                    }
                }
            }

            // process sub folders
            foreach (var folder in directory.GetDirectories())
            {
                result += IncludeClassesInAsDoc(context, folder.FullName, parentPath + folder.Name + ".", classExclusions);
            }

            return result;
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
