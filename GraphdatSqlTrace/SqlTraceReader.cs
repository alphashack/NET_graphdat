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
        private readonly string _workDirectory;

        public static event EventHandler<SqlTraceService.StoppingEventArgs> Stopping;

        private void InvokeStopping(SqlTraceService.StoppingEventArgs e)
        {
            var handler = Stopping;
            if (handler != null) handler(this, e);
        }

        public static void Start(EventLog eventLog, string workDirectory)
        {
            if (_instance == null) _instance = new SqlTraceReader(eventLog, workDirectory);
        }

        public static void Stop()
        {
            if (_instance != null) _instance.Term();
        }

        private void Exit(string message)
        {
            _eventLog.WriteEntry(message, EventLogEntryType.Error);
            _thread = null;
            _instance = null;
            InvokeStopping(new SqlTraceService.StoppingEventArgs { Reason = message });
        }

        private static void LogEvent(string message, EventLogEntryType logType)
        {
            if (_instance != null) _instance._eventLog.WriteEntry(message, logType);
        }

        private SqlTraceReader(EventLog eventLog, string workDirectory)
        {
            try
            {
                _eventLog = eventLog;
                _workDirectory = workDirectory;

                _agentConnect = new Connect();
                _agentConnect.Init(Properties.Settings.Default.Port.ToString(), Properties.Settings.Default.Source,
                                   Logger);

                _thread = new Thread(Worker) {IsBackground = true};
                _termHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
                _thread.Start();
            }
            catch (Exception ex)
            {
                Exit(string.Format("SqlTraceReader failed to start due to exception. {0}", ex));
            }
        }

        private void Term()
        {
            _agentConnect.Term(Logger);

            if (_termHandle != null) _termHandle.Set();
            if (_thread != null) _thread.Join();
            _thread = null;

            _instance = null;
        }

        private void Worker()
        {
            try
            {
                DeleteOldTraces(_workDirectory);

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
                Exit(string.Format("SqlTraceReader worker exiting because of exception. {0}", ex));
            }
        }

        private void DeleteOldTraces(string dir)
        {
            foreach (var trace in Directory.EnumerateFiles(dir, "graphdat_*.trc"))
            {
                try
                {
                    File.Delete(trace);
                }
                catch (Exception ex)
                {
                    _eventLog.WriteEntry(string.Format("Failed to delete old trace file ({0}). {1}", trace, ex.Message), EventLogEntryType.Warning);
                }
            }
        }

        private void FindInstanceTraces(Dictionary<string, TraceInfo> instances)
        {
            var files = Directory.GetFiles(_workDirectory, @"graphdat*.trc");
            var regex = new Regex(string.Format(@"^{0}\\graphdat_((?<instanceName>.*)_(?<fileNumber>\d+)\.trc|(?<instanceName>.*)\.trc)$", Regex.Escape(_workDirectory)));
            DebugHelper.LogEntry(_eventLog, string.Format("{0} instance traces found", files.Length));
            foreach (var file in files)
            {
                var match = regex.Match(file);
                if (!match.Success || !match.Groups["instanceName"].Success)
                {
                    DebugHelper.LogEntry(_eventLog, string.Format("File '{0}' regex failed: match {1}, group {2}", file, match.Success, match.Groups["instanceName"].Success));
                    continue;
                }
                var instanceName = match.Groups["instanceName"].Value;
                var fileNumber = match.Groups["fileNumber"].Success
                                     ? int.Parse(match.Groups["fileNumber"].Value)
                                     : 0;

                if (!instances.ContainsKey(instanceName))
                {
                    DebugHelper.LogEntry(_eventLog, string.Format("First instance trace: {0} ({1})", instanceName, fileNumber));
                    instances[instanceName] = new TraceInfo { MaxFileNumber = fileNumber };
                }
                else if (instances[instanceName].MaxFileNumber < fileNumber)
                {
                    DebugHelper.LogEntry(_eventLog, string.Format("New instance trace: {0} ({1})", instanceName, fileNumber));
                    instances[instanceName].TraceRead = false;
                    instances[instanceName].MaxFileNumber = fileNumber;
                }
                else
                {
                    DebugHelper.LogEntry(_eventLog, string.Format("Old instance trace: {0} ({1}, last: {2})", instanceName, fileNumber, instances[instanceName].MaxFileNumber));                    
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

                DebugHelper.LogEntry(_eventLog, string.Format("Read instance trace: {0}", instance.Key));

                var filename = string.Format(string.Format("{0}\\graphdat_{1}{2}{3}.trc",
                    _workDirectory,
                    instance.Key,
                    fileNumberToRead > 0 ? "_" : "",
                    fileNumberToRead > 0 ? fileNumberToRead.ToString() : ""));

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
                {
                    _agentConnect.Store(new Sample
                                            {
                                                //Method = databaseName,
                                                Uri = name,
                                                ResponseTime = duration,
                                                Timestamp = startTime.Ticks
                                            }, Logger);
                    DebugHelper.LogEntry(_eventLog, string.Format("Data sent: '{0}'", textData), EventLogEntryType.SuccessAudit);
                }
                else
                {
                    DebugHelper.LogEntry(_eventLog, string.Format("Could not simplify '{0}': {1}", textData, Outliner.LastError), EventLogEntryType.Warning);
                }
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
            LogEvent(string.Format(fmt, args), logType);
        }
    }

    internal class TraceInfo
    {
        public int MaxFileNumber;
        public bool TraceRead;
    }

}
