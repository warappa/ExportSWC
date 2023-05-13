using ExportSWC.Tracing.Interfaces;
using PluginCore.Managers;

namespace ExportSWC.Tracing
{
    public class TraceManagerTracer:ITraceable
	{
		public void WriteLine(string msg)
		{
			TraceManager.AddAsync(msg);
		}

		public void WriteLine(string msg, TraceMessageType messageType)
		{
			TraceManager.AddAsync(msg, (int)messageType);
		}
	}
}
