using System;
using System.Drawing;
using System.Windows.Forms;
using ExportSWC.Options;
using ExportSWC.Resources;
using PluginCore;
using ProjectManager.Projects.AS3;

namespace ExportSWC
{
    public partial class PluginMain
    {
        private Image? _enabledIcon;
        private Image? _disabledIcon;

        /// <summary>
        /// Creates a menu item for the plugin and adds a ignored key
        /// </summary>
        private void CreateMenuItem()
        {
            _enabledIcon ??= LocaleHelper.GetImage("icon");
            _disabledIcon ??= LocaleHelper.GetImage("icon_disabled");

            //ToolStripMenuItem viewMenu = (ToolStripMenuItem)PluginBase.MainForm.FindMenuItem("ViewMenu");
            //viewMenu.DropDownItems.Add(new ToolStripMenuItem(LocaleHelper.GetString("Label.ViewMenuItem"), this.pluginImage, new EventHandler(Configure)));

            var mainForm = PluginBase.MainForm;
            // TODO localise button labels
            // toolbar items
            var toolStrip = mainForm.ToolStrip;
            if (toolStrip != null)
            {
                toolStrip.Items.Add(new ToolStripSeparator());

                /* main button */
                _button = new ToolStripSplitButton(_disabledIcon)
                {
                    ToolTipText = LocaleHelper.GetString("ExportButton_Label")
                };
                _button.ButtonClick += (s, e) =>
                {
                    if (CurrentSwcProject is null)
                    {
                        Configure(s, e);
                        return;
                    }

                    Build(s, e);
                };
                toolStrip.Items.Add(_button);

                /* add menu items */
                /* build */
                _button_build_def = new ToolStripMenuItem
                {
                    Text = LocaleHelper.GetString("ExportButton_Label"),
                    ToolTipText = LocaleHelper.GetString("ExportButton_ToolTip"),
                    Enabled = CurrentSwcProject is not null
                };
                _button_build_def.Font = new Font(_button_build_def.Font, FontStyle.Bold);
                _button_build_def.Click += new EventHandler(Build);
                _button.DropDown.Items.Add(_button_build_def);
                /* meta */
                _button_partial = new ToolStripMenuItem
                {
                    Text = LocaleHelper.GetString("PrepareBuildConfigFiles_Label"),
                    ToolTipText = LocaleHelper.GetString("PrepareBuildConfigFiles_ToolTip"),
                    Enabled = CurrentSwcProject is not null
                };
                _button_partial.Click += new EventHandler(PreBuildClick);
                _button.DropDown.Items.Add(_button_partial);
                /* compile */
                _button_compile = new ToolStripMenuItem
                {
                    Text = LocaleHelper.GetString("OnlyCompileButton_Label"),
                    ToolTipText = LocaleHelper.GetString("OnlyCompileButton_ToolTip"),
                    Enabled = CurrentSwcProject is not null
                };
                _button_compile.Click += new EventHandler(CompileClick);
                _button.DropDown.Items.Add(_button_compile);
                /* splitter */
                _button_seperator = new ToolStripSeparator();
                _button.DropDown.Items.Add(_button_seperator);
                /* configure */
                _button_config = new ToolStripMenuItem
                {
                    Text = LocaleHelper.GetString("ConfigureProjectSettingsButton_Label"),
                    ToolTipText = LocaleHelper.GetString("ConfigureProjectSettingsButton_ToolTip")
                };
                _button_config.Click += new EventHandler(Configure);
                _button.DropDown.Items.Add(_button_config);

                /* override default build command */
                _button_override_default_build_command = new ToolStripMenuItem
                {
                    Text = LocaleHelper.GetString("OverrideDefaultBuildCommandButton_Label"),
                    Checked = _settingsObject.OverrideBuildCommand,
                    ToolTipText = LocaleHelper.GetString("OverrideDefaultBuildCommandButton_ToolTip")
                };
                _button_override_default_build_command.Click += (s, e) =>
                {
                    _settingsObject.OverrideBuildCommand = !_settingsObject.OverrideBuildCommand;
                    _button_override_default_build_command.Checked = _settingsObject.OverrideBuildCommand;
                };
                _button.DropDown.Items.Add(_button_override_default_build_command);

                UpdateToolstrip();
            }
        }

        private void UpdateToolstrip()
        {
            _button.Enabled = !_compiler.IsBuilding &&
                CurrentProject is not null;
            _button.ToolTipText = CurrentSwcProject is not null ? 
                LocaleHelper.GetString("ExportButton_Label") :
                LocaleHelper.GetString("ExportButton_Label:NoProject");
            _button.Image = _button.Enabled && CurrentSwcProject is not null ? _enabledIcon : _disabledIcon;

            _button_build_def.Enabled =
                _button_partial.Enabled =
                _button_compile.Enabled =
                !_compiler.IsBuilding && CurrentSwcProject is not null;

            _button_override_default_build_command.Enabled = _settingsObject.OverrideBuildCommand;
        }

        private void Configure(object sender, EventArgs e)
        {
            EnsureNotNull(CurrentProject);

            ConfigureSwcProject(CurrentProject, CurrentSwcProject);
        }

        private void ConfigureSwcProject(AS3Project project, SWCProject? swcProject)
        {
            swcProject ??= new SWCProject();

            var dr = ProjectOptions.ShowDialog(swcProject);

            if (dr == DialogResult.OK)
            {
                swcProject.Save(GetSwcProjectSettingsPath(project));
                CurrentSwcProject = swcProject;
            }
        }
    }
}
