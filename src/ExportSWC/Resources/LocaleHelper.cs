using System;
using System.Resources;
using System.Reflection;
using PluginCore.Localization;
using System.Drawing;

namespace ExportSWC.Resources
{
    class LocaleHelper
    {
        private static ResourceManager resources = null;

        /// <summary>
        /// Initializes the localization of the plugin
        /// </summary>
        public static void Initialize(LocaleVersion locale)
        {
            string path = "ExportSWC.Resources." + locale.ToString();
            resources = new ResourceManager(path, Assembly.GetExecutingAssembly());
        }

        /// <summary>
        /// Loads a string from the internal resources
        /// </summary>
        public static string GetString(string identifier)
        {
            return resources.GetString(identifier);
        }

		public static Image GetImage(string identifier) {
			return (Image)resources.GetObject(identifier);
		}

    }

}
