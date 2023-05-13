namespace ExportSWC.Tracing.Interfaces
{
    public interface ITraceable
    {
        void WriteLine(string msg);
        void WriteLine(string msg, TraceMessageType messageType);
    }
}
