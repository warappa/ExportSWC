using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using ASCompletion.Context;
using ExportSWC.Compiling;
using ExportSWC.Tracing;
using ExportSWC.Tracing.Interfaces;
using ExportSWC.Utils;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.CodeAnalysis.Semantics;
using Mono.CSharp;
using PluginCore;
using PluginCore.Utilities;
using ProjectManager.Projects.AS3;

namespace ExportSWC.AsDoc
{
    internal class AsDocGenerator
    {
        private static readonly Regex sourceRegex = new Regex("Text for description in (?<fullname>(?<namespace>[a-zA-Z0-9.]+):?(?<classname>[a-zA-Z0-9.]+)(/((?<modifier>[a-z]+):)?(?<member>[a-zA-Z0-9_]+))?)");
        private static readonly Regex diagnosticsRegex = new Regex("lineNumber: (?<linenumber>[0-9]+); columnNumber: (?<columnnumber>[0-9]+); (?<message>.*)");
        private const int _asdocLineNumberReportCorrectionDivisor = 2;
        private const int _cdataLength = 6;
        private const int _fdColumnOffset = 1;
        private const int _carriageReturnOffset = 1;

        private readonly ITraceable _tracer;
        private bool _anyErrors;

        public AsDocGenerator(ITraceable tracer)
        {
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        }

        public bool IncludeAsDoc(AsDocContext context)
        {
            var project = context.Project;

            WriteLine("");
            WriteLine("Building documentation", TraceMessageType.Message);
            if (!File.Exists(context.FlexOutputPath))
            {
                WriteLine($"File '{context.FlexOutputPath}' not found");
                return false;
            }

            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            var configFilepath = context.AsDocConfigPath;

            CreateAsDocConfig(context, configFilepath, tempPath, true, context.SwcProjectSettings.FlexIgnoreClasses);
            var cmdArgs = CreateAsDocConfigFileArguments(context, project, configFilepath);

            WriteLine("asdoc temp output: " + tempPath);

            var asDocPath = Path.Combine(context.SdkBase, @"bin\asdoc.exe");
            var asdoc = PathUtils.GetExeOrBatPath(asDocPath)
                ?? throw new FileNotFoundException("asdoc not found", asDocPath);

            WriteLine($"Start asdoc: {asdoc.FullName}\n{cmdArgs}");

            var success = RunAsDoc(context, tempPath, cmdArgs, asdoc, out var exitCode);

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

        private static string CreateAsDocConfigFileArguments(AsDocContext context, AS3Project project, string configFilepath)
        {
            var cmdArgs = $"";

            // prevent flaky builds by specifying configname explicitly
            cmdArgs += $" +configname={(context.IsAir ? "air" : "flex")}";

            // generate arguments based on config, additional configs, and additional user arguments
            cmdArgs += $@" -load-config+=""{configFilepath}""";
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

            return cmdArgs;
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
            /**
             * asdoc validation messages are exported to a file. The problem is that this output has double(!) the linebreaks than the source (and therefore FlashDevelop)!
             * To correct this we need to divide the reported line numbers by 2.
             * 
             * Also, asdoc strips the '*' in its comments representation, so the column number is not in line with the original found in source code. We can work around
             * by finding the corresponding line in both sources, and then go char by char until the column value is reached. If both differ, just move on to the
             * next FlashDevelop comment character while the asdoc one stays on the same index until the same value is found again where both again increment.
            **/

            try
            {
                // parse
                var toplevelX = XDocument.Load(toplevelXmlFilepath);

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
                        var originalComments = "CDATA[" + (entry.XPathEvaluate("description/text()") as IEnumerable)
                                .OfType<XCData>()
                                .FirstOrDefault()
                                ?.Value;
                        var projectRelativeSource = source.Substring(context.ProjectFullPath.Length + 1);

                        if (!enumerator.MoveNext())
                        {
                            break;
                        }
                        var diagosticsLine = enumerator.Current;

                        var exceptionMatch = diagnosticsRegex.Match(diagosticsLine);
                        if (exceptionMatch.Success)
                        {
                            int.TryParse(exceptionMatch.Groups["columnnumber"].Value, out var column);
                            int.TryParse(exceptionMatch.Groups["linenumber"].Value, out var originalLineNumber);

                            var correctedOriginalLineNumber = (originalLineNumber + 1) / _asdocLineNumberReportCorrectionDivisor;
                            var absoluteFdLineNumber = 1;
                            var message = exceptionMatch.Groups["message"]?.Value;
                            var fileModel = ASContext.Context.GetFileModel(projectRelativeSource);
                            var memberLineFrom = 0;
                            var comments = "";
                            var isMember = false;

                            column -= _fdColumnOffset; // FD is 0-based!

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
                                            isMember = true;
                                        }
                                    }
                                    else
                                    {
                                        comments = classModel.Comments;
                                        memberLineFrom = classModel.LineFrom + 1;
                                    }
                                }

                                var commentLines = comments.Where(x => x == '\r').Count() + 1;

                                absoluteFdLineNumber = commentLines - (correctedOriginalLineNumber - 1);
                                absoluteFdLineNumber = memberLineFrom - absoluteFdLineNumber;
                            }

                            var commentsLineStartIndex = GetStartIndexOfLineNumber(correctedOriginalLineNumber, comments, '\r', 0);
                            var commentsLineStartIndexOrig = GetStartIndexOfLineNumber(originalLineNumber, originalComments, '\n', _cdataLength);

                            if (correctedOriginalLineNumber == 1)
                            {
                                column += -_cdataLength - 1;

                                var additionalSpace = CalulateAdditionalSpace(column, originalComments, comments, commentsLineStartIndex, commentsLineStartIndexOrig);

                                var memberColumnFrom = CalculateCommentColumnIndentation(comments, isMember);
                                column += memberColumnFrom + additionalSpace;
                            }
                            else
                            {
                                var additionalSpace = CalulateAdditionalSpace(column, originalComments, comments, commentsLineStartIndex, commentsLineStartIndexOrig);

                                column += additionalSpace - _carriageReturnOffset;
                            }

                            var resultMessage = $"{projectRelativeSource}({absoluteFdLineNumber},{column}): {message}";
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

        /// <summary>
        /// As there is no offset information we can only try heuristics
        /// </summary>
        /// <param name="comments"></param>
        /// <returns></returns>
        private int CalculateCommentColumnIndentation(string comments, bool isMember)
        {
            var indent = isMember ? 5 : 4;
            var i = 0;
            for (; i < comments.Length; i++)
            {
                var c = comments[i];
                if (c == '\r')
                {
                    c++;
                    break;
                }
            }
            for (; i < comments.Length; i++)
            {
                var c = comments[i];
                if (c == '\r')
                {
                    indent = 0;
                }
                else if (char.IsWhiteSpace(c))
                {
                    indent++;
                }
                else
                {
                    break;
                }
            }

            return indent + 2;
        }

        private static int GetStartIndexOfLineNumber(int lineNumber, string comments, char newLineCharacter, int offset)
        {
            var commentsLineStartIndex = offset;
            var lineCounter = 1;
            for (; commentsLineStartIndex < comments.Length; commentsLineStartIndex++)
            {
                if (lineCounter >= lineNumber)
                {
                    break;
                }

                var c = comments[commentsLineStartIndex];
                if (c == newLineCharacter)
                {
                    lineCounter++;
                }

                if (lineCounter >= lineNumber)
                {
                    commentsLineStartIndex++;
                    break;
                }
            }

            return commentsLineStartIndex;
        }

        /// <summary>
        /// As asterisks are stripped from the validation logs we have to manually calculate how many characters before the column need to be added to match with source column.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="originalComments"></param>
        /// <param name="comments"></param>
        /// <param name="commentsLineStartIndex"></param>
        /// <param name="commentsLineStartIndexOrig"></param>
        /// <returns></returns>
        private static int CalulateAdditionalSpace(int column, string originalComments, string comments, int commentsLineStartIndex, int commentsLineStartIndexOrig)
        {
            var additionalSpace = 0;
            var hadAsterisk = false;
            char? firstNonWhitespace = null;

            for (int origI = commentsLineStartIndexOrig, fdI = commentsLineStartIndex; origI - commentsLineStartIndexOrig < column;)
            {
                var cFd = comments[fdI];
                var cOrig = originalComments[origI];

                if (firstNonWhitespace is null &&
                    !char.IsWhiteSpace(cFd))
                {
                    firstNonWhitespace = cFd;
                }

                if (cFd == '*')
                {
                    hadAsterisk = true;

                    do
                    {
                        fdI++;
                        additionalSpace++;
                        if (fdI < comments.Length &&
                            comments[fdI] == ' ')
                        {
                            fdI++;
                            additionalSpace++;
                        }
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

        protected void CreateAsDocConfig(AsDocContext context, string configFilepath, string outputDirectorypath, bool isRuntimeSharedLibrary, List<string> classExclusions)
        {
            var project = context.Project;

            configFilepath = $@"{configFilepath.TrimEnd('\\', '/')}\";

            WriteLine("Prebuilding asdoc config " + configFilepath + "...");

            // build the config file
            var config = new XmlDocument();

            config.LoadXml("<?xml version=\"1.0\"?><flex-config/>");

            //// general options...
            //// output
            config.DocumentElement.CreateElement("output", outputDirectorypath);

            // target
            config.DocumentElement.CreateElement("target-player", context.TargetVersion);

            // use-network
            config.DocumentElement.CreateElement("use-network", project.CompilerOptions.UseNetwork.ToString().ToLower());

            // warnings
            config.DocumentElement.CreateElement("warnings", project.CompilerOptions.Warnings.ToString().ToLower());

            // benchmark
            config.DocumentElement.CreateElement("benchmark", project.CompilerOptions.Benchmark.ToString().ToLower());

            // compiler options...
            var compiler = config.DocumentElement.CreateElement("compiler", null!);

            compiler.CreateElement("debug", (!PluginMain.IsReleaseBuild(project)).ToString().ToLower());

            // locale
            if (!string.IsNullOrEmpty(context.Project.CompilerOptions.Locale))
            {
                var localeX = compiler.CreateElement("locale", "");
                localeX.CreateElement("locale-element", context.Project.CompilerOptions.Locale);
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

            // compute-digest
            if (!isRuntimeSharedLibrary)
            {
                config.DocumentElement.CreateElement("compute-digest", "false");
            }

            // libarary-path			
            if (project.CompilerOptions.LibraryPaths.Length > 0)
            {
                var includeLibraries = compiler.CreateElement("library-path", null!);
                foreach (var libPath in project.CompilerOptions.LibraryPaths)
                {
                    var absLibPath = PathUtils.GetProjectItemFullPath(context.ProjectFullPath, libPath).ToLower();
                    includeLibraries.CreateElement("path-element", absLibPath);
                }
            }

            // include-libraries
            if (project.CompilerOptions.IncludeLibraries.Length > 0)
            {
                var includeLibraries = compiler.CreateElement("include-libraries", null!);
                foreach (var libPath in project.CompilerOptions.IncludeLibraries)
                {
                    var absLibPath = PathUtils.GetProjectItemFullPath(context.ProjectFullPath, libPath).ToLower();
                    includeLibraries.CreateElement("library", absLibPath);
                }
            }

            // external-library-path 
            if (project.CompilerOptions.ExternalLibraryPaths != null &&
                project.CompilerOptions.ExternalLibraryPaths.Length > 0)
            {
                var externalLibs = compiler.CreateElement("external-library-path", null!);
                var attr = externalLibs.OwnerDocument.CreateAttribute("append");
                attr.InnerXml = "true";
                externalLibs.Attributes.Append(attr);

                foreach (var libPath in project.CompilerOptions.ExternalLibraryPaths)
                {
                    var absLibPath = PathUtils.GetProjectItemFullPath(context.ProjectFullPath, libPath).ToLower();
                    externalLibs.CreateElement("path-element", absLibPath);
                }
            }

            // runtime-shared-libraries
            if (project.CompilerOptions.RSLPaths.Length > 0)
            {
                var rslUrls = config.DocumentElement.CreateElement("runtime-shared-libraries", null!);
                foreach (var rslUrl in project.CompilerOptions.RSLPaths)
                {
                    rslUrls.CreateElement("rsl-url", rslUrl);
                }
            }

            // source-path
            var sourcePath = compiler.CreateElement("source-path", null!);
            foreach (var classPath in project.Classpaths)
            {
                var absClassPath = PathUtils.GetProjectItemFullPath(context.ProjectFullPath, classPath).ToLower();
                sourcePath.CreateElement("path-element", absClassPath);
            }

            // doc-classes
            var origClassExclusions = classExclusions;
            classExclusions = new List<string>();
            for (var i = 0; i < origClassExclusions.Count; i++)
            {
                classExclusions.Add(PathUtils.GetProjectItemFullPath(context.ProjectFullPath, origClassExclusions[i]).ToLower());
            }

            var docClasses = config.DocumentElement.CreateElement("doc-classes", null!);
            foreach (var classPath in project.Classpaths)
            {
                var absClassPath = PathUtils.GetProjectItemFullPath(context.ProjectFullPath, classPath).ToLower();
                DocClassesIn(docClasses, context.ProjectFullPath, absClassPath, string.Empty, "class", classExclusions);
            }

            // lenient
            config.DocumentElement.CreateElement("lenient", "true");

            // lenient
            config.DocumentElement.CreateElement("keep-xml", "true");

            // lenient
            config.DocumentElement.CreateElement("skip-xsl", "true");

            // add namespace, save config to obj folder
            config.DocumentElement.SetAttribute("xmlns", "http://www.adobe.com/2006/flex-config");
            config.Save(configFilepath);
            
            WriteLine("asdoc Configuration written to: " + configFilepath);
        }

        protected void DocClassesIn(XmlElement includeClasses, string projectPath, string sourcePath, string parentPath, string elementTag, List<string> classExclusions)
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
                if (file.Extension is
                    ".as" or
                    ".mxml")
                {
                    if (!PathUtils.IsFileIgnored(projectPath, file.FullName, classExclusions))
                    {
                        includeClasses.CreateElement(elementTag, parentPath + Path.GetFileNameWithoutExtension(file.FullName));
                    }
                }
            }

            // process sub folders
            foreach (var folder in directory.GetDirectories())
            {
                DocClassesIn(includeClasses, projectPath, folder.FullName, parentPath + folder.Name + ".", elementTag, classExclusions);
            }
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
