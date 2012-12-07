Graphdat SqlTrace Reader Service
====

Windows service to monitor the sql traces generated when running the graphdat sql trace script.

----

Manages sql traces in multiple db instances for multiple databases.
Reads the trace entries from the (second last) trace file - the one that is not currently in use by sql server.
Canonicalises the queries, removing variable values etc.
Sends the data to the graphdat agent.

----

Configuration:

Modify the .config file to specify the connection strings for each database you wish to monitor.

The trace will be run once on each MS SQL instance (each unique DataSource in your connection strings). The trace will be customised by the service to filter the trace by database name (each unique InitialCatalog in your connection strings)

Edit the .config file and put in two types of connection strings in the ConnectionStrings section:

Instance connection strings - A database to connect to and start the trace: we will use this connection string to actually connect to each instance (DataSource). These connection strings should be named starting with "Instance" e.g. Instance01. The InitialCatalog specified is not important, but we reccommend just using master.

Database connection strings - A database to collect data from: a filter will be added to the trace for each database (InitialCatalog) specified when it is run on the instance (DataSource). These connection strings should be named starting with "Database" e.g. Database01. The credential (etc.) need not work, we will not be using this connection string, but the DataSource must match one of the Instance connection string's DataSource, and the InitialCatalog needs to be specified.

----

Intallation:

Run the installer - GraphdatSqlTraceSetup.exe

During the install you will be asked to modify the .config file, see above for details on how to do that.

----

Starting and Stopping the service:

The service will start automatically after install and on system reboot. You can also stop and start it manually:

net start graphdat-sqltrace

net stop graphdat-sqltrace

----

Logging:

The service will log startup and shutdown message and any error messages to the Windows Event Log Application Log

----

Notes:

The service runs as "Local Service", it is possible to add this user to your database (with appropriate permissions to execute sys.fn_trace_getinfo, sp_trace_setstatus and sp_trace_create)

Each trace file (even an empty one) will be 1MB. Therefore if you increase the frequency of trace rollover, you will affect the disk r/w stats badly.

We are sending the DatabaseName as Method and the Query as Uri so in the graph you will see "DatabaseName : Query"

----

Uninstallation:

There is an uninstall.exe file in the service's directory in program files. Usually: "C:\Program Files(x64)\Alphashack\Graphdat SqlTrace Service"

----

Issues:

I could not find a reliable way to test the sql script execution for success... ExecuteNonQuery is supposed to return -1 but it seems to like returning -2 most of the time but returns -1 when it fails... I have just left it for now and failure will be silent.

Trace start and stop messages are send to the windows event log thus filling the event log (possibly the sql log also). Perhaps there is a way to turn this off? You can turn off event logging altogether by starting the sql server instance with the -n argument but this is probably not what we want.

I cannot, for some reason, get the DatabaseName out of the trace file... look at the line with getOrdinal("DatabaseName")... buggered if I know, must be something stupid
