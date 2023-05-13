using ExportSWC.Tracing;
using ExportSWC.Tracing.Interfaces;

namespace ExportSWC
{
    public partial class SWCBuilder
    {
        private bool _anyErrors;
        private bool _running;
        private readonly ITraceable _tracer;

        public SWCBuilder(ITraceable tracer)
        {
            _tracer = tracer;
        }

        //private SWCProject _swcProjectSettings = null;
        //private AS3Project _project = null;
        //private ITraceable _tracer = null;

        //private DirectoryInfo ProjectPath => new DirectoryInfo(_project.Directory);

        //private string CompcBinPath_Flex => Path.Combine(ProjectPath.FullName, _swcProjectSettings.FlexBinPath);

        private void ProcessOutput(object sender, string line)
        {
            //TraceManager.AddAsync(line);
        }

        private void ProcessError(object sender, string line)
        {
            _anyErrors = true;
            //TraceManager.AddAsync(line, 3);
            WriteLine(line, TraceMessageType.Error);
        }
    }
}
