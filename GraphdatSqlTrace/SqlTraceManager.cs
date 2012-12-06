using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using System.Linq;

namespace Alphashack.Graphdat.Agent.SqlTrace
{
    public class SqlTraceManager
    {
        private static SqlTraceManager _instance;

        private readonly EventLog _eventLog;

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
            if (_instance == null) _instance = new SqlTraceManager(eventLog);
        }

        public static void Stop()
        {
            if (_instance != null) _instance.Term();
        }

        private SqlTraceManager(EventLog eventLog)
        {
            try
            {
                _eventLog = eventLog;

                _thread = new Thread(Worker) {IsBackground = true};
                _termHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
                _thread.Start();
            }
            catch (Exception ex)
            {
                _eventLog.WriteEntry(string.Format("SqlTraceManager failed to start due to exception. {0}", ex));
                InvokeStopping(null);
            }
        }

        private void Term()
        {
            if (_termHandle != null) _termHandle.Set();
            if (_thread != null) _thread.Join();
            _thread = null;

            foreach (var database in GetDatabases())
            {
                var conn = new SqlConnection(database.Value.InstanceConnectionString);
                var server = new Server(new ServerConnection(conn));
                var result = server.ConnectionContext.ExecuteNonQuery(StopScript);
            }
        }

        private void Worker()
        {
            try
            {
                var databases = GetDatabases();
                if (databases.Count > 0)
                {
                    ReportDatabases(databases);

                    int timeToWait;
                    do
                    {
                        var processingStart = Stopwatch.StartNew();

                        foreach (var database in databases)
                        {
                            var db = database.Value;

                            var databaseLines = new StringBuilder();
                            foreach (var catalog in db.Catalogs)
                            {
                                databaseLines.AppendLine(
                                    string.Format(databaseLines.Length == 0 ? DatabaseFirstLine : DatabaseLine, catalog));
                            }
                            var script = string.Format(InstanceScript, databaseLines);

                            var conn = new SqlConnection(db.InstanceConnectionString);
                            var server = new Server(new ServerConnection(conn));
                            var result = server.ConnectionContext.ExecuteNonQuery(script);
                        }

                        // calculate time to wait
                        var elapsed = (int) processingStart.ElapsedMilliseconds;
                        timeToWait = Properties.Settings.Default.SqlTraceManagerWorkerLoopSleep - elapsed;
                        if (timeToWait < 0) timeToWait = 0;
                    } while (!_termHandle.WaitOne(timeToWait));
                }
                else
                {
                    _eventLog.WriteEntry(string.Format("SqlTraceManager worker exiting, no databases to monitor."));
                    _thread = null;
                    InvokeStopping(null);
                }
            }
            catch (Exception ex)
            {
                _eventLog.WriteEntry(string.Format("SqlTraceManager worker exiting because of exception. {0}", ex));
                _thread = null;
                InvokeStopping(null);
            }
        }

        private void ReportDatabases(ICollection<KeyValuePair<string, DatabaseInfo>> databases)
        {
            var list = new StringBuilder();
            var databaseCount = 0;
            foreach(var database in databases)
            {
                var db = database.Value;
                var instance = db.DataSource;
                foreach (var catalog in db.Catalogs)
                {
                    list.AppendFormat("{2}{0}.{1}", instance, catalog, databaseCount != 0 ? ", " : "");
                    databaseCount++;
                }
            }

            var report = string.Format("Monitoring {0} instance{1}, {2} database{3}: {4}", databases.Count, databases.Count != 1 ? "s" : "", databaseCount, databaseCount != 1 ? "s" : "", list);
            _eventLog.WriteEntry(report);
        }

        private static Dictionary<string, DatabaseInfo> GetDatabases()
        {
            var databases = new Dictionary<string, DatabaseInfo>();

            foreach (ConnectionStringSettings connectionString in ConfigurationManager.ConnectionStrings)
            {
                ConnectionStringType connectionStringType;
                var name = connectionString.Name.Substring(connectionString.Name.LastIndexOf(".") + 1);
                if (name.StartsWith("database", StringComparison.InvariantCultureIgnoreCase))
                {
                    connectionStringType = ConnectionStringType.Database;
                }
                else if (name.StartsWith("instance", StringComparison.InvariantCultureIgnoreCase))
                {
                    connectionStringType = ConnectionStringType.Instance;
                }
                else
                {
                    continue;
                }

                var builder = new SqlConnectionStringBuilder(connectionString.ConnectionString);

                var dataSource = builder.DataSource;
                var initialCatalog = builder.InitialCatalog;

                DatabaseInfo database;
                if (!databases.TryGetValue(dataSource, out database))
                {
                    database = new DatabaseInfo { DataSource = dataSource };
                    databases.Add(dataSource, database);
                }
                switch (connectionStringType)
                {
                    case ConnectionStringType.Database:
                        if (!database.Catalogs.Contains(initialCatalog)) database.Catalogs.Add(initialCatalog);
                        break;
                    case ConnectionStringType.Instance:
                        database.InstanceConnectionString = connectionString.ConnectionString;
                        break;
                    default:
                        break;
                }
            }

            var noCatalog = databases.Where(dbi => dbi.Value.Catalogs.Count == 0).ToList();
            foreach (var database in noCatalog)
            {
                databases.Remove(database.Key);
            }

            return databases;
        }

        private const string InstanceScript =
    @"
/*
* Manages graphdat trace files for an instance.
* ---------------------------------------------
*
* Stops and Deletes a trace if it exists.
* (This flushes the trace data to the file)
* Creates a new trace.
*
* Notes:
* The trace file(s) will be named like: c:\tmp\graphdat_[INSTANCE NAME]_n.trc
*/

set nocount on

declare @databaseNames table (name nvarchar(64), done bit)
declare @databaseName nvarchar(64)
declare @logicalOperator int

-- Add your database names here
insert @databaseNames
{0}

declare @rc int
declare @doing nvarchar(128)
declare @TraceID int
declare @filename nvarchar(32)
declare @maxfilesize bigint
declare @keepfiles int

set @filename = N'c:\tmp\graphdat_' + @@servicename
set @maxfilesize = 10
set @keepfiles = 5

set @doing = N'Find existing trace'
select @TraceID = traceid from sys.fn_trace_getinfo(0) where property = 2 and cast(value as varchar(1024)) like @filename + N'%.trc';

if @TraceID is NULL goto createTrace

set @doing = N'Stop existing trace'
exec @rc = sp_trace_setstatus @traceid = @TraceID, @status = 0; -- Trace stop

set @doing = N'Delete existing trace'
exec @rc = sp_trace_setstatus @traceid = @TraceID, @status = 2; -- Trace delete

createTrace:

set @doing = N'Create trace'
exec @rc = sp_trace_create @TraceID output, 2, @filename, @maxfilesize, NULL, @keepfiles

if (@rc != 0) goto error

-- Set the events
declare @on bit
set @on = 1
exec sp_trace_setevent @TraceID, 10, 1, @on
exec sp_trace_setevent @TraceID, 10, 12, @on
exec sp_trace_setevent @TraceID, 10, 13, @on
exec sp_trace_setevent @TraceID, 10, 14, @on
exec sp_trace_setevent @TraceID, 10, 15, @on
exec sp_trace_setevent @TraceID, 10, 18, @on
exec sp_trace_setevent @TraceID, 10, 35, @on
exec sp_trace_setevent @TraceID, 10, 51, @on
exec sp_trace_setevent @TraceID, 41, 1, @on
exec sp_trace_setevent @TraceID, 41, 12, @on
exec sp_trace_setevent @TraceID, 41, 13, @on
exec sp_trace_setevent @TraceID, 41, 14, @on
exec sp_trace_setevent @TraceID, 41, 15, @on
exec sp_trace_setevent @TraceID, 41, 18, @on
exec sp_trace_setevent @TraceID, 41, 35, @on
exec sp_trace_setevent @TraceID, 41, 51, @on

set @logicalOperator = 0

-- Set the db name filters
while exists(select * from @databaseNames where done = 0)
begin
	select top 1 @databaseName = name from @databaseNames where done = 0
	
	exec sp_trace_setfilter @TraceID, 35, 1, 6, @databaseName
	set @logicalOperator = 1

	update @databaseNames set done = 1 where done = 0 and name = @databaseName
end

-- Set the trace status to start
set @doing = N'Start trace'
exec sp_trace_setstatus @TraceID, 1
if (@rc != 0) goto error

goto finish

error: 
select Doing=@doing, ErrorCode=@rc

finish:
set nocount off

go
";

        private const string StopScript =
@"
/*
* Manages graphdat trace files for an instance.
* ---------------------------------------------
*
* Stops and Deletes a trace if it exists.
*/

set nocount on

declare @rc int
declare @doing nvarchar(128)
declare @TraceID int
declare @filename nvarchar(32)

set @filename = N'c:\tmp\graphdat_' + @@servicename

set @doing = N'Find existing trace'
select @TraceID = traceid from sys.fn_trace_getinfo(0) where property = 2 and cast(value as varchar(1024)) like @filename + N'%.trc';

if @TraceID is NULL goto finish

set @doing = N'Stop existing trace'
exec @rc = sp_trace_setstatus @traceid = @TraceID, @status = 0; -- Trace stop

set @doing = N'Delete existing trace'
exec @rc = sp_trace_setstatus @traceid = @TraceID, @status = 2; -- Trace delete

error: 
select Doing=@doing, ErrorCode=@rc

finish:
set nocount off

go
";

        private const string DatabaseFirstLine = @"select '{0}', 0";
        private const string DatabaseLine = @"union all select '{0}', 0";

    }

    internal class DatabaseInfo
    {
        public string DataSource;
        public List<string> Catalogs;
        public string InstanceConnectionString;

        public DatabaseInfo()
        {
            Catalogs = new List<string>();
        }
    }

    internal enum ConnectionStringType
    {
        Database,
        Instance
    }

}
