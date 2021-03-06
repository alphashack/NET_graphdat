/*
* Manages graphdat trace files for an instance.
* ---------------------------------------------
*
* Every second this script:
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

-- Add your database names here
insert @databaseNames
	select 'Testing', 0
	-- union all select 'SomeOtherDatabase', 0

declare @rc int
declare @doing nvarchar(128)
declare @TraceID int
declare @filename nvarchar(32)
declare @maxfilesize bigint
declare @keepfiles int

set @filename = N'c:\tmp\graphdat_' + @@servicename
set @maxfilesize = 10
set @keepfiles = 5

--while 1 = 1
--begin

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

-- Set the db name filters
while exists(select * from @databaseNames where done = 0)
begin
	select top 1 @databaseName = name from @databaseNames where done = 0
	
	exec sp_trace_setfilter @TraceID, 35, 0, 6, @databaseName
	
	update @databaseNames set done = 1 where done = 0 and name = @databaseName
end

-- Set the trace status to start
set @doing = N'Start trace'
exec sp_trace_setstatus @TraceID, 1
if (@rc != 0) goto error

goto finish

error: 
select Doing=@doing, ErrorCode=@rc
--break

finish:
--waitfor delay '00:00:10'
set nocount off

go
--end
