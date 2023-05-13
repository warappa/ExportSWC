namespace ExportSWC.Tracing.Interfaces
{
    internal interface ITraceable
    {
        void WriteLine(string msg);
        void WriteLine(string msg, TraceMessageType messageType);
    }
}
