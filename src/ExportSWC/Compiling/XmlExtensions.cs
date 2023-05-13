using System.Xml;

namespace ExportSWC.Compiling
{
    internal static class XmlExtensions
    {
        public static XmlElement CreateElement(this XmlElement parent, string name)
        {
            return parent.CreateElement(name, string.Empty);
        }

        public static XmlElement CreateElement(this XmlElement parent, string name, string innerText)
        {
            var element = parent.OwnerDocument.CreateElement(name, parent.OwnerDocument.DocumentElement.NamespaceURI);
            if (innerText != null && innerText != string.Empty)
            {
                element.InnerText = innerText;
            }

            parent.AppendChild(element);

            return element;
        }
    }
}
