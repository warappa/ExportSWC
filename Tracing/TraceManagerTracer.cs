using System;
using System.Collections.Generic;
using System.Text;
using ExportSWC.Tracing.Interfaces;
using PluginCore.Managers;

namespace ExportSWC.Tracing
{
	public class TraceManagerTracer:ITraceable
	{
		#region ITraceable Member		

		public void WriteLine(string msg)
		{
			TraceManager.AddAsync(msg);
		}

		public void WriteLine(string msg, TraceMessageType messageType)
		{
			TraceManager.AddAsync(msg, (int)messageType);
		}

		#endregion
	}
}
