using System;
using System.Drawing;
using System.Windows.Forms;
using ExportSWC.Options;
using ExportSWC.Resources;
using PluginCore;
using ProjectManager.Projects.AS3;

namespace ExportSWC
{
    internal partial class PluginMain
    {
        /// <summary>
        /// Creates a menu item for the plugin and adds a ignored key
        /// </summary>
        public void CreateMenuItem()
        {
            //ToolStripMenuItem viewMenu = (ToolStripMenuItem)PluginBase.MainForm.FindMenuItem("ViewMenu");
            //viewMenu.DropDownItems.Add(new ToolStripMenuItem(LocaleHelper.GetString("Label.ViewMenuItem"), this.pluginImage, new EventHandler(Configure)));

            var mainForm = PluginBase.MainForm;
            // TODO localise button labels
            // toolbar items
            var toolStrip = mainForm.ToolStrip;
            if (toolStrip != null)
            {
                toolStrip.Items.Add(new ToolStripSeparator());
                //_button = new ToolStripButton(LocaleHelper.GetImage("icon"));
                _button = new ToolStripSplitButton(LocaleHelper.GetImage("icon"))
                {
                    ToolTipText = LocaleHelper.GetString("Label.PluginButton"),
                    Enabled = false
                };
                _button.ButtonClick += new EventHandler(Build);
                /* add main button */
                toolStrip.Items.Add(_button);
                /* add menu items */
                /* build */
                _button_build_def = new ToolStripMenuItem
                {
                    Text = "Build All",
                    ToolTipText = LocaleHelper.GetString("Label.PluginButton")
                };
                _button_build_def.Font = new Font(_button_build_def.Font, FontStyle.Bold);
                _button_build_def.Click += new EventHandler(Build);
                _button.DropDown.Items.Add(_button_build_def);
                /* meta */
                _button_partial = new ToolStripMenuItem
                {
                    Text = "Prebuild Meta",
                    ToolTipText = LocaleHelper.GetString("Label.PartialBuildButton")
                };
                _button_partial.Click += new EventHandler(PreBuildClick);
                _button.DropDown.Items.Add(_button_partial);
                /* compile */
                _button_compile = new ToolStripMenuItem
                {
                    Text = "Compile Targets",
                    ToolTipText = LocaleHelper.GetString("Label.CompileButton")
                };
                _button_compile.Click += new EventHandler(CompileClick);
                _button.DropDown.Items.Add(_button_compile);
                /* splitter */
                _button_seperator = new ToolStripSeparator();
                _button.DropDown.Items.Add(_button_seperator);
                /* configure */
                _button_config = new ToolStripMenuItem
                {
                    Text = "Configure",
                    ToolTipText = LocaleHelper.GetString("Label.Configure")
                };
                _button_config.Click += new EventHandler(Configure);
                _button.DropDown.Items.Add(_button_config);
            }
        }

        private void Configure(object sender, EventArgs e)
        {
            ConfigureSwcProject(CurrentProject, CurrentSwcProject);
        }

        private void ConfigureSwcProject(AS3Project project, SWCProject swcProject)
        {
            var dr = ProjectOptions.ShowDialog(swcProject, _compiler);

            if (dr == DialogResult.OK)
            {
                swcProject.Save(GetSwcProjectSettingsPath(project));
            }
        }
    }
}
