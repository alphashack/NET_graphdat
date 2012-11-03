using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.SqlServer.Management.Trace;
using SqlQueryHelper;

namespace Alphashack.Graphdat.Agent.SqlTrace
{
    public class SqlTraceReader
    {
        private static SqlTraceReader _instance;

        private readonly EventLog _eventLog;
        private readonly Connect _agentConnect;
        
        private Thread _thread;
        private readonly EventWaitHandle _termHandle;

        public static event EventHandler Stopping;

        private void InvokeStopping(EventArgs e)
        {
            var handler = Stopping;
            if (handler != null) handler(this, e);
        }

        public static void Start(EventLog eventLog)
        {
            if (_instance == null) _instance = new SqlTraceReader(eventLog);
        }

        public static void Stop()
        {
            if (_instance != null) _instance.Term();
        }

        private SqlTraceReader(EventLog eventLog)
        {
            try
            {
                _eventLog = eventLog;

                _agentConnect = new Connect();
                _agentConnect.Init(Properties.Settings.Default.Port.ToString(), Properties.Settings.Default.Source,
                                   Logger);

                _thread = new Thread(Worker) {IsBackground = true};
                _termHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
                _thread.Start();
            }
            catch (Exception ex)
            {
                _eventLog.WriteEntry(string.Format("SqlTraceReader failed to start due to exception. {0}", ex));
                InvokeStopping(null);
            }
        }

        private void Term()
        {
            _agentConnect.Term(Logger);

            if (_termHandle != null) _termHandle.Set();
            if (_thread != null) _thread.Join();
            _thread = null;
        }

        private void Worker()
        {
            try
            {
                var instances = new Dictionary<string, TraceInfo>();
                int timeToWait;

                do
                {
                    var processingStart = Stopwatch.StartNew();

                    FindInstanceTraces(instances);

                    ReadInstanceTraces(instances);

                    // calculate time to wait
                    var elapsed = (int) processingStart.ElapsedMilliseconds;
                    timeToWait = Properties.Settings.Default.SqlTraceReaderWorkerLoopSleep - elapsed;
                    if (timeToWait < 0) timeToWait = 0;
                } while (!_termHandle.WaitOne(timeToWait));
            }
            catch (Exception ex)
            {
                _eventLog.WriteEntry(string.Format("SqlTraceReader worker exiting because of exception. {0}", ex));
                _thread = null;
                InvokeStopping(null);
            }
        }

        private static void FindInstanceTraces(Dictionary<string, TraceInfo> instances)
        {
            var files = Directory.GetFiles(@"c:\tmp", @"graphdat*.trc");
            var regex = new Regex(@"^c:\\tmp\\graphdat_((?<instanceName>.*)_(?<fileNumber>\d+)\.trc|(?<instanceName>.*)\.trc)$");
            foreach (var file in files)
            {
                var match = regex.Match(file);
                if (!match.Success || !match.Groups["instanceName"].Success) continue;
                var instanceName = match.Groups["instanceName"].Value;
                var fileNumber = match.Groups["fileNumber"].Success
                                     ? int.Parse(match.Groups["fileNumber"].Value)
                                     : 0;

                if (!instances.ContainsKey(instanceName))
                {
                    instances[instanceName] = new TraceInfo { MaxFileNumber = fileNumber };
                }
                else if (instances[instanceName].MaxFileNumber < fileNumber)
                {
                    instances[instanceName].TraceRead = false;
                    instances[instanceName].MaxFileNumber = fileNumber;
                }
            }
        }

        private void ReadInstanceTraces(Dictionary<string, TraceInfo> instances)
        {
            foreach (var instance in instances)
            {
                // has the trace been read already
                if (instance.Value.TraceRead) continue;

                // read the second last written trace, the last one is still in use by sql server
                var fileNumberToRead = instance.Value.MaxFileNumber - 1;
                if (fileNumberToRead < 0) continue;

                var filename = string.Format("c:\\tmp\\graphdat_{0}{1}{2}.trc", instance.Key,
                                             fileNumberToRead > 0 ? "_" : "",
                                             fileNumberToRead > 0 ? fileNumberToRead.ToString() : "");

                // does the file still exist
                if (!File.Exists(filename)) continue;

                // read the trace
                ReadTraceFile(filename);
                instance.Value.TraceRead = true;
            }
        }

        private void ReadTraceFile(string filename)
        {
            var traceFile = new TraceFile();
            traceFile.InitializeAsReader(filename);

            var eventClassOrdinal = traceFile.GetOrdinal("EventClass");
            var textDataOrdinal = traceFile.GetOrdinal("TextData");
            //var databaseNameOrdinal = traceFile.GetOrdinal("DatabaseName");
            var startTimeOrdinal = traceFile.GetOrdinal("StartTime");
            var durationOrdinal = traceFile.GetOrdinal("Duration");

            while (traceFile.Read())
            {
                var eventClass = traceFile.GetString(eventClassOrdinal);

                // skip on start, end on stop
                if (eventClass.Equals("trace start", StringComparison.InvariantCultureIgnoreCase)) continue;
                if (eventClass.Equals("trace stop", StringComparison.InvariantCultureIgnoreCase)) break;

                var textData = traceFile.GetString(textDataOrdinal);
                //var databaseName = traceFile.GetString(databaseNameOrdinal);
                var startTime = traceFile.GetDateTime(startTimeOrdinal);
                var duration = traceFile.GetInt64(durationOrdinal) / 1000; // duration is in microseconds

                // if the query simplified, send it to the agent
                string name;
                if (Outliner.TrySimplify(textData, out name))
                    _agentConnect.Store(new Sample
                    {
                        //Method = databaseName,
                        Uri = name,
                        ResponseTime = duration,
                        Timestamp = startTime.Ticks
                    }, Logger);
            }
            traceFile.Close();
        }

        private static void Logger(GraphdatLogType type, object user, string fmt, params object[] args)
        {
            EventLogEntryType logType;
            switch (type)
            {
                case GraphdatLogType.SuccessMessage:
                    logType = EventLogEntryType.SuccessAudit;
                    break;
                case GraphdatLogType.ErrorMessage:
                    logType = EventLogEntryType.Error;
                    break;
                case GraphdatLogType.WarningMessage:
                    logType = EventLogEntryType.Warning;
                    break;
                case GraphdatLogType.InformationMessage:
                    logType = EventLogEntryType.Information;
                    break;
                default:
                    logType = EventLogEntryType.Error;
                    break;
            }
            _instance._eventLog.WriteEntry(string.Format(fmt, args), logType);
        }
    }

    internal class TraceInfo
    {
        public int MaxFileNumber;
        public bool TraceRead;
    }

}
