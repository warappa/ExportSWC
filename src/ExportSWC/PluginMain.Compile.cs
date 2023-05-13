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
        protected void Build(object sender, EventArgs e)
        {
            _button.Enabled = false;

            try
            {
                _compiler.Build(CurrentProject, CurrentSwcProject, _tracer);
            }
            finally
            {
                _button.Enabled = true;
            }
        }

        protected void PreBuildClick(object sender, EventArgs e)
        {
            _compiler.PreBuild(CurrentProject, CurrentSwcProject, _tracer);
        }

        protected void CompileClick(object sender, EventArgs e)
        {
            _compiler.Compile(CurrentProject, CurrentSwcProject, _tracer);
        }
    }
}
