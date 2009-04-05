using System;
using System.Text;
using System.Resources;
using System.Reflection;
using System.Collections.Generic;
using PluginCore.Localization;
using PluginCore;
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
            String path = "ExportSWC.Resources." + locale.ToString();
            resources = new ResourceManager(path, Assembly.GetExecutingAssembly());
        }

        /// <summary>
        /// Loads a string from the internal resources
        /// </summary>
        public static String GetString(String identifier)
        {
            return resources.GetString(identifier);
        }

		public static Image GetImage(String identifier) {
			return (Image)resources.GetObject(identifier);
		}

    }

}
