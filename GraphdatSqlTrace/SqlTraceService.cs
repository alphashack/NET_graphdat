﻿using System.ServiceProcess;

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
            SqlTraceManager.Start(EventLog);
            SqlTraceReader.Start(EventLog);
        }

        protected override void OnStop()
        {
            SqlTraceManager.Stop();
            SqlTraceReader.Stop();
        }
    }
}