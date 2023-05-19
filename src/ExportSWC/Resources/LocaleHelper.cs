using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Resources;
using PluginCore.Localization;

namespace ExportSWC.Resources
{
    internal class LocaleHelper
    {
        private static ResourceManager? resources;
        private static string[] _names;

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

        private static Image? GetImageFromEmbeddedResource(string identifier)
        {
            _names ??= Assembly.GetExecutingAssembly().GetManifestResourceNames();

            var resourceName = $"ExportSWC.Resources.{identifier}";
            if (_names.Contains(resourceName))
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                return Image.FromStream(stream);
            }

            return null;
        }

        public static Image GetImage(string identifier)
        {
            return (Image)resources!.GetObject(identifier) ?? GetImageFromEmbeddedResource(identifier);
        }
    }
}
