using System.Collections.Generic;
using System.IO;
using System.Xml;
using ExportSWC.Tracing;

namespace ExportSWC
{
    public partial class SWCBuilder
    {
        private XmlElement CreateElement(string name, XmlElement parent)
        {
            return CreateElement(name, parent, string.Empty);
        }

        private XmlElement CreateElement(string name, XmlElement parent, string innerText)
        {
            var element = parent.OwnerDocument.CreateElement(name, parent.OwnerDocument.DocumentElement.NamespaceURI);
            if (innerText != null && innerText != string.Empty)
            {
                element.InnerText = innerText;
            }

            parent.AppendChild(element);

            return element;
        }

        private bool IsFileIgnored(string file, List<string> classExclusions)
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

            return Path.GetFullPath(ProjectPath.FullName + "\\" + path);
        }

        private string GetTargetVersionString()
        {
            return _project.MovieOptions.Version;
        }

        private FileInfo GetExeOrBatPath(string filepath)
        {
            var fileInfo = new FileInfo(filepath);
            if (!fileInfo.Exists)
            {
                var batFilepath = Path.ChangeExtension(filepath, ".bat");
                fileInfo = new FileInfo(batFilepath);
                if ((!ProjectPath.Exists) | (!fileInfo.Exists))
                {
                    throw new FileNotFoundException($"{filepath} not found", fileInfo.FullName);
                }
            }

            return fileInfo;
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
