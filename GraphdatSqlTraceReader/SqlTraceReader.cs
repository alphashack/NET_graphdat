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
            _eventLog = eventLog;

            _agentConnect = new Connect();
            _agentConnect.Init(Properties.Settings.Default.Port.ToString(), Properties.Settings.Default.Source, Logger);

            _thread = new Thread(Worker) { IsBackground = true };
            _termHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _thread.Start();
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
            var instances = new Dictionary<string, TraceInfo>();

            do
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

                foreach (var instance in instances)
                {
                    if (instance.Value.TraceRead) continue;

                    var fileNumberToRead = instance.Value.MaxFileNumber - 1;
                    if (fileNumberToRead < 0) continue;

                    var filename = string.Format("c:\\tmp\\graphdat_{0}{1}{2}.trc", instance.Key,
                                                 fileNumberToRead > 0 ? "_" : "",
                                                 fileNumberToRead > 0 ? fileNumberToRead.ToString() : "");

                    var traceFile = new TraceFile();
                    traceFile.InitializeAsReader(filename);

                    var eventClassOrdinal = traceFile.GetOrdinal("EventClass");
                    var eventSequenceOrdinal = traceFile.GetOrdinal("EventSequence");
                    var textDataOrdinal = traceFile.GetOrdinal("TextData");
                    var startTimeOrdinal = traceFile.GetOrdinal("StartTime");
                    var durationOrdinal = traceFile.GetOrdinal("Duration");

                    while (traceFile.Read())
                    {
                        var eventClass = traceFile.GetString(eventClassOrdinal);

                        if (eventClass.Equals("trace start", StringComparison.InvariantCultureIgnoreCase)) continue;
                        if (eventClass.Equals("trace stop", StringComparison.InvariantCultureIgnoreCase))
                        {
                            instance.Value.TraceRead = true;
                            break;
                        }

                        var eventSequence = traceFile.GetInt64(eventSequenceOrdinal);
                        if (instance.Value.MaxEventSequence >= eventSequence) continue;

                        var textData = traceFile.GetString(textDataOrdinal);
                        var startTime = traceFile.GetDateTime(startTimeOrdinal);
                        var duration = traceFile.GetInt64(durationOrdinal) / 1000;

                        string name;
                        if(Outliner.ParseSql(textData, out name))
                            _agentConnect.Store(new Sample
                                                    {
                                                        Uri = name,
                                                        ResponseTime = duration,
                                                        Timestamp = startTime.Ticks
                                                    }, Logger);
                    }
                    traceFile.Close();
                }
            } while (!_termHandle.WaitOne(Properties.Settings.Default.SqlTraceReaderWorkerLoopSleep));
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
        public int MaxEventSequence;
        public bool TraceRead;
    }

}
