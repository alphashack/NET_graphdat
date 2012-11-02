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

The trace script needs to be run once on each instance (each unique DataSource in your connection strings). The script will be customised to filter the trace by database name (each unique InitialCatalog in your connection strings)

Edit the App.config file and put in two types of connection strings:

Instance connection strings - we will use this connection string to actually connect to each instance (DataSource). These connection strings should be named starting with "Instance" e.g. Instance01. The InitialCatalog specified is not important, but I would reccommend just using master.

Database connection strings - a filter will be added for each database (InitialCatalog) specified when it is run on the instance (DataSource). These connection strings should be named starting with "Database" e.g. Database01. The credential (etc.) need not work, we will not be using this connection string, but the DataSource must match one of the Instance connection string's DataSource, and the InitialCatalog needs to be specified.

----

Intallation:

Sql Tracing can only be done from a 32 bit program. The windows service is therefore a 32 bit program. As such it must be installed using the correct 32 bit installutil. This can be done like this:

c:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe ./GraphdatAgentSqlTraceReader.exe

----

Starting and Stopping the service

net start graphdat-sqltrace

net stop graphdat-sqltrace

----

Notes:

Each trace file (even an empty one) will be 1MB. Therefore if you increase the frequency of trace rollover, you will affect the disk r/w stats badly.

We are cheating a bit to get this data into the "request response time" graph. We are sending the DatabaseName as Method and the Query as Uri so in the graph you will see "DatabaseName : Query"

----

Issues:

I could not find a reliable way to test the sql script execution for success... ExecuteNonQuery is supposed to return -1 but it seems to like returning -2... I have just left it for now and failure will be silent.

Trace start and stop messages are send to the windows event log thus filling the event log (possibly the sql log also). Perhaps there is a way to turn this off? You can turn off event logging altogether by starting the sql server instance with the -n argument but this is probably not what we want.

I cannot, for some reason, get the DatabaseName out of the trace file... look at the line with getOrdinal("DatabaseName")... buggered if I know, must be something stupid