using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.XPath;
using ASCompletion.Context;
using ASCompletion.Model;
using ExportSWC.Tracing;
using ExportSWC.Tracing.Interfaces;
using ExportSWC.Utils;
using ICSharpCode.SharpZipLib.Zip;
using PluginCore;
using PluginCore.Utilities;
using ProjectManager.Projects.AS3;

namespace ExportSWC.AsDoc
{
    internal class AsDocGenerator
    {
        private const int _asdocLineNumberReportCorrectionDivisor = 2;

        private readonly ITraceable _tracer;
        private bool _anyErrors;

        public AsDocGenerator(ITraceable tracer)
        {
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        }

        public bool IncludeAsDoc(AsDocContext context)
        {
            WriteLine("");
            WriteLine("Building documentation", TraceMessageType.Message);
            if (!File.Exists(context.FlexOutputPath))
            {
                WriteLine($"File '{context.FlexOutputPath}' not found");
                return false;
            }

            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            var arguments = BuildAsDocArguments(context, tempPath);


            WriteLine("asdoc temp output: " + tempPath);

            var asDocPath = Path.Combine(context.SdkBase, @"bin\asdoc.exe");
            var asdoc = PathUtils.GetExeOrBatPath(asDocPath)
                ?? throw new FileNotFoundException("asdoc not found", asDocPath);

            WriteLine($"Start asdoc: {asdoc.FullName}\n{arguments}");

            var success = RunAsDoc(context, tempPath, arguments, asdoc, out var exitCode);

            WriteLine($"asdoc complete ({exitCode})", success ? TraceMessageType.Verbose : TraceMessageType.Error);

            if (!success)
            {
                return false;
            }

            WriteLine("asdoc created successfully, including in SWC...");

            try
            {
                MergeAsDocIntoSWC(tempPath, context.FlexOutputPath);

                WriteLine("");
                WriteLine($"asdoc integration complete ({exitCode})",
                    success ? TraceMessageType.Verbose : TraceMessageType.Error);
            }
            catch (Exception exc)
            {
                WriteLine("");
                WriteLine($"Integration error {exc.Message}", TraceMessageType.Error);
            }

            // delete temporary directory
            Directory.Delete(tempPath, true);

            return true;
        }

        private bool RunAsDoc(AsDocContext context, string tempPath, string arguments, FileInfo asdoc, out int exitCode)
        {
            var project = context.Project;
            var env = new Dictionary<string, string>();

            // Apache Flex compat
            env.SetApacheFlexCompatibilityEnvironment(context.Project);

            var process = new ProcessRunnerExtended();
            process.Error += new LineOutputHandler(ProcessError);
            process.Output += new LineOutputHandler(ProcessOutput);
            //process.WorkingDirectory = ProjectPath.FullName; // commented out as supposed by i.o. (http://www.flashdevelop.org/community/viewtopic.php?p=36764#p36764)
            process.RedirectInput = true;

            process.Run(asdoc.FullName, arguments, env);

            WriteLine("");

            while (process.IsRunning)
            {
                Thread.Sleep(5);
                Application.DoEvents();
            }

            WriteLine("");

            exitCode = process.HostedProcess!.ExitCode;

            var success = exitCode == 0 && !_anyErrors;

            var validationErrorLogPath = Path.Combine(tempPath, "validation_errors.log");
            if (File.Exists(validationErrorLogPath))
            {
                var toplevelXmlFilepath = Path.Combine(tempPath, "toplevel.xml");

                var handled = false;
                if (File.Exists(toplevelXmlFilepath))
                {
                    handled = TryParseValidationErrorsLog(context, project, validationErrorLogPath, toplevelXmlFilepath);
                }

                if (!handled)
                {
                    var content = File.ReadAllText(validationErrorLogPath);
                    WriteLine("");
                    WriteLine("Error details:", TraceMessageType.Error);
                    WriteLine(content, TraceMessageType.Error);
                }
            }

            return success;
        }

        private bool TryParseValidationErrorsLog(AsDocContext context, AS3Project project, string validationErrorLogPath, string toplevelXmlFilepath)
        {
            try
            {
                // parse
                var toplevelX = XDocument.Load(toplevelXmlFilepath);

                var sourceRegex = new Regex("Text for description in (?<fullname>(?<namespace>[a-zA-Z0-9.]+):?(?<classname>[a-zA-Z0-9.]+)(/(?<modifier>[a-z]+):?(?<member>[a-zA-Z0-9_]+))?)");
                var positionRegex = new Regex("lineNumber: (?<linenumber>[0-9]+); columnNumber: (?<columnnumber>[0-9]+); (?<message>.*)");

                var lines = File.ReadLines(validationErrorLogPath);
                var enumerator = lines.GetEnumerator();
                string? line = null;
                while (true)
                {
                    if (line is null)
                    {
                        if (!enumerator.MoveNext())
                        {
                            break;
                        }

                        line = enumerator.Current;
                    }

                    if (line is null)
                    {
                        break;
                    }

                    var sourceMatch = sourceRegex.Match(line);
                    if (sourceMatch.Success)
                    {
                        var fullname = sourceMatch.Groups["fullname"].Value;
                        var @namespace = sourceMatch.Groups["namespace"].Value;
                        var classname = sourceMatch.Groups["classname"].Value;
                        var membername = sourceMatch.Groups["member"].Value;

                        var entry = (toplevelX.XPathEvaluate($"//*[@fullname='{fullname}']") as IEnumerable).OfType<XElement>().FirstOrDefault();

                        var source = entry.Attribute("sourcefile")?.Value;
                        if (source is null)
                        {
                            var classX = (toplevelX.XPathEvaluate($"//*[@fullname='{(@namespace != "" ? @namespace + ":" : "")}{classname}']") as IEnumerable)
                                .OfType<XElement>()
                                .FirstOrDefault();
                            source = classX!.Attribute("sourcefile").Value;
                        }
                        var originalComments = "<!CDATA[" + (entry.XPathEvaluate("description/text()") as IEnumerable)
                                .OfType<XCData>()
                                .FirstOrDefault()
                                ?.Value;
                        var projectRelativeSource = source.Substring(context.ProjectFullPath.Length + 1);

                        if (!enumerator.MoveNext())
                        {
                            break;
                        }
                        var exception = enumerator.Current;

                        var exceptionMatch = positionRegex.Match(exception);
                        if (exceptionMatch.Success)
                        {
                            int.TryParse(exceptionMatch.Groups["columnnumber"].Value, out var column);
                            int.TryParse(exceptionMatch.Groups["linenumber"].Value, out var originalLineNumber);

                            var correctedOriginalLineNumber = (originalLineNumber + 1) / _asdocLineNumberReportCorrectionDivisor;
                            var lineNumber = 1;
                            var message = exceptionMatch.Groups["message"]?.Value;
                            var fileModel = ASContext.Context.GetFileModel(projectRelativeSource);
                            var memberLineFrom = 0;
                            var memberColumnFrom = 0;
                            var comments = "";

                            column -= 1; // FD is 0-based!

                            if (fileModel is not null)
                            {
                                var classModel = fileModel?.GetClassByName(classname);
                                if (classModel is not null)
                                {
                                    if (membername != "")
                                    {
                                        var memberModel = classModel.Members.FirstOrDefault(x => x.Name == membername);
                                        if (memberModel is not null)
                                        {
                                            comments = memberModel.Comments;
                                            memberLineFrom = memberModel.LineFrom + 1;
                                            memberColumnFrom = memberModel.StartPosition;
                                        }
                                    }
                                    else
                                    {
                                        comments = classModel.Comments;
                                        memberLineFrom = classModel.LineFrom + 1;
                                        memberColumnFrom = classModel.StartPosition;
                                    }
                                }


                                var commentLines = comments.Where(x => x == '\r').Count() + 1;

                                lineNumber = commentLines - (correctedOriginalLineNumber - 1);
                                lineNumber = memberLineFrom - lineNumber;
                            }

                            var commentLine = "";
                            var lineCounter = 1;
                            var commentsLineStartIndex = 0;
                            for (; commentsLineStartIndex < comments.Length; commentsLineStartIndex++)
                            {
                                if (lineCounter >= correctedOriginalLineNumber)
                                {
                                    break;
                                }

                                var c = comments[commentsLineStartIndex];
                                if (c == '\r')
                                {
                                    lineCounter++;

                                }

                                if (lineCounter >= correctedOriginalLineNumber)
                                {
                                    commentsLineStartIndex++;
                                    break;
                                }

                            }

                            var lineCounterOrig = 1;
                            var commentsLineStartIndexOrig = 8;
                            for (; commentsLineStartIndexOrig < originalComments.Length; commentsLineStartIndexOrig++)
                            {
                                if (lineCounterOrig >= originalLineNumber)
                                {
                                    break;
                                }

                                var c = originalComments[commentsLineStartIndexOrig];
                                if (c == '\n')
                                {
                                    lineCounterOrig++;

                                }

                                if (lineCounterOrig >= originalLineNumber)
                                {
                                    commentsLineStartIndexOrig++;
                                    break;
                                }

                            }

                            if (lineCounter == 1)
                            {
                                column += -7 + 2 - 1;

                                var additionalSpace = CalulateAdditionalSpace(column, originalComments, comments, commentsLineStartIndex, commentsLineStartIndexOrig);

                                column += additionalSpace;
                                // adjust indentation
                                column += membername == "" ? 4 : 5;
                            }
                            else
                            {
                                var additionalSpace = CalulateAdditionalSpace(column, originalComments, comments, commentsLineStartIndex, commentsLineStartIndexOrig);

                                column += additionalSpace;
                            }

                            var resultMessage = $"{projectRelativeSource}({lineNumber},{column}): {message}";
                            WriteLine(resultMessage, TraceMessageType.Error);
                        }
                        line = null;
                    }
                    else
                    {
                        line = null;
                    }
                }

                WriteLine("");
            }
            catch
            {
                return false;
            }

            return true;
        }

        private static int CalulateAdditionalSpace(int column, string originalComments, string comments, int commentsLineStartIndex, int commentsLineStartIndexOrig)
        {
            var additionalSpace = 0;
            for (int origI = commentsLineStartIndexOrig, fdI = commentsLineStartIndex; origI - commentsLineStartIndexOrig < column;)
            {
                var cFd = comments[fdI];
                var cOrig = originalComments[origI];

                if (cFd == '*')
                {
                    do
                    {
                        fdI++;
                        additionalSpace++;
                    } while (fdI < comments.Length && ((cFd = comments[fdI]) == '*'));
                    fdI--;
                }
                else if (cFd == cOrig)
                {
                    origI++;
                }
                else
                {

                }
                fdI++;
            }

            return additionalSpace;
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

            //if (context.IsAir)
            {
                arguments += $"+configname={(context.IsAir ? "air" : "flex")} ";
            }
            //else
            {
                // the target-player
                arguments += $"-target-player={context.TargetVersion} ";
            }

            // locale
            if (!string.IsNullOrEmpty(context.Project.CompilerOptions.Locale))
            {
                arguments += $"-locale={context.Project.CompilerOptions.Locale} ";
            }

            arguments = $@"-lenient=true -keep-xml=true -skip-xsl=true -output ""{tempPath}"" {arguments}";
            //arguments += " -load-config=\"" + Path.Combine(ProjectPath.FullName, "obj/" + _project.Name + ".flex.compc.xml") + "\"";


            return arguments;
        }

        private void MergeAsDocIntoSWC(string tmpPath, string targetSwcFile)
        {
            WriteLine("");
            WriteLine("Merge documentation into SWC", TraceMessageType.Message);

            using var fsZip = new FileStream(targetSwcFile, FileMode.Open, FileAccess.ReadWrite); //CompcBinPath_Flex
            using var zipFile = new ZipFile(fsZip);

            zipFile.BeginUpdate();

            AddContentsOfDirectory(zipFile, Path.Combine(tmpPath, "tempdita"), Path.Combine(tmpPath, "tempdita"), "docs");

            zipFile.CommitUpdate();

            WriteLine("Merging complete", TraceMessageType.Verbose);
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
                if (file.Extension is
                    ".as" or
                    ".mxml")
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
            WriteLine($"  asdoc: {line}", TraceMessageType.Verbose);
        }

        private void ProcessError(object sender, string line)
        {
            //TraceManager.AddAsync(line, 3);
            var isError = line.StartsWithOrdinal("Error:") ||
                line.StartsWithOrdinal("[Error]") ||
                line.StartsWithOrdinal("[Fatal Error]");
            var level = TraceMessageType.Warning;
            if (isError)
            {
                level = TraceMessageType.Error;
                _anyErrors = true;
            }
            WriteLine($"  asdoc: {line}", level);
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
