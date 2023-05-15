using System;

namespace ExportSWC
{
    public partial class PluginMain
    {
        /// <summary>
        /// Main method for plugin - Export SWC using compc.exe
        /// </summary>
        /// <param name="sender">the sender</param>
        /// <param name="e">the event args</param>
        private void Build(object sender, EventArgs e)
        {
            _button.Enabled = false;

            try
            {
                EnsureNotNull(CurrentProject);
                EnsureNotNull(CurrentSwcProject);

                _compiler.Build(CurrentProject, CurrentSwcProject);
            }
            finally
            {
                _button.Enabled = true;
            }
        }

        private void PreBuildClick(object sender, EventArgs e)
        {
            _button.Enabled = false;

            try
            {

                EnsureNotNull(CurrentProject);
                EnsureNotNull(CurrentSwcProject);

                _compiler.PreBuild(CurrentProject, CurrentSwcProject);
            }
            finally
            {
                _button.Enabled = true;
            }
        }

        private void CompileClick(object sender, EventArgs e)
        {
            _button.Enabled = false;

            try
            {
                EnsureNotNull(CurrentProject);
                EnsureNotNull(CurrentSwcProject);

                _compiler.Compile(CurrentProject, CurrentSwcProject);
            }
            finally
            {
                _button.Enabled = true;
            }
        }
    }
}
