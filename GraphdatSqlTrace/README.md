Graphdat SqlTrace Reader Service
====

Windows service that generates sql traces to collect query data and send it to the Graphdat agent.

----

Manages sql traces in multiple db instances for multiple databases.
Reads the trace entries from the (second last) trace file - the one that is not currently in use by sql server.
Canonicalises the queries, removing variable values etc.
Sends the data to the graphdat agent.

----

Prerequisites:

You will need to have MS Sql Server (not express), with "Management Tools - Complete" installed in order for SqlTrace to be work - SqlTrace uses the sql profiler.

----

Intallation:

Run the installer - GraphdatSqlTraceSetup.exe

During the install you will be asked to modify the .config file, see below for details on how to do that.

----

Configuration:

Modify the .config file to specify the connection strings for each database you wish to monitor.

Edit the .config file and put in two types of connection strings in the ConnectionStrings section:

Instance connection strings - A database to connect to and start the trace: we will use this connection string to actually connect to each instance (DataSource). These connection strings should be named starting with "Instance" e.g. Instance01. The InitialCatalog specified is not important, but we reccommend just using master.

Database connection strings - A database to collect data from: a filter will be added to the trace for each database (InitialCatalog) specified when it is run on the instance (DataSource). These connection strings should be named starting with "Database" e.g. Database01. The credential (etc.) need not work, we will not be using this connection string, but the DataSource must match one of the Instance connection string's DataSource, and the InitialCatalog needs to be specified.

The trace will be run once on each MS SQL instance (each unique DataSource in your connection strings). The trace will be customised by the service to filter the trace by database name (each unique InitialCatalog in your connection strings)

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

The user specified in the "Instance" connection string needs to be a member of the sysadmin server role (in order to execute sys.fn_trace_getinfo, sp_trace_setstatus and sp_trace_create)
The service runs as "Local System", it is possible to add this user to your database (as a member of the sysadmin role) and use windows authentication.
The traces will be written to the services program files directory, "Local System" will usually have access to this directory.

Each trace file (even an empty one) will be a minimum of 1MB. Therefore if you increase the frequency of trace rollover, you will affect the disk r/w stats badly.

We are sending the DatabaseName as Method and the Query as Uri so in the graph you will see "DatabaseName : Query"

----

Uninstallation:

There is an uninstall.exe file in the service's directory in program files. Usually: "C:\Program Files(x64)\Alphashack\Graphdat SqlTrace Service"

----

Issues:

The Windows Event Log will have a trace start and stop message every 10 seconds.
	There are several way that you are supposed to be able to prevent this message from being logged, the first two do not seem to always work, the second is a bit heavy handed:
	1
		sp_altermessage 19030, 'with_log', 'false'
		sp_altermessage 19031, 'with_log', 'false'
	2
		add startup parameter -T3688 and restart
	3
		add startup parameter -n (no event log logging)

	Options 1 and 2 are supposed to be working in 2005 and available again or fixed in 2008 SP3 and 2008 R2 SP2 (after being broken), but this does not seem to be the case.
	Option 3 will prevent Sql Server from logging ANY and ALL message to the Windows Event Log. Unfortunately this is the only option that will prevent the Event Log filling up with trace messages. Sql Error messages (and the trace messages...) will still be logged to the Sql Log.
