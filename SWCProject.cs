using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Drawing;

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
			get { return flexBinPath; }
			set { flexBinPath = value; }
		}

		private string flashBinPath = "";
		public string FlashBinPath
		{
			get { return flashBinPath; }
			set { flashBinPath = value; }
		}

		private bool flexIncludeAsi;
		public bool FlexIncludeASI
		{
			get { return flexIncludeAsi; }
			set { flexIncludeAsi = value; }
		}

		private bool makeCS3;
		public bool MakeCS3
		{
			get { return makeCS3; }
			set { makeCS3 = value; }
		}

		private bool makeMXI;
		public bool MakeMXI
		{
			get { return makeMXI; }
			set { makeMXI = value; }
		}

		private bool launchAEM;
		public bool LaunchAEM
		{
			get { return launchAEM; }
			set { launchAEM = value; }
		}

		private string mxiVersion;
		public string MXIVersion
		{
			get { return mxiVersion; }
			set { mxiVersion = value; }
		}

		private bool mxpIncludeAsi;
		public bool MXPIncludeASI
		{
			get { return mxpIncludeAsi; }
			set { mxpIncludeAsi = value; }
		}

		private string mxiAuthor;
		public string MXIAuthor
		{
			get { return mxiAuthor; }
			set { mxiAuthor = value; }
		}

		private string mxiDescription;
		public string MXIDescription
		{
			get { return mxiDescription; }
			set { mxiDescription = value; }
		}

		private string mxiUIAccessText;
		public string MXIUIAccessText
		{
			get { return mxiUIAccessText; }
			set { mxiUIAccessText = value; }
		}

		#region Flex SWC

		private List<string> flex_ignoreClasses = new List<string>();
		public List<string> Flex_IgnoreClasses
		{
			get { return flex_ignoreClasses; }
			set { flex_ignoreClasses = value; }
		}

		#endregion

		#region Flash CS3

		private List<string> cs3_ignoreClasses = new List<string>();
		public List<string> CS3_IgnoreClasses
		{
			get { return cs3_ignoreClasses; }
			set { cs3_ignoreClasses = value; }
		}

		private string cs3_componentClass;
		public string CS3_ComponentClass
		{
			get { return cs3_componentClass; }
			set { cs3_componentClass = value; }
		}

		private string cs3_componentName;
		public string CS3_ComponentName
		{
			get { return cs3_componentName; }
			set { cs3_componentName = value; }
		}

		private string cs3_componentGroup;
		public string CS3_ComponentGroup
		{
			get { return cs3_componentGroup; }
			set { cs3_componentGroup = value; }
		}

		private string cs3_componentToolTip;
		public string CS3_ComponentToolTip
		{
			get { return cs3_componentToolTip; }
			set { cs3_componentToolTip = value; }
		}

		private string cs3_componentIconFile;
		public string CS3_ComponentIconFile
		{
			get { return cs3_componentIconFile; }
			set { cs3_componentIconFile = value; }
		}

		private CS3_PreviewType_ENUM cs3_previewType;
		public CS3_PreviewType_ENUM CS3_PreviewType
		{
			get { return cs3_previewType; }
			set { cs3_previewType = value; }
		}

		private string cs3_previewResource;
		public string CS3_PreviewResource
		{
			get { return cs3_previewResource; }
			set { cs3_previewResource = value; }
		}

		#endregion

		public void Save(string filename)
		{
			XmlSerializer xms = new XmlSerializer(typeof(SWCProject));
			StreamWriter sw = new StreamWriter(filename, false);
			xms.Serialize(sw, this);
			sw.Flush();
			sw.Close();
		}

		public static SWCProject Load(string filename)
		{
			if (!File.Exists(filename))
			{
				SWCProject newproj = new SWCProject();
				newproj.Save(filename);
				return newproj;
			}
			XmlSerializer xms = new XmlSerializer(typeof(SWCProject));
			StreamReader sw = new StreamReader(filename);
			object output = xms.Deserialize(sw);
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
				return false;
			Image img = Image.FromFile(cs3_componentIconFile);
			int h = img.Size.Height;
			int w = img.Size.Width;
			img.Dispose();
			return h == 18 && w == 18;
		}

		internal bool ValidLivePreview()
		{
			switch (this.cs3_previewType)
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
				mxiVersion = "0.0.0";
			string[] vers = mxiVersion.Split('.');
			if (vers.Length != 3)
				vers = new string[] { "0", "0", "0", "0" };
			vers[0] = (Convert.ToInt32(vers[0]) + a).ToString();
			vers[1] = (Convert.ToInt32(vers[1]) + b).ToString();
			vers[2] = (Convert.ToInt32(vers[2]) + c).ToString();

			mxiVersion = String.Format("{0}.{1}.{2}", vers[0], vers[1], vers[2]);
		}
	}

}
