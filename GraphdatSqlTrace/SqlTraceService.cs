using System;
using System.IO;
using System.Reflection;
using System.ServiceProcess;

namespace Alphashack.Graphdat.Agent.SqlTrace
{
    public class SqlTraceService : ServiceBase
    {
        public SqlTraceService()
        {
            ServiceName = Constants.ServiceName;
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            SqlTraceManager.Stopping += WorkerStopping;
            SqlTraceReader.Stopping += WorkerStopping;

            var workDir = GetWorkDir();

            SqlTraceManager.Start(EventLog, workDir);
            SqlTraceReader.Start(EventLog, workDir);
        }

        protected override void OnStop()
        {
            SqlTraceManager.Stop();
            SqlTraceReader.Stop();
        }

        void WorkerStopping(object sender, StoppingEventArgs e)
        {
            ExitCode = 1066;
            Stop();
        }

        public class StoppingEventArgs : EventArgs
        {
            public string Reason;
        }

        private static string GetWorkDir()
        {
            var exe = new FileInfo(Assembly.GetExecutingAssembly().Location);
            return exe.DirectoryName;
        }

    }
}
