using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using ProjectManager.Projects.AS3;

namespace ExportSWC
{
    public partial class ProjectOptions : Form
    {
		private SWCBuilder builder;
        private SWCProject project;
        private AutoCompleteStringCollection classCache;

        public ProjectOptions()
        {
            InitializeComponent();
        }

        public ProjectOptions(SWCProject swcp, SWCBuilder builder)
        {
            InitializeComponent();

            project = swcp;
			this.builder = builder;

            cb_intrinsic_flex.Checked   = swcp.FlexIncludeASI;

            cb_makecs3.Checked          = swcp.MakeCS3;
            cb_intrinsic_cs3.Checked    = swcp.MXPIncludeASI;
            cb_createmxi.Checked        = swcp.MakeMXI;
            cb_runaem.Checked           = swcp.LaunchAEM;
            tb_compname.Text            = swcp.CS3_ComponentName;
            tb_compgroup.Text           = swcp.CS3_ComponentGroup;
            tb_tooltip.Text             = swcp.CS3_ComponentToolTip;
            tb_icon.Text                = swcp.CS3_ComponentIconFile;
            tb_preview.Text             = swcp.CS3_PreviewResource;
            tb_compclass.Text           = swcp.CS3_ComponentClass;
            tb_uiaccess.Text            = swcp.MXIUIAccessText;
            tb_desc.Text                = swcp.MXIDescription;
            tb_comauthor.Text           = swcp.MXIAuthor;
            tb_comver.Text              = swcp.MXIVersion;

            textBoxFlexBin.Text         = project.FlexBinPath;
            textBoxFlashBin.Text        = project.FlashBinPath;

			checkBoxAsDoc.Checked		= swcp.IntegrateAsDoc;

            CheckFlexDir();
            CheckFlashDir();

            switch (project.CS3_PreviewType)
            {
                case SWCProject.CS3_PreviewType_ENUM.None:
                    rb_none.Checked = true;
                    break;
                case SWCProject.CS3_PreviewType_ENUM.ExternalSWF:
                    rb_swf.Checked = true;
                    break;
                case SWCProject.CS3_PreviewType_ENUM.Class:
                    rb_class.Checked = true;
                    break;
                default:
                    break;
            }

            classCache = new AutoCompleteStringCollection();
            AS3Project proj = (AS3Project)PluginCore.PluginBase.CurrentProject;
            string fclass;
            foreach (string path in proj.AbsoluteClasspaths)
            {
                string[] files = Directory.GetFiles(path, "*.as", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    fclass = file.Substring(path.Length).TrimStart('\\').Replace('\\', '.');
                    fclass = fclass.Substring(0, fclass.Length - 3);
                    classCache.Add(fclass);
                }
            }
            tb_preview.AutoCompleteSource = AutoCompleteSource.CustomSource;
            tb_preview.AutoCompleteCustomSource = classCache;
            tb_compclass.AutoCompleteSource = AutoCompleteSource.CustomSource;
            tb_compclass.AutoCompleteCustomSource = classCache;
        }

        internal static DialogResult ShowDialog(SWCProject swcProject, SWCBuilder builder)
        {
			ProjectOptions po = new ProjectOptions(swcProject, builder);
            return po.ShowDialog();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            project.CS3_ComponentClass      = tb_compclass.Text;
            project.CS3_ComponentGroup      = tb_compgroup.Text;
            project.CS3_ComponentIconFile   = tb_icon.Text;
            project.CS3_ComponentName       = tb_compname.Text;
            project.CS3_ComponentToolTip    = tb_tooltip.Text;
            project.CS3_PreviewResource     = tb_preview.Text;
            project.CS3_PreviewType =
                rb_none.Checked ? SWCProject.CS3_PreviewType_ENUM.None :
                rb_class.Checked ? SWCProject.CS3_PreviewType_ENUM.Class :
                SWCProject.CS3_PreviewType_ENUM.ExternalSWF;
            project.FlexIncludeASI  = cb_intrinsic_flex.Checked;
            project.LaunchAEM       = cb_runaem.Checked;
            project.MakeCS3         = cb_makecs3.Checked;
            project.MakeMXI         = cb_createmxi.Checked;
            project.MXPIncludeASI   = cb_intrinsic_cs3.Checked;
            project.MXIAuthor       = tb_comauthor.Text;
            project.MXIDescription  = tb_desc.Text;
            project.MXIUIAccessText = tb_uiaccess.Text;
            project.MXIVersion      = tb_comver.Text;

			project.IntegrateAsDoc = checkBoxAsDoc.Checked;

            project.FlexBinPath = textBoxFlexBin.Text;
            project.FlashBinPath = textBoxFlashBin.Text;
        }

        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            tb_icon.Text = openFileDialogIcon.FileName;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                openFileDialogIcon.InitialDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(PluginCore.PluginBase.CurrentProject.ProjectPath), Path.GetDirectoryName(textBoxFlashBin.Text)));
            }
            catch { }
            openFileDialogIcon.ShowDialog();
        }

        private void openFileDialog2_FileOk(object sender, CancelEventArgs e)
        {
            tb_preview.Text = openFileDialogSWF.FileName;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (rb_swf.Checked)
            {
                try
                {
                    openFileDialogIcon.InitialDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(PluginCore.PluginBase.CurrentProject.ProjectPath), Path.GetDirectoryName(textBoxFlashBin.Text)));
                }
                catch { }
                openFileDialogSWF.ShowDialog();
            }
        }

        private void EnableCtrls()
        {
            cb_intrinsic_cs3.Enabled
                = cb_createmxi.Enabled
                = tb_compname.Enabled
                = tb_compclass.Enabled
                = tb_compgroup.Enabled
                = tb_icon.Enabled
                = tb_preview.Enabled
                = tb_tooltip.Enabled 
                = rb_class.Enabled
                = rb_none.Enabled 
                = rb_swf.Enabled
                = button1.Enabled
                = textBoxFlashBin.Enabled
                = buttonBrowseFlashOutput.Enabled
                = cb_makecs3.Checked;
            cb_runaem.Enabled = cb_createmxi.Checked && cb_createmxi.Enabled;

            tb_preview.Enabled 
                = !rb_none.Checked && cb_makecs3.Checked;
            button2.Enabled = rb_swf.Checked && cb_makecs3.Checked;

            tb_compgroup.Enabled 
                = tb_desc.Enabled 
                = tb_uiaccess.Enabled
                = tb_comver.Enabled
                = tb_comauthor.Enabled
                = cb_createmxi.Checked && cb_makecs3.Checked;

            tb_preview.AutoCompleteMode = rb_class.Checked ?
                AutoCompleteMode.SuggestAppend : AutoCompleteMode.None;
        }

        private void uiSettingChanged(object sender, EventArgs e)
        {
            EnableCtrls();
        }

        private void tb_icon_TextChanged(object sender, EventArgs e)
        {
            tb_icon.BackColor = Color.FromKnownColor(KnownColor.Window);
            if (!File.Exists(tb_icon.Text))
            {
                toolStripStatusLabel1.Text = "";
                pictureBox1.Visible = false;
                return;
            }
            if (Path.GetExtension(tb_icon.Text).ToLower() != ".png")
            {
                toolStripStatusLabel1.Text = "Unsupported Icon";
                pictureBox1.Visible = false;
                return;
            }
            pictureBox1.Load(tb_icon.Text);
            if (pictureBox1.Image.Size.Height == 18
                && pictureBox1.Image.Size.Width == 18)
            {
                pictureBox1.Visible = true;
                toolStripStatusLabel1.Text = "";
            }
            else
            {
                toolStripStatusLabel1.Text = "Icon must be 18x18...";
                pictureBox1.Visible = false;
                tb_icon.BackColor = Color.Goldenrod;
            }
        }

        private void tb_preview_TextChanged(object sender, EventArgs e)
        {
            tb_preview.BackColor = Color.FromKnownColor(KnownColor.Window);
            if (rb_class.Checked)
            {
                
            }
            else if (rb_swf.Checked)
            {
                if (!File.Exists(tb_preview.Text))
                    return;
                if (Path.GetExtension(tb_preview.Text).ToLower() != ".swf")
                {
                    toolStripStatusLabel1.Text = "Unsupported Preview";
                    tb_preview.BackColor = Color.Goldenrod;
                }
                else
                    toolStripStatusLabel1.Text = "";
            }
        }

        private void tb_SelectAll(object sender, EventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private void ProjectOptions_Load(object sender, EventArgs e)
        {

        }

        private void textBoxFlexBin_Leave(object sender, EventArgs e)
        {
            CheckFlexDir();
        }
        private void CheckFlexDir()
        {
            if (textBoxFlexBin.Text == "")
                textBoxFlexBin.Text = ".\\bin\\" + PluginCore.PluginBase.CurrentProject.Name + "";

            if (textBoxFlexBin.Text.ToLower().EndsWith(".swc") == false)
                textBoxFlexBin.Text += ".swc";
        }

        private void textBoxFlashBin_Leave(object sender, EventArgs e)
        {
            CheckFlashDir();
        }
        private void CheckFlashDir()
        {
            if (textBoxFlashBin.Text == "")
                textBoxFlashBin.Text = ".\\bin\\" + PluginCore.PluginBase.CurrentProject.Name + ".flash";

            if (textBoxFlashBin.Text.ToLower().EndsWith(".swc") == false)
                textBoxFlashBin.Text += ".swc";
        }

        private void buttonBrowseFlexOutput_Click(object sender, EventArgs e)
        {
            try
            {
                saveFileDialogFlex.InitialDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(PluginCore.PluginBase.CurrentProject.ProjectPath), Path.GetDirectoryName(textBoxFlexBin.Text)));                
            }
            catch{ }
            

            if (saveFileDialogFlex.ShowDialog(this) == DialogResult.OK)
            {
                textBoxFlexBin.Text = saveFileDialogFlex.FileName;
            }
        }

        private void buttonBrowseFlashOutput_Click(object sender, EventArgs e)
        {
            try
            {
                saveFileDialogFlash.InitialDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(PluginCore.PluginBase.CurrentProject.ProjectPath), Path.GetDirectoryName(textBoxFlashBin.Text)));
            }
            catch { }
            if (saveFileDialogFlash.ShowDialog(this) == DialogResult.OK)
            {
                textBoxFlashBin.Text = saveFileDialogFlash.FileName;
            }
        }
    }
}
