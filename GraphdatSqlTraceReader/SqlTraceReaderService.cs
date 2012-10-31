using System.ServiceProcess;

namespace Alphashack.Graphdat.Agent.SqlTrace
{
    public class SqlTraceReaderService : ServiceBase
    {
        public SqlTraceReaderService()
        {
            ServiceName = Constants.ReaderServiceName;
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            SqlTraceReader.Start(EventLog);
        }

        protected override void OnStop()
        {
            SqlTraceReader.Stop();
        }
    }
}
