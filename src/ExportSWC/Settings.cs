using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Text;

namespace ExportSWC
{
	[Serializable]
	public class Settings
	{
		#region ARCHIVED_CODE_UNUSED
		// no longer using but kept for implementation reference
		/*public static string[] DEFAULT_NAMESPACES = new string[] { "http://tempuri.org" };
		private string[] _namespaces = DEFAULT_NAMESPACES;
		[Category("Compiler"), Description("Specify any namespaces to pass to the compiler."), DefaultValue(new string[] { "http://tempuri.org" })]
		public string[] NameSpaces {
			get { return _namespaces; }
			set { _namespaces = value; }
		}

		const bool DEFAULT_DELETEMANIFEST = true;
		private bool _deleteManifest = DEFAULT_DELETEMANIFEST;
		[Category("General"), DisplayName("Delete Manifest After Use"), Description("Delete the bin\\manifest.xml file created for the compiler after generating it (set to false if you want to see which classes were passed to the compiler)."), DefaultValue(DEFAULT_DELETEMANIFEST)]
		public bool DeleteManifest {
			get { return _deleteManifest; }
			set { _deleteManifest = value; }
		}

		const string DEFAULT_NAMESPACE = "*";
		private string _namespace = DEFAULT_NAMESPACE;
		[Category("Compiler"), Description("The namespace to use (a namespace must be used, * is a generic default)."), DefaultValue(DEFAULT_NAMESPACE)]
		public string Namespace {
			get { return _namespace; }
			set { _namespace = value; }
		}*/
		#endregion
	}

}
