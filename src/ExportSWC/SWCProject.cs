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
        public string FlexBinPath { get; set; } = "";

        public string FlashBinPath { get; set; } = "";

        public bool FlexIncludeASI { get; set; }

        
        public bool MakeCS3 { get; set; }

        public bool MakeMXI { get; set; }

        public bool IntegrateAsDoc { get; set; }

        public bool LaunchAEM { get; set; }

        public string MXIVersion { get; set; }

        public bool MXPIncludeASI { get; set; }

        public string MXIAuthor { get; set; }

        public string MXIDescription { get; set; }

        public string MXIUIAccessText { get; set; }

        #region Flex SWC

        public List<string> FlexIgnoreClasses { get; set; } = new List<string>();

        #endregion

        #region Flash CS3

        public List<string> CS3IgnoreClasses { get; set; } = new List<string>();

        public string CS3ComponentClass { get; set; }

        public string CS3ComponentName { get; set; }

        public string CS3ComponentGroup { get; set; }

        public string CS3ComponentToolTip { get; set; }

        public string CS3ComponentIconFile { get; set; }

        public CS3PreviewType CS3PreviewType { get; set; }

        public string CS3PreviewResource { get; set; }

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

        internal bool ValidImage()
        {
            if (!File.Exists(CS3ComponentIconFile))
            {
                return false;
            }

            var img = Image.FromFile(CS3ComponentIconFile);
            var h = img.Size.Height;
            var w = img.Size.Width;
            img.Dispose();
            return h == 18 && w == 18;
        }

        internal bool ValidLivePreview()
        {
            switch (CS3PreviewType)
            {
                case CS3PreviewType.ExternalSWF:
                    return File.Exists(CS3PreviewResource);
                case CS3PreviewType.Class:
                    return true;
                default:
                    return false;
            }
        }

        internal void IncrementVersion(int a, int b, int c)
        {
            if (MXIVersion == null)
            {
                MXIVersion = "0.0.0";
            }

            var vers = MXIVersion.Split('.');
            if (vers.Length != 3)
            {
                vers = new string[] { "0", "0", "0", "0" };
            }

            vers[0] = (Convert.ToInt32(vers[0]) + a).ToString();
            vers[1] = (Convert.ToInt32(vers[1]) + b).ToString();
            vers[2] = (Convert.ToInt32(vers[2]) + c).ToString();

            MXIVersion = string.Format("{0}.{1}.{2}", vers[0], vers[1], vers[2]);
        }
    }
}
