using System.Xml;
using ExportSWC.Tracing;
using ProjectManager.Projects;

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
