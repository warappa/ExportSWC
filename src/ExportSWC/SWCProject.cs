using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Xml.Serialization;

namespace ExportSWC
{
    [Serializable]
    public class SWCProject
    {
        public SWCProject()
        {

        }

        private string flexBinPath = "";
        public string FlexBinPath
        {
            get => flexBinPath;
            set => flexBinPath = value;
        }

        private string flashBinPath = "";
        public string FlashBinPath
        {
            get => flashBinPath;
            set => flashBinPath = value;
        }

        private bool flexIncludeAsi;
        public bool FlexIncludeASI
        {
            get => flexIncludeAsi;
            set => flexIncludeAsi = value;
        }

        private bool makeCS3;
        public bool MakeCS3
        {
            get => makeCS3;
            set => makeCS3 = value;
        }

        private bool makeMXI;
        public bool MakeMXI
        {
            get => makeMXI;
            set => makeMXI = value;
        }

        private bool integrateAsDoc = true;
        public bool IntegrateAsDoc
        {
            get => integrateAsDoc;
            set => integrateAsDoc = value;
        }

        private bool launchAEM;
        public bool LaunchAEM
        {
            get => launchAEM;
            set => launchAEM = value;
        }

        private string mxiVersion;
        public string MXIVersion
        {
            get => mxiVersion;
            set => mxiVersion = value;
        }

        private bool mxpIncludeAsi;
        public bool MXPIncludeASI
        {
            get => mxpIncludeAsi;
            set => mxpIncludeAsi = value;
        }

        private string mxiAuthor;
        public string MXIAuthor
        {
            get => mxiAuthor;
            set => mxiAuthor = value;
        }

        private string mxiDescription;
        public string MXIDescription
        {
            get => mxiDescription;
            set => mxiDescription = value;
        }

        private string mxiUIAccessText;
        public string MXIUIAccessText
        {
            get => mxiUIAccessText;
            set => mxiUIAccessText = value;
        }

        #region Flex SWC

        private List<string> flex_ignoreClasses = new List<string>();
        public List<string> Flex_IgnoreClasses
        {
            get => flex_ignoreClasses;
            set => flex_ignoreClasses = value;
        }

        #endregion

        #region Flash CS3

        private List<string> cs3_ignoreClasses = new List<string>();
        public List<string> CS3_IgnoreClasses
        {
            get => cs3_ignoreClasses;
            set => cs3_ignoreClasses = value;
        }

        private string cs3_componentClass;
        public string CS3_ComponentClass
        {
            get => cs3_componentClass;
            set => cs3_componentClass = value;
        }

        private string cs3_componentName;
        public string CS3_ComponentName
        {
            get => cs3_componentName;
            set => cs3_componentName = value;
        }

        private string cs3_componentGroup;
        public string CS3_ComponentGroup
        {
            get => cs3_componentGroup;
            set => cs3_componentGroup = value;
        }

        private string cs3_componentToolTip;
        public string CS3_ComponentToolTip
        {
            get => cs3_componentToolTip;
            set => cs3_componentToolTip = value;
        }

        private string cs3_componentIconFile;
        public string CS3_ComponentIconFile
        {
            get => cs3_componentIconFile;
            set => cs3_componentIconFile = value;
        }

        private CS3_PreviewType_ENUM cs3_previewType;
        public CS3_PreviewType_ENUM CS3_PreviewType
        {
            get => cs3_previewType;
            set => cs3_previewType = value;
        }

        private string cs3_previewResource;
        public string CS3_PreviewResource
        {
            get => cs3_previewResource;
            set => cs3_previewResource = value;
        }

        #endregion

        public void Save(string filename)
        {
            var xms = new XmlSerializer(typeof(SWCProject));
            var sw = new StreamWriter(filename, false);
            xms.Serialize(sw, this);
            sw.Flush();
            sw.Close();
        }

        public static SWCProject Load(string filename)
        {
            if (!File.Exists(filename))
            {
                var newproj = new SWCProject();
                newproj.Save(filename);
                return newproj;
            }

            var xms = new XmlSerializer(typeof(SWCProject));
            var sw = new StreamReader(filename);
            var output = xms.Deserialize(sw);
            sw.Close();
            return (SWCProject)output;
        }

        public enum CS3_PreviewType_ENUM
        {
            None,
            ExternalSWF,
            Class
        }

        internal bool ValidImage()
        {
            if (!File.Exists(cs3_componentIconFile))
            {
                return false;
            }

            var img = Image.FromFile(cs3_componentIconFile);
            var h = img.Size.Height;
            var w = img.Size.Width;
            img.Dispose();
            return h == 18 && w == 18;
        }

        internal bool ValidLivePreview()
        {
            switch (cs3_previewType)
            {
                case CS3_PreviewType_ENUM.ExternalSWF:
                    return File.Exists(cs3_previewResource);
                case CS3_PreviewType_ENUM.Class:
                    return true;
                default:
                    return false;
            }
        }

        internal void IncrementVersion(int a, int b, int c)
        {
            if (mxiVersion == null)
            {
                mxiVersion = "0.0.0";
            }

            var vers = mxiVersion.Split('.');
            if (vers.Length != 3)
            {
                vers = new string[] { "0", "0", "0", "0" };
            }

            vers[0] = (Convert.ToInt32(vers[0]) + a).ToString();
            vers[1] = (Convert.ToInt32(vers[1]) + b).ToString();
            vers[2] = (Convert.ToInt32(vers[2]) + c).ToString();

            mxiVersion = string.Format("{0}.{1}.{2}", vers[0], vers[1], vers[2]);
        }
    }
}