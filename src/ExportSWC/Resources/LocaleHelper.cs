using System.Drawing;
using System.Reflection;
using System.Resources;
using PluginCore.Localization;

namespace ExportSWC.Resources
{
    internal class LocaleHelper
    {
        private static ResourceManager? resources;

        /// <summary>
        /// Initializes the localization of the plugin
        /// </summary>
        public static void Initialize(LocaleVersion locale)
        {
            var path = "ExportSWC.Resources." + locale.ToString();
            resources = new ResourceManager(path, Assembly.GetExecutingAssembly());
        }

        /// <summary>
        /// Loads a string from the internal resources
        /// </summary>
        public static string GetString(string identifier)
        {
            return resources!.GetString(identifier);
        }

        public static Image GetImage(string identifier)
        {
            return (Image)resources!.GetObject(identifier);
        }
    }
}
